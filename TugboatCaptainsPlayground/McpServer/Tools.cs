using Common.Models;
using Common.Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Common.Services;
using System.Linq; // Added for LINQ methods

namespace TugboatCaptainsPlayground.McpServer
{
    [McpServerToolType]
    public static class Tools
    {
        [McpServerTool, Description("Get a list of all tables and views in a given Oracle schema, with optional search and pagination.")]
        public static async Task<OracleTablesAndViewsResult> GetOracleTablesAndViewsAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The optional schema name to filter tables and views. Use get_oracle_schemas to get available schemas. If null, all schemas are considered.")] string? schema = null,
            [Description("The optional search term to filter tables and views by name.")] string? search = null,
            [Description("The number of items to return per page. Default is 20.")] int pageSize = 20,
            [Description("The page number to retrieve. Default is 1.")] int pageNumber = 1)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name.ToLower() == environmentName.ToLower());

            if (oracleEnv == null)
            {
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");
            }

            return await oracleSchemaService.GetTablesAndViewsAsync(
                oracleEnv.ConnectionString,
                schema,
                search,
                pageSize,
                pageNumber);
        }

        [McpServerTool, Description("Get column details (including description/comment) for a specific table or view.")]
        public static async Task<IEnumerable<OracleColumn>> GetOracleTableOrViewColumnsAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The schema name where the table or view is located. Use get_oracle_schemas to get available schemas.")] string schema,
            [Description("The name of the table or view to get column details for. Use get_oracle_tables_and_views to get available tables and views.")] string objectName)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name.ToLower() == environmentName.ToLower());

            if (oracleEnv == null)
            {
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");
            }

            return await oracleSchemaService.GetTableOrViewColumnsAsync(oracleEnv.ConnectionString, schema, objectName);
        }

        [McpServerTool, Description("Test the connection to an Oracle database.")]
        public static async Task<bool> TestOracleConnectionAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to test the connection for. Use get_oracle_environments to get available environments.")] string environmentName)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name.ToLower() == environmentName.ToLower());

            if (oracleEnv == null)
            {
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");
            }

            return await oracleSchemaService.TestConnectionAsync(oracleEnv.ConnectionString);
        }

        [McpServerTool, Description("Get a single view definition for a given Oracle schema.")]
        public static async Task<OracleViewDefinition> GetOracleViewDefinitionAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The schema name where the view is located. Use get_oracle_schemas to get available schemas.")] string schema,
            [Description("The name of the view to get the definition for. Use get_oracle_tables_and_views to get available views.")] string viewName)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name.ToLower() == environmentName.ToLower());

            if (oracleEnv == null)
            {
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");
            }
            return await oracleSchemaService.GetViewDefinitionAsync(oracleEnv.ConnectionString, schema, viewName);
        }

        // Environment listing methods
        [McpServerTool, Description("Get a list of all configured Oracle environments.")]
        public static List<string> GetOracleEnvironments(
            IConfigurationService configurationService)
        {
            var config = configurationService.GetConfig();
            return config.OracleEnvironments
                         .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                         .Select(e => e.Name)
                         .ToList();
        }

        [McpServerTool, Description("Analyze performance of a given Oracle SQL query, returning execution plan and elapsed time.")]
        public static async Task<string> AnalyzeOracleQueryPerformanceAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The schema name to execute the query in. Use get_oracle_schemas to get available schemas.")] string schema,
            [Description("The SQL query to analyze for performance.")] string sql)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name.ToLower() == environmentName.ToLower());
            if (oracleEnv == null)
            {
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");
            }
            return await oracleSchemaService.AnalyzeQueryPerformanceAsync(
                oracleEnv.ConnectionString,
                schema,
                sql);
        }

        [McpServerTool, Description("Execute a custom SELECT query against Oracle returning dynamic rows.")]
        public static async Task<OracleQueryResult> ExecuteOracleSelectAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The SELECT SQL query to execute.")] string sql)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name.ToLower() == environmentName.ToLower());
            if (oracleEnv == null)
            {
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");
            }

            return await oracleSchemaService.ExecuteSelectQueryAsync(
                oracleEnv.ConnectionString,
                sql
            );
        }

        [McpServerTool, Description("Get a list of all schemas in a given Oracle environment.")]
        public static async Task<IEnumerable<string>> GetOracleSchemasAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to get schemas from. Use get_oracle_environments to get available environments.")] string environmentName)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name.ToLower() == environmentName.ToLower());

            if (oracleEnv == null)
            {
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");
            }

            return await oracleSchemaService.GetSchemasAsync(oracleEnv.ConnectionString);
        }

        [McpServerTool, Description("Search for text inside view definitions")]
        public static async Task<List<OracleViewSearchResult>> SearchInOracleViewDefinitionsAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            SmartComponents.LocalEmbeddings.LocalEmbedder embedder,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The schema name to search view definitions in. Use get_oracle_schemas to get available schemas.")] string schema,
            [Description("The search query to find matching text in view definitions.")] string searchQuery,
            [Description("The maximum number of results to return. Default is 20.")] int maxResults = 20)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name.ToLower() == environmentName.ToLower());
            if (oracleEnv == null)
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");

            // Fetch a reasonable number of views (10x maxResults) for embedding search
            var viewDefinitions = await oracleSchemaService.GetViewDefinitionsAsync(oracleEnv.ConnectionString, schema, searchQuery, maxResults * 10, 1);

            var allCandidates = new List<(string ViewName, string Line, int LineNumber)>();

            foreach (var def in viewDefinitions)
            {
                var lines = def.Definition.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    allCandidates.Add((def.Name, lines[i], i + 1));
                }
            }

            var queryVec = embedder.Embed(searchQuery);

            var embeddingPairs = allCandidates
                .Select(c => (Item: c, Embedding: embedder.Embed(c.Line)))
                .ToList();

            var results = SmartComponents.LocalEmbeddings.LocalEmbedder.FindClosestWithScore(queryVec, embeddingPairs, maxResults, 0.6f);

            return results.Select(r => new OracleViewSearchResult
            {
                ViewName = r.Item.ViewName,
                MatchingLines = new List<string> { $"Line {r.Item.LineNumber}: {r.Item.Line}" },
                Similarity = r.Similarity
            }).ToList();
        }
    }
}