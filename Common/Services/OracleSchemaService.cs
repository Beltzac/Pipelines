using Common.Models;
using Common.Services.Interfaces;
using CSharpDiff.Diffs.Models;
using CSharpDiff.Patches;
using CSharpDiff.Patches.Models;
using Dapper;
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
        private readonly IOracleConnectionFactory _connectionFactory;

        public OracleSchemaService(
            ILogger<OracleSchemaService> logger,
            IConfigurationService configService,
            IOracleConnectionFactory connectionFactory)
        {
            _logger = logger;
            _configService = configService;
            _formatter = Of(Dialect.PlSql);
            _connectionFactory = connectionFactory;
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
                using var connection = _connectionFactory.CreateConnection(connectionString);

                var sql = "SELECT COUNT(*) FROM ALL_VIEWS WHERE OWNER = :schema";

                await connection.QueryFirstAsync<int>(
                    sql,
                    new { schema },
                    commandTimeout: 120
                );
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
            using var connection = _connectionFactory.CreateConnection(connectionString);

            var sql = "SELECT TEXT FROM ALL_VIEWS WHERE OWNER = :schema AND VIEW_NAME = :viewName";

            var text = await connection.QueryFirstOrDefaultAsync<string>(
                sql,
                new { schema, viewName },
                commandTimeout: 120
            );

            return new OracleViewDefinition(viewName, text ?? string.Empty);
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
            using var connection = _connectionFactory.CreateConnection(connectionString);
            var sql = "SELECT VIEW_NAME, TEXT FROM ALL_VIEWS WHERE OWNER = :schema";
            
            var results = await connection.QueryAsync<(string ViewName, string Text)>(
                sql, 
                new { schema }, 
                commandTimeout: 120
            );

            return results.Select(x => new OracleViewDefinition(x.ViewName, x.Text ?? string.Empty));
        }

        public async Task<IEnumerable<OracleDiffResult>> CompareViewDefinitions(IEnumerable<OracleViewDefinition> devViews, IEnumerable<OracleViewDefinition> qaViews)
        {
            var differences = new List<OracleDiffResult>();

            var devViewDict = CreateViewDictionary(devViews);
            var qaViewDict = CreateViewDictionary(qaViews);

            foreach (var devView in devViews)
            {
                var viewName = devView.Name;
                if (qaViewDict.ContainsKey(viewName))
                {
                    var diff = GetViewDiff(viewName, devView.Definition, qaViewDict[viewName]);
                    if (diff.HasDifferences)
                    {
                        _logger.LogInformation($"Diferença na view: {viewName}");
                        differences.Add(diff);
                    }
                }
                else
                {
                    var diff = GetViewDiff(viewName, devView.Definition, string.Empty);
                    differences.Add(diff);
                    _logger.LogInformation($"A view {viewName} está presente no DEV, mas não no QA");
                }
            }

            foreach (var qaView in qaViews)
            {
                var viewName = qaView.Name;
                if (!devViewDict.ContainsKey(viewName))
                {
                    var diff = GetViewDiff(viewName, string.Empty, qaView.Definition);
                    differences.Add(diff);
                    _logger.LogInformation($"A view {viewName} está presente no QA, mas não no DEV");
                }
            }

            return differences;
        }


        public OracleDiffResult GetViewDiff(string viewName, string oldContent, string newContent)
        {
            var viewNameFormatted = $"{viewName}.SQL";
            var ps = new Patch(new PatchOptions(), new DiffOptions());
            
            var patch = ps.createPatchResult(
                viewNameFormatted,
                viewNameFormatted,
                NormalizeLineBreaks(oldContent),
                NormalizeLineBreaks(newContent),
                null,
                null
            );

            var formattedDiff = ps.formatPatch(patch);
            var hasDifferences = patch.Hunks.Any();

            return new OracleDiffResult(viewName, formattedDiff, hasDifferences);
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
    }
}
