using Common.Models;
using Common.Repositories.TCP.Interfaces;
using Common.Services.Interfaces;
using Common.Utils;
using CSharpDiff.Diffs.Models;
using CSharpDiff.Patches;
using CSharpDiff.Patches.Models;
using Microsoft.Extensions.Logging;
using SQL.Formatter.Language;
using static SQL.Formatter.SqlFormatter;

namespace Common.Services
{
    public class OracleSchemaService : IOracleSchemaService
    {
        private readonly ILogger<OracleSchemaService> _logger;
        private readonly IConfigurationService _configService;
        private readonly Formatter _formatter;
        private readonly IOracleRepository _repo;

        public OracleSchemaService(
            ILogger<OracleSchemaService> logger,
            IConfigurationService configService,
            IOracleRepository repo)
        {
            _logger = logger;
            _configService = configService;
            _formatter = Of(Dialect.PlSql);
            _repo = repo;
        }

        public async Task<IEnumerable<OracleDiffResult>> Compare(string sourceEnvName, string targetEnvName)
        {
            var config = _configService.GetConfig();

            var sourceEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == sourceEnvName)
                ?? throw new ArgumentException($"Source environment '{sourceEnvName}' not found");

            var targetEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == targetEnvName)
                ?? throw new ArgumentException($"Target environment '{targetEnvName}' not found");

            var sourceViews = await GetViewDefinitionsAsync(sourceEnv.ConnectionString, sourceEnv.Schema);
            var targetViews = await GetViewDefinitionsAsync(targetEnv.ConnectionString, targetEnv.Schema);

