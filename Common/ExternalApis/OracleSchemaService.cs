﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using CSharpDiff.Diffs.Models;
using CSharpDiff.Patches;
using CSharpDiff.Patches.Models;
using Common.Models;
using Common.Services;
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

        public async Task<IEnumerable<OracleDiffResult>> Compare(string sourceEnvName, string targetEnvName)
        {
            var config = _configService.GetConfig();

            var sourceEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == sourceEnvName)
                ?? throw new ArgumentException($"Source environment '{sourceEnvName}' not found");

            var targetEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == targetEnvName)
                ?? throw new ArgumentException($"Target environment '{targetEnvName}' not found");

            var sourceViews = GetViewDefinitions(sourceEnv.ConnectionString, sourceEnv.Schema);
            var targetViews = GetViewDefinitions(targetEnv.ConnectionString, targetEnv.Schema);

            return await CompareViewDefinitions(sourceViews, targetViews);
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

        public async Task<OracleViewDefinition> GetViewDefinitionAsync(string connectionString, string schema, string viewName)
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
                            return new OracleViewDefinition(viewName, reader.GetOracleString(0).Value);
                        }
                        return new OracleViewDefinition(viewName, string.Empty);
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

        public async Task<IEnumerable<OracleViewDefinition>> GetViewDefinitionsAsync(string connectionString, string schema)
        {
            var definitions = GetViewDefinitions(connectionString, schema);
            return definitions.Select(kvp => new OracleViewDefinition(kvp.Key, kvp.Value));
        }

        public async Task<IEnumerable<OracleDiffResult>> CompareViewDefinitions(Dictionary<string, string> devViews, Dictionary<string, string> qaViews)
        {
            var differences = new List<OracleDiffResult>();

            foreach (var viewName in devViews.Keys)
            {
                if (qaViews.ContainsKey(viewName))
                {
                    var diff = await GetViewDiff(viewName, devViews[viewName], qaViews[viewName]);
                    if (diff.HasDifferences)
                    {
                        _logger.LogInformation($"Difference in view: {viewName}");
                        differences.Add(diff);
                    }
                }
                else
                {
                    var diff = await GetViewDiff(viewName, devViews[viewName], string.Empty);
                    differences.Add(diff);
                    _logger.LogInformation($"View {viewName} is present in DEV but not in QA");
                }
            }

            foreach (var viewName in qaViews.Keys)
            {
                if (!devViews.ContainsKey(viewName))
                {
                    var diff = await GetViewDiff(viewName, string.Empty, qaViews[viewName]);
                    differences.Add(diff);
                    _logger.LogInformation($"View {viewName} is present in QA but not in DEV");
                }
            }

            return differences;
        }

        public async Task<OracleDiffResult> GetViewDiff(string viewName, string oldContent, string newContent)
        {
            return await Task.Run(() =>
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
            });
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
