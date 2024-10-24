using Common.Utils;
using Common.Models;
using CSharpDiff.Patches.Models;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace Common.ExternalApis
{
    public class OracleSchemaService : IOracleSchemaService
    {
        private readonly ILogger<OracleSchemaService> _logger;
        private readonly IConfigurationService _configService;

        public OracleSchemaService(ILogger<OracleSchemaService> logger, IConfigurationService configService)
        {
            _logger = logger;
            _configService = configService;
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
                    difs.Add(viewName, OracleDiffUtils.GetDiff(viewName, devViews[viewName], qaViews[viewName]));
                }
                else
                {
                    difs.Add(viewName, OracleDiffUtils.GetDiff(viewName, devViews[viewName], string.Empty));

                    _logger.LogInformation($"View {viewName} is present in DEV but not in QA");
                }
            }

            foreach (var viewName in qaViews.Keys)
            {
                if (!devViews.ContainsKey(viewName))
                {
                    difs.Add(viewName, OracleDiffUtils.GetDiff(viewName, string.Empty, qaViews[viewName]));

                    _logger.LogInformation($"View {viewName} is present in QA but not in DEV");
                }
            }

            Dictionary<string, string> difsString = new Dictionary<string, string>();

            foreach (var kv in difs)
            {
                if (kv.Value.Hunks.Any())
                {
                    _logger.LogInformation($"Difference in view: {kv.Key}");
                    difsString.Add(kv.Key, OracleDiffUtils.Format(kv.Value));
                }
            }

            return difsString;
        }
    }
}