            return await CompareViewDefinitions(sourceViews, targetViews);
        }

        public async Task<bool> TestConnectionAsync(string connectionString, string schema)
        {
            try
            {
                await _repo.GetSingleFromSqlAsync<int>(
                    connectionString,
                    $"SELECT COUNT(*) FROM ALL_VIEWS WHERE OWNER = {schema}",
                    default);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao testar conexão do Oracle");
                return false;
            }
        }

        public async Task<OracleViewDefinition> GetViewDefinitionAsync(string connectionString, string schema, string viewName)
        {
            return await _repo.GetSingleFromSqlAsync<OracleViewDefinition>(
                connectionString,
                $"SELECT Owner, VIEW_NAME AS Name, TEXT AS Definition FROM ALL_VIEWS WHERE OWNER = {schema} AND VIEW_NAME = {viewName} ",
                default);
        }

        private Dictionary<string, string> CreateViewDictionary(IEnumerable<OracleViewDefinition> views)
        {
            return views.ToDictionary(
                v => v.Name.ToUpperInvariant(),
                v => v.Definition,
                StringComparer.OrdinalIgnoreCase
            );
        }

        public async Task<IEnumerable<OracleViewDefinition>> GetViewDefinitionsAsync(string connectionString, string schema)
        {
            return await _repo.GetFromSqlAsync<OracleViewDefinition>(
                connectionString,
                $"SELECT Owner, VIEW_NAME AS Name, TEXT AS Definition FROM ALL_VIEWS WHERE OWNER = {schema}",
                default);
        }

        public async Task<IEnumerable<OracleDiffResult>> CompareViewDefinitions(IEnumerable<OracleViewDefinition> devViews, IEnumerable<OracleViewDefinition> qaViews)
        {
            var differences = new List<OracleDiffResult>();

            var devViewDict = CreateViewDictionary(devViews);
            var qaViewDict = CreateViewDictionary(qaViews);

            foreach (var devView in devViews)
            {
                var viewName = devView.Name;
                if (qaViewDict.TryGetValue(viewName, out string? value))
                {
                    var diff = await GetViewDiffAsync(viewName, devView.Definition, value);
                    if (diff.HasDifferences)
                    {
                        _logger.LogInformation($"Diferença na view: {viewName}");
                    }

                    differences.Add(diff);
                }
                else
                {
                    var diff = await GetViewDiffAsync(viewName, devView.Definition, string.Empty);
                    differences.Add(diff);
                    _logger.LogInformation($"A view {viewName} está presente no DEV, mas não no QA");
                }
            }

            foreach (var qaView in qaViews)
            {
                var viewName = qaView.Name;
                if (!devViewDict.ContainsKey(viewName))
                {
                    var diff = await GetViewDiffAsync(viewName, string.Empty, qaView.Definition);
                    differences.Add(diff);
                    _logger.LogInformation($"A view {viewName} está presente no QA, mas não no DEV");
                }
            }

            return differences;
        }


        public async Task<OracleDiffResult> GetViewDiffAsync(string viewName, string oldContent, string newContent)
        {
            var viewNameFormatted = $"{viewName}.SQL";
            var ps = new Patch(new PatchOptions(), new DiffOptions());

            // Execute NormalizeLineBreaks in parallel
            var normalizeOldTask = Task.Run(() => NormalizeLineBreaks(oldContent));
            var normalizeNewTask = Task.Run(() => NormalizeLineBreaks(newContent));

            await Task.WhenAll(normalizeOldTask, normalizeNewTask);

            var normalizedOldContent = normalizeOldTask.Result;
            var normalizedNewContent = normalizeNewTask.Result;

            var patch = ps.createPatchResult(
                viewNameFormatted,
                viewNameFormatted,
                normalizedOldContent,
                normalizedNewContent,
                null,
                null
            );

            var hasDifferences = patch.Hunks.Any();
            var diffString = "diff --git" + "\r\n" + ps.formatPatch(patch);

            return new OracleDiffResult(viewName, diffString, hasDifferences)
            {
                Patch = patch
            };
        }

        private string NormalizeLineBreaks(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Replace Windows line breaks (\r\n) with Unix line breaks (\n)
            text = text.Replace("\r\n", "\n");

            // Replace old Mac line breaks (\r) with Unix line breaks (\n)
            text = text.Replace("\r", "\n");

            text = _formatter.Format(text);

            return text;
        }
       public async Task<IEnumerable<string>> GetTablesAndViewsAsync(string connectionString, string schema)
       {
           return await _repo.GetFromSqlAsync<string>(
               connectionString,
               $"SELECT OBJECT_NAME FROM ALL_OBJECTS WHERE OWNER = {schema} AND OBJECT_TYPE IN ('TABLE', 'VIEW') ORDER BY OBJECT_NAME",
               default);
       }

       public async Task<IEnumerable<OracleColumn>> GetTableOrViewColumnsAsync(string connectionString, string schema, string objectName)
       {
           return await _repo.GetFromSqlAsync<OracleColumn>(
               connectionString,
               $"SELECT COLUMN_NAME, DATA_TYPE, DATA_LENGTH, DATA_PRECISION, DATA_SCALE, NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER = {schema} AND TABLE_NAME = {objectName} ORDER BY COLUMN_ID",
               default);
       }

       public async Task<string> GenerateEfCoreMappingClassAsync(string connectionString, string schema, string objectName, string className)
        {
            var columns = await GetTableOrViewColumnsAsync(connectionString, schema, objectName);
            var sb = new System.Text.StringBuilder();

            foreach (var column in columns)
            {
                sb.AppendLine(GenerateProperty(column));
                sb.AppendLine();
            }

            return sb.ToString();
        }

       private string GenerateProperty(OracleColumn column)
        {
            var propertyName = column.COLUMN_NAME.ToPascalCase();
            var propertyChain = $"builder.Property(x => x.{propertyName})\n"
                             + $"\t.HasColumnName(\"{column.COLUMN_NAME}\")\n"
                             + $"\t.HasColumnType(\"{column.DATA_TYPE}\")";

            if (column.DATA_TYPE == "NUMBER" && column.DATA_PRECISION.HasValue && column.DATA_SCALE.HasValue)
            {
                propertyChain += $"\n\t.HasPrecision({column.DATA_PRECISION.Value}, {column.DATA_SCALE.Value})";
            }
            else if (column.DATA_TYPE.Contains("VARCHAR") && column.DATA_LENGTH.HasValue)
            {
                propertyChain += $"\n\t.HasMaxLength({column.DATA_LENGTH.Value})";
            }

            return propertyChain + ";";
        }

       private string GetCSharpType(string oracleType)
       {
           if (oracleType.StartsWith("TIMESTAMP")) return "DateTime";

           return oracleType switch
           {
               "DATE" => "DateTime",
               "NUMBER" => "decimal",
               "FLOAT" => "double",
               "BINARY_FLOAT" => "float",
               "BINARY_DOUBLE" => "double",
               "VARCHAR2" => "string",
               "NVARCHAR2" => "string",
               "CHAR" => "string",
               "NCHAR" => "string",
               "CLOB" => "string",
               "NCLOB" => "string",
               "BLOB" => "byte[]",
               "RAW" => "byte[]",
               "LONG RAW" => "byte[]",
               _ => "string"
           };
       }
   }
}
