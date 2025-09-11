using Common.Models;
using Common.Repositories.TCP.Interfaces;
using Common.Services.Interfaces;
using Common.Utils;
using CSharpDiff.Diffs.Models;
using CSharpDiff.Patches;
using CSharpDiff.Patches.Models;
using Microsoft.Extensions.Logging;
using SQL.Formatter.Language;
using System.Runtime.CompilerServices;
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

        public async Task<OracleConnectionTestResult> TestConnectionAsync(string connectionString)
        {
            try
            {
                await _repo.GetSingleFromSqlAsync<int>(
                    connectionString,
                    $"SELECT 1 as \"Value\" FROM DUAL",
                    default);

                return new OracleConnectionTestResult
                {
                    IsConnected = true,
                    ErrorMessage = null,
                    ConnectionDetails = "Connection successful"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao testar conexão do Oracle");
                return new OracleConnectionTestResult
                {
                    IsConnected = false,
                    ErrorMessage = ex.Message,
                    ConnectionDetails = $"Connection failed: {ex.GetType().Name}"
                };
            }
        }

        public async Task<OracleViewDefinition> GetViewDefinitionAsync(string connectionString, string schema, string viewName)
        {
            var query = $@"
                SELECT Owner, Name, Definition
                FROM (
                    SELECT
                        av.Owner,
                        av.VIEW_NAME AS Name,
                        DBMS_METADATA.GET_DDL('VIEW', av.VIEW_NAME, av.OWNER) AS Definition
                    FROM ALL_VIEWS av
                    WHERE av.OWNER = '{schema.ToUpperInvariant()}' AND av.VIEW_NAME = '{viewName.ToUpperInvariant()}'
                )";

            return await _repo.GetSingleFromSqlAsync<OracleViewDefinition>(
                connectionString,
                FormattableStringFactory.Create(query),
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

/*SELECT Owner, Name, Definition
FROM (
    SELECT
        av.Owner,
        av.VIEW_NAME AS Name,
        DBMS_METADATA.GET_DDL('VIEW', av.VIEW_NAME, av.OWNER) AS Definition,
        ROW_NUMBER() OVER (ORDER BY av.VIEW_NAME) AS rn
    FROM ALL_VIEWS av
    WHERE av.OWNER = 'TCPAPI'
) ranked
WHERE rn BETWEEN 1 AND 200
AND (
    LOWER(ranked.Name) LIKE '%container%'
    OR
    dbms_lob.instr( ranked.Definition ,'container') > 0
);
*/
        public async Task<IEnumerable<OracleViewDefinition>> GetViewDefinitionsAsync(string connectionString, string schema, string? search = null, int pageSize = 9999, int pageNumber = 1)
        {
            var whereClauses = new List<string>();
            whereClauses.Add($"rn BETWEEN {(pageNumber - 1) * pageSize + 1} AND {pageNumber * pageSize}");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchStrings = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var searchString in searchStrings)
                {
                    whereClauses.Add(@$"(
                       dbms_lob.instr( ranked.Definition, '{searchString.ToLowerInvariant()}') > 0
                       OR dbms_lob.instr( ranked.Definition, '{searchString.ToUpperInvariant()}') > 0
                    )");
                }
            }

            var whereClause = "WHERE " + string.Join(" AND ", whereClauses);


            var query = $@"
                    SELECT Owner, Name, Definition
                    FROM (
                        SELECT
                                av.Owner,
                                av.VIEW_NAME AS Name,
                                DBMS_METADATA.GET_DDL('VIEW', av.VIEW_NAME, av.OWNER) AS Definition,
                                ROW_NUMBER() OVER (ORDER BY av.VIEW_NAME) AS rn
                            FROM
                                ALL_VIEWS av
                            WHERE
                                av.OWNER = '{schema.ToUpperInvariant()}'

                    ) ranked
                    {whereClause}

                    ";


            return await _repo.GetFromSqlAsync<OracleViewDefinition>(
                connectionString,
                FormattableStringFactory.Create(query),
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
       public async Task<OracleTablesAndViewsResult> GetTablesAndViewsAsync(
           string connectionString,
           string? schema = null,
           string? search = null,
           int pageSize = 50,
           int pageNumber = 1)
       {
           var whereClauses = new List<string>();
           if (!string.IsNullOrWhiteSpace(schema))
           {
               whereClauses.Add($"OWNER = '{schema.ToUpperInvariant()}'");
           }
           if (!string.IsNullOrWhiteSpace(search))
           {
               whereClauses.Add($"LOWER(OBJECT_NAME) LIKE '%{search.ToLowerInvariant()}%'");
           }

           var whereClause = whereClauses.Any() ? "WHERE " + string.Join(" AND ", whereClauses) : "";

           var baseQuery = $@"
               FROM (
                   SELECT TABLE_NAME AS OBJECT_NAME, OWNER FROM ALL_TABLES
                   UNION ALL
                   SELECT VIEW_NAME AS OBJECT_NAME, OWNER FROM ALL_VIEWS
               ) {whereClause}
           ";

           var dataQuery = $@"
               SELECT OBJECT_NAME AS Name, OWNER
               FROM (
                   SELECT OBJECT_NAME, OWNER, ROW_NUMBER() OVER (ORDER BY OBJECT_NAME) AS rn
                   {baseQuery}
               ) ranked
               WHERE rn BETWEEN {(pageNumber - 1) * pageSize + 1} AND {pageNumber * pageSize}
           ";

           var results = await _repo.GetFromSqlAsync<OracleTableOrViewInfo>(
               connectionString,
               FormattableStringFactory.Create(dataQuery),
               default
           );

           var countQuery = $"SELECT COUNT(*) {baseQuery}";

           var totalCountResult = await _repo.GetFromSqlAsync<int>(
               connectionString,
               FormattableStringFactory.Create(countQuery),
               default
           );

           var totalCount = totalCountResult.FirstOrDefault();

           return new OracleTablesAndViewsResult
           {
               Results = results,
               TotalCount = totalCount
           };
       }

       public async Task<IEnumerable<OracleColumn>> GetTableOrViewColumnsAsync(string connectionString, string schema, string objectName)
       {
           return await _repo.GetFromSqlAsync<OracleColumn>(
               connectionString,
               $@"SELECT col.COLUMN_NAME,
                         col.DATA_TYPE,
                         col.DATA_LENGTH,
                         col.DATA_PRECISION,
                         col.DATA_SCALE,
                         col.NULLABLE,
                         com.COMMENTS
                  FROM ALL_TAB_COLUMNS col
                  LEFT JOIN ALL_COL_COMMENTS com
                         ON col.OWNER = com.OWNER
                        AND col.TABLE_NAME = com.TABLE_NAME
                        AND col.COLUMN_NAME = com.COLUMN_NAME
                  WHERE col.OWNER = {schema} AND col.TABLE_NAME = {objectName}
                  ORDER BY col.COLUMN_ID",
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

       public async Task<string> GenerateCSharpClassAsync(string connectionString, string schema, string objectName, string className)
       {
           var columns = await GetTableOrViewColumnsAsync(connectionString, schema, objectName);
           var sb = new System.Text.StringBuilder();

           var actualClassName = string.IsNullOrEmpty(className) ? objectName.ToPascalCase() : className;

           sb.AppendLine($"public class {actualClassName}");
           sb.AppendLine("{");

           foreach (var column in columns)
           {
               var cSharpType = GetCSharpType(column.DataType, column.ColumnName);
               var propertyName = column.ColumnName.ToPascalCase();
               var nullableIndicator = column.Nullable == "Y" && IsNullableType(cSharpType) ? "?" : "";
               sb.AppendLine($"    public {cSharpType}{nullableIndicator} {propertyName} {{ get; set; }}");
           }

           sb.AppendLine("}");

           return sb.ToString();
       }

       private bool IsNullableType(string cSharpType)
       {
           return cSharpType switch
           {
               "string" => true,
               "byte[]" => true,
               _ => false
           };
       }

       private string GenerateProperty(OracleColumn column)
       {
           var propertyName = column.ColumnName.ToPascalCase();
           var columnType = GetFullColumnType(column);

           var propertyChain = $"\t\t\tbuilder.Property(x => x.{propertyName})\n"
                            + $"\t\t\t\t.HasColumnName(\"{column.ColumnName}\")\n"
                            + $"\t\t\t\t.HasColumnType(\"{columnType}\")";

           if (column.Nullable == "N")
           {
               propertyChain += "\n\t\t\t\t.IsRequired()";
           }

           return propertyChain + ";";
       }




        private string GetFullColumnType(OracleColumn column)
        {
            switch (column.DataType)
            {
                case "VARCHAR2":
                case "NVARCHAR2":
                case "CHAR":
                    return $"{column.DataType}({column.DataLength})";
                case "NUMBER":
                    if (column.DataPrecision.HasValue && column.DataScale.HasValue)
                    {
                        return $"NUMBER({column.DataPrecision.Value}, {column.DataScale.Value})";
                    }
                    if (column.DataPrecision.HasValue)
                    {
                        return $"NUMBER({column.DataPrecision.Value})";
                    }
                    return "NUMBER";
                default:
                    return column.DataType;
            }
        }

       private string GetCSharpType(string oracleType, string columnName)
       {
           // Rule: "Is*** should be bools"
           if (columnName.StartsWith("IS", StringComparison.OrdinalIgnoreCase))
           {
               return "bool";
           }

           // Rule: "ids should be long"
           if (columnName.Contains("ID", StringComparison.OrdinalIgnoreCase) && oracleType == "NUMBER")
           {
               return "long";
           }

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

       public async Task<string> AnalyzeQueryPerformanceAsync(string connectionString, string schema, string sql)
       {
           try
           {
               var block = $@"
                    DECLARE
                    l_cursor SYS_REFCURSOR;
                    BEGIN
                    -- Run the EXPLAIN PLAN
                    EXECUTE IMMEDIATE 'EXPLAIN PLAN FOR {sql.Replace("'", "''")}';

                    -- Return the plan in the same call
                    OPEN l_cursor FOR
                        SELECT PLAN_TABLE_OUTPUT
                        FROM TABLE(DBMS_XPLAN.DISPLAY(NULL, NULL, 'BASIC +NOTE +OUTLINE +ALIAS +PROJECTION +PREDICATE +BYTES +COST +PARTITION +PARALLEL'));

                    DBMS_SQL.RETURN_RESULT(l_cursor);
                    END;";

               var planLines = await _repo.GetFromSqlAsync<string>(
                   connectionString,
                   FormattableStringFactory.Create(block),
                   default);

               var planText = string.Join(Environment.NewLine, planLines);

               return $"Execution Plan:\n{planText}";
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Error analyzing Oracle query performance");
               return $"Error analyzing query performance: {ex.Message}";
           }
       }

       public async Task<OracleQueryResult> ExecuteSelectQueryAsync(string connectionString, string sql)
       {
           if (string.IsNullOrWhiteSpace(sql) || !sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
           {
               throw new ArgumentException("Only SELECT statements are allowed.");
           }

           try
           {
               var rows = await _repo.GetFromSqlDynamicAsync(
                   connectionString,
                   FormattableStringFactory.Create(sql),
                   default
               );

               return new OracleQueryResult
               {
                   Rows = rows.ToList(),
                   TotalCount = rows.Count()
               };
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Error executing Oracle SELECT query");
               throw;
           }
       }

       public async Task<IEnumerable<string>> GetSchemasAsync(string connectionString)
       {
           var query = "SELECT DISTINCT USERNAME FROM ALL_USERS ORDER BY USERNAME";
           var schemas = await _repo.GetFromSqlAsync<string>(
               connectionString,
               FormattableStringFactory.Create(query),
               default
           );
           return schemas;
       }

       public async Task<IEnumerable<OracleDependency>> GetOracleDependenciesAsync(string connectionString, string schema, string objectName, string objectType)
       {
           if (string.IsNullOrWhiteSpace(schema))
               throw new ArgumentException("Schema cannot be null or empty.", nameof(schema));
           if (string.IsNullOrWhiteSpace(objectName))
               throw new ArgumentException("Object name cannot be null or empty.", nameof(objectName));
           if (string.IsNullOrWhiteSpace(objectType))
               throw new ArgumentException("Object type cannot be null or empty.", nameof(objectType));

           var query = $@"
               SELECT CONNECT_BY_ROOT d.type as Type, d.name AS Referencee, d.referenced_owner as ReferencedSchema, d.referenced_name as ReferencedName
               FROM all_dependencies d
               WHERE d.owner = '{schema.ToUpperInvariant()}'
                 AND d.referenced_type in ('TABLE', 'VIEW')
               START WITH d.name = '{objectName.ToUpperInvariant()}' AND d.type = '{objectType.ToUpperInvariant()}'
               CONNECT BY PRIOR d.referenced_name = d.name AND PRIOR d.referenced_type = d.type";

           return await _repo.GetFromSqlAsync<OracleDependency>(
               connectionString,
               FormattableStringFactory.Create(query),
               default
           );
       }
   }
}
