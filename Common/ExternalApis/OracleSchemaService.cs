using CSharpDiff.Diffs.Models;
using CSharpDiff.Patches;
using CSharpDiff.Patches.Models;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using SQL.Formatter;
using SQL.Formatter.Language;
using static SQL.Formatter.SqlFormatter;


namespace Common.ExternalApis
{
    public class OracleSchemaService : IOracleSchemaService
    {
        private readonly ILogger<OracleSchemaService> _logger;
        private readonly IConfigurationService _configService;
        private readonly Formatter _formatter;

        public OracleSchemaService(ILogger<OracleSchemaService> logger, IConfigurationService configService)
        {
            _logger = logger;
            _configService = configService;
            _formatter = SqlFormatter.Of(Dialect.PlSql);
        }

        public Dictionary<string, string> Compare(string sourceEnvName, string targetEnvName)
        {
            var config = _configService.GetConfig();

            var sourceEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == sourceEnvName)
                ?? throw new ArgumentException($"Source environment '{sourceEnvName}' not found");

            var targetEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == targetEnvName)
                ?? throw new ArgumentException($"Target environment '{targetEnvName}' not found");

            var sourceViews = GetViewDefinitions(sourceEnv.ConnectionString, sourceEnv.Schema);
            var targetViews = GetViewDefinitions(targetEnv.ConnectionString, targetEnv.Schema);

            return CompareViewDefinitions(sourceViews, targetViews);
        }

        public async Task<bool> TestConnectionAsync(string connectionString, string schema)
        {
            try
            {
                using (var connection = new OracleConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand($"SELECT COUNT(*) FROM ALL_VIEWS WHERE OWNER = :schema", connection))
                    {
                        command.Parameters.Add(new OracleParameter("schema", schema));
                        await command.ExecuteScalarAsync();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Oracle connection");
                return false;
            }
        }

        public async Task<string> GetViewDefinitionAsync(string connectionString, string schema, string viewName)
        {
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand($"SELECT TEXT FROM ALL_VIEWS WHERE OWNER = :schema AND VIEW_NAME = :viewName", connection))
                {
                    command.InitialLONGFetchSize = -1;
                    command.Parameters.Add(new OracleParameter("schema", schema));
                    command.Parameters.Add(new OracleParameter("viewName", viewName));

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return reader.GetOracleString(0).Value;
                        }
                        return string.Empty;
                    }
                }
            }
        }

        public Dictionary<string, string> GetViewDefinitions(string connectionString, string schema)
        {
            var viewDefinitions = new Dictionary<string, string>();

            using (var connection = new OracleConnection(connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand($"SELECT VIEW_NAME, TEXT FROM ALL_VIEWS WHERE OWNER = '{schema}'", connection))
                {
                    command.InitialLONGFetchSize = -1;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string viewName = reader.GetString(0);
                            string viewText = reader.GetOracleString(1).Value;
                            viewDefinitions[viewName] = viewText;
                        }
                    }
                }
            }

            return viewDefinitions;
        }

        public Dictionary<string, string> CompareViewDefinitions(Dictionary<string, string> devViews, Dictionary<string, string> qaViews)
        {
            Dictionary<string, PatchResult> difs = new Dictionary<string, PatchResult>();

            foreach (var viewName in devViews.Keys)
            {
                if (qaViews.ContainsKey(viewName))
                {
                    difs.Add(viewName, GetDiff(viewName, devViews[viewName], qaViews[viewName]));
                }
                else
                {
                    difs.Add(viewName, GetDiff(viewName, devViews[viewName], string.Empty));

                    _logger.LogInformation($"View {viewName} is present in DEV but not in QA");
                }
            }

            foreach (var viewName in qaViews.Keys)
            {
                if (!devViews.ContainsKey(viewName))
                {
                    difs.Add(viewName, GetDiff(viewName, string.Empty, qaViews[viewName]));

                    _logger.LogInformation($"View {viewName} is present in QA but not in DEV");
                }
            }

            Dictionary<string, string> difsString = new Dictionary<string, string>();

            foreach (var kv in difs)
            {
                if (kv.Value.Hunks.Any())
                {
                    _logger.LogInformation($"Difference in view: {kv.Key}");
                    difsString.Add(kv.Key, Format(kv.Value));
                }
            }

            return difsString;
        }

        public PatchResult GetDiff(string view, string old, string newString)
        {
            var viewNameFormated = $"{view}.SQL";

            var ps = new Patch(new PatchOptions(), new DiffOptions());

            var patch = ps.createPatchResult(viewNameFormated, viewNameFormated, NormalizeLineBreaks(old), NormalizeLineBreaks(newString), null, null);

            return patch;
        }

        public static string Format(PatchResult patchResult)
        {
            var ps = new Patch(new PatchOptions(), new DiffOptions());
            return ps.formatPatch(patchResult);
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
