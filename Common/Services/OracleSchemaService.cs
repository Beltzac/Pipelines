﻿using CSharpDiff.Diffs.Models;
using CSharpDiff.Patches;
using CSharpDiff.Patches.Models;
using Common.Models;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using SQL.Formatter;
using SQL.Formatter.Language;
using static SQL.Formatter.SqlFormatter;
using Dapper;
using Common.Services.Interfaces;

namespace Common.Services
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
            _formatter = Of(Dialect.PlSql);
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
                using var connection = new OracleConnection(connectionString);
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
            using var connection = new OracleConnection(connectionString);
            var sql = "SELECT TEXT FROM ALL_VIEWS WHERE OWNER = :schema AND VIEW_NAME = :viewName";

            var text = await connection.QueryFirstOrDefaultAsync<string>(
                sql,
                new { schema, viewName },
                commandTimeout: 120
            );

            return new OracleViewDefinition(viewName, text ?? string.Empty);
        }

        public Dictionary<string, string> GetViewDefinitions(string connectionString, string schema)
        {
            using var connection = new OracleConnection(connectionString);
            var sql = "SELECT VIEW_NAME, TEXT FROM ALL_VIEWS WHERE OWNER = :schema";

            var results = connection.Query<(string ViewName, string Text)>(
                sql,
                new { schema },
                commandTimeout: 120
            );

            return results.ToDictionary(
                x => x.ViewName,
                x => x.Text
            );
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
                        _logger.LogInformation($"Diferença na view: {viewName}");
                        differences.Add(diff);
                    }
                }
                else
                {
                    var diff = await GetViewDiff(viewName, devViews[viewName], string.Empty);
                    differences.Add(diff);
                    _logger.LogInformation($"A view {viewName} está presente no DEV, mas não no QA");
                }
            }

            foreach (var viewName in qaViews.Keys)
            {
                if (!devViews.ContainsKey(viewName))
                {
                    var diff = await GetViewDiff(viewName, string.Empty, qaViews[viewName]);
                    differences.Add(diff);
                    _logger.LogInformation($"A view {viewName} está presente no QA, mas não no DEV");
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
