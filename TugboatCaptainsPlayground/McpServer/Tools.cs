using Common.Models;
using Common.Services.Interfaces;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TugboatCaptainsPlayground.McpServer
{
    [McpServerToolType]
    public static class Tools
    {
        [McpServerTool(ReadOnly = true), Description("Get a list of all tables and views in a given Oracle schema, with optional search and pagination.")]
        public static async Task<OracleTablesAndViewsResult> GetOracleTablesAndViewsAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The optional schema name to filter tables and views. Use get_oracle_schemas to get available schemas. If null, all schemas are considered.")] string? schema = null,
            [Description("The optional search term to filter tables and views by name.")] string? search = null,
            [Description("The number of items to return per page. Default is 20.")] int pageSize = 20,
            [Description("The page number to retrieve. Default is 1.")] int pageNumber = 1)
        {
            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);

            return await oracleSchemaService.GetTablesAndViewsAsync(
                oracleEnv.ConnectionString,
                schema,
                search,
                pageSize,
                pageNumber);
        }

        [McpServerTool(ReadOnly = true), Description("Get column details (including description/comment) for a specific table or view.")]
        public static async Task<IEnumerable<OracleColumn>> GetOracleTableOrViewColumnsAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The schema name where the table or view is located. Use get_oracle_schemas to get available schemas.")] string schema,
            [Description("The name of the table or view to get column details for. Use get_oracle_tables_and_views to get available tables and views.")] string objectName)
        {
            if (string.IsNullOrWhiteSpace(schema))
                throw new ArgumentException("Schema cannot be null or empty.", nameof(schema));
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be null or empty.", nameof(objectName));

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);

            return await oracleSchemaService.GetTableOrViewColumnsAsync(oracleEnv.ConnectionString, schema, objectName);
        }


        [McpServerTool(ReadOnly = true), Description("Get a single view definition for a given Oracle schema.")]
        public static async Task<OracleViewDefinition> GetOracleViewDefinitionAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The schema name where the view is located. Use get_oracle_schemas to get available schemas.")] string schema,
            [Description("The name of the view to get the definition for. Use get_oracle_tables_and_views to get available views.")] string viewName)
        {
            if (string.IsNullOrWhiteSpace(schema))
                throw new ArgumentException("Schema cannot be null or empty.", nameof(schema));
            if (string.IsNullOrWhiteSpace(viewName))
                throw new ArgumentException("View name cannot be null or empty.", nameof(viewName));

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);
            return await oracleSchemaService.GetViewDefinitionAsync(oracleEnv.ConnectionString, schema, viewName);
        }

        // Environment listing methods
        [McpServerTool(ReadOnly = true), Description("Get a list of all configured Oracle environments with connection status and environment type.")]
        public static async Task<List<OracleEnvironmentInfo>> GetOracleEnvironmentsAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService)
        {
            var config = configurationService.GetConfig();
            var environments = config.OracleEnvironments
                                   .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                                   .ToList();

            var result = new List<OracleEnvironmentInfo>();

            foreach (var env in environments)
            {
                var connectionTest = await oracleSchemaService.TestConnectionAsync(env.ConnectionString);

                result.Add(new OracleEnvironmentInfo
                {
                    Name = env.Name,
                    IsConnected = connectionTest.IsConnected,
                    IsProduction = env.IsProduction,
                    ConnectionError = connectionTest.ErrorMessage
                });
            }

            return result;
        }

        [McpServerTool(ReadOnly = true), Description("Analyze performance of a given Oracle SQL query, returning execution plan and elapsed time.")]
        public static async Task<string> AnalyzeOracleQueryPerformanceAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The schema name to execute the query in. Use get_oracle_schemas to get available schemas.")] string schema,
            [Description("The SQL query to analyze for performance.")] string sql)
        {
            if (string.IsNullOrWhiteSpace(schema))
                throw new ArgumentException("Schema cannot be null or empty.", nameof(schema));
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL query cannot be null or empty.", nameof(sql));

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);
            return await oracleSchemaService.AnalyzeQueryPerformanceAsync(
                oracleEnv.ConnectionString,
                schema,
                sql);
        }

        [McpServerTool(ReadOnly = true), Description("Execute a custom SELECT query against Oracle returning dynamic rows.")]
        public static async Task<OracleQueryResult> ExecuteOracleSelectAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The SELECT SQL query to execute.")] string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL query cannot be null or empty.", nameof(sql));

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);

            return await oracleSchemaService.ExecuteSelectQueryAsync(
                oracleEnv.ConnectionString,
                sql
            );
        }

        [McpServerTool(ReadOnly = true), Description("Get a list of all schemas in a given Oracle environment.")]
        public static async Task<IEnumerable<string>> GetOracleSchemasAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to get schemas from. Use get_oracle_environments to get available environments.")] string environmentName)
        {
            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);
            return await oracleSchemaService.GetSchemasAsync(oracleEnv.ConnectionString);
        }

        [McpServerTool(ReadOnly = true), Description("Search for text inside view definitions")]
        public static async Task<List<OracleViewSearchResult>> SearchInOracleViewDefinitionsAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            SmartComponents.LocalEmbeddings.LocalEmbedder embedder,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The schema name to search view definitions in. Use get_oracle_schemas to get available schemas.")] string schema,
            [Description("The search query to find matching text in view definitions.")] string searchQuery,
            [Description("The maximum number of results to return. Default is 20.")] int maxResults = 20)
        {
            if (string.IsNullOrWhiteSpace(schema))
                throw new ArgumentException("Schema cannot be null or empty.", nameof(schema));
            if (string.IsNullOrWhiteSpace(searchQuery))
                throw new ArgumentException("Search query cannot be null or empty.", nameof(searchQuery));

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);

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

        [McpServerTool(ReadOnly = true), Description("Get object dependencies (tables/views) for a given Oracle object.")]
        public static async Task<IEnumerable<OracleDependency>> GetOracleDependenciesAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to connect to. Use get_oracle_environments to get available environments.")] string environmentName,
            [Description("The schema name where the object is located. Use get_oracle_schemas to get available schemas.")] string schema,
            [Description("The name of the object (table or view).")] string objectName,
            [Description("The type of the object (TABLE or VIEW).")] string objectType)
        {
            if (string.IsNullOrWhiteSpace(schema))
                throw new ArgumentException("Schema cannot be null or empty.", nameof(schema));
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be null or empty.", nameof(objectName));
            if (string.IsNullOrWhiteSpace(objectType))
                throw new ArgumentException("Object type cannot be null or empty.", nameof(objectType));

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);
            return await oracleSchemaService.GetOracleDependenciesAsync(
                oracleEnv.ConnectionString,
                schema,
                objectName,
                objectType
            );
        }

        [McpServerTool(ReadOnly = true), Description("Get a list of all configured MongoDB environments.")]
        public static List<MongoEnvironment> GetMongoEnvironments(IConfigurationService configurationService)
        {
            return configurationService.GetConfig().MongoEnvironments;
        }

        [McpServerTool(ReadOnly = true), Description("Get messages from a specific MongoDB environment (paginated).")]
        public static async Task<PaginatedResult<MongoMessage>> GetMongoMessagesAsync(
            IMongoMessageService mongoMessageService,
            IConfigurationService configurationService,
            [Description("The name of the MongoDB environment. Use get_mongo_environments to get available environments.")] string environmentName,
            [Description("Page number (1-based)")] int pageNumber = 1,
            [Description("Page size")] int pageSize = 50)
        {
            if (string.IsNullOrWhiteSpace(environmentName))
                throw new ArgumentException("Environment name cannot be null or empty.", nameof(environmentName));

            var mongoEnv = configurationService.GetConfig().MongoEnvironments
                .FirstOrDefault(e => e.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));

            if (mongoEnv == null)
                throw new ArgumentException($"MongoDB environment '{environmentName}' not found.");

            return await mongoMessageService.GetMessagesPaginatedAsync(mongoEnv.ConnectionString, pageNumber, pageSize);
        }

        [McpServerTool(ReadOnly = true), Description("Execute an ESB query to search for execution requests.")]
        public static async Task<(List<RequisicaoExecucao> Results, int TotalCount)> ExecuteEsbQueryAsync(
            IEsbService esbService,
            [Description("The ESB environment name.")] string environment,
            [Description("Start date for the search.")] DateTimeOffset? startDate = null,
            [Description("End date for the search.")] DateTimeOffset? endDate = null,
            [Description("Filter by URL.")] string? urlFilter = null,
            [Description("Filter by HTTP Method.")] string? httpMethod = null,
            [Description("Filter by generic text search.")] string? genericText = null,
            [Description("Filter by User ID.")] long? userId = null,
            [Description("Filter by Execution ID.")] int? execucaoId = null,
            [Description("Page size for pagination.")] int pageSize = 10,
            [Description("Page number for pagination.")] int pageNumber = 1,
            [Description("Filter by HTTP Status range (e.g., 200-299).")] string? httpStatusRange = null,
            [Description("Filter by Response Status.")] string? responseStatus = null,
            [Description("Filter by minimum delay in seconds.")] int? minDelaySeconds = null)
        {
            if (string.IsNullOrWhiteSpace(environment))
                throw new ArgumentException("Environment cannot be null or empty.", nameof(environment));

            return await esbService.ExecuteQueryAsync(environment, startDate, endDate, urlFilter, httpMethod, genericText, userId, execucaoId, pageSize, pageNumber, httpStatusRange, responseStatus, minDelaySeconds);
        }

        [McpServerTool(ReadOnly = true), Description("Get ESB sequences for a specific request and server.")]
        public static async Task<string> GetEsbSequencesAsync(
            IEsbService esbService,
            IConfigurationService configurationService,
            [Description("The SOAP request to analyze.")] string soapRequest,
            [Description("The ESB server name. Use get_esb_servers to get available servers.")] string serverName)
        {
            if (string.IsNullOrWhiteSpace(soapRequest))
                throw new ArgumentException("SOAP request cannot be null or empty.", nameof(soapRequest));
            if (string.IsNullOrWhiteSpace(serverName))
                throw new ArgumentException("Server name cannot be null or empty.", nameof(serverName));

            var server = configurationService.GetConfig().EsbServers
                .FirstOrDefault(s => s.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));

            if (server == null)
                throw new ArgumentException($"ESB server '{serverName}' not found.");

            return await esbService.GetEsbSequencesAsync(soapRequest, server);
        }

        [McpServerTool(ReadOnly = true), Description("Get a list of all configured ESB servers.")]
        public static List<EsbServerConfig> GetEsbServers(IConfigurationService configurationService)
        {
            return configurationService.GetConfig().EsbServers;
        }

        [McpServerTool(ReadOnly = true), Description("Execute an SGG query to search for LTDB/LTVC records.")]
        public static async Task<(List<LtdbLtvcRecord> Results, int TotalCount)> ExecuteSggQueryAsync(
            ISggService sggService,
            [Description("The SGG environment name.")] string environment,
            [Description("Start date for the search.")] DateTimeOffset? startDate = null,
            [Description("End date for the search.")] DateTimeOffset? endDate = null,
            [Description("Filter by generic text search.")] string? genericText = null,
            [Description("Filter by Placa.")] string? placa = null,
            [Description("Filter by Motorista.")] string? motorista = null,
            [Description("Filter by Move Type.")] string? moveType = null,
            [Description("Filter by Agendamento ID.")] long? idAgendamento = null,
            [Description("Filter by Status.")] string? status = null,
            [Description("Filter by minimum delay.")] double? minDelay = null,
            [Description("Filter by barcode.")] string? codigoBarras = null,
            [Description("Filter by Request ID.")] string? requestId = null,
            [Description("Page size for pagination.")] int pageSize = 10,
            [Description("Page number for pagination.")] int pageNumber = 1)
        {
            if (string.IsNullOrWhiteSpace(environment))
                throw new ArgumentException("Environment cannot be null or empty.", nameof(environment));

            var filter = new SggQueryFilter
            {
                Environment = environment,
                StartDate = startDate,
                EndDate = endDate,
                GenericText = genericText,
                Placa = placa,
                Motorista = motorista,
                MoveType = moveType,
                IdAgendamento = idAgendamento,
                Status = status,
                MinDelay = minDelay,
                CodigoBarras = codigoBarras,
                RequestId = requestId,
                PageSize = pageSize,
                PageNumber = pageNumber
            };
            return await sggService.ExecuteQueryAsync(filter);
        }

        [McpServerTool(ReadOnly = true), Description("Get SGG delay metrics for the given filter.")]
        public static async Task<List<DelayMetric>> GetSggDelayMetricsAsync(
            ISggService sggService,
            [Description("The SGG environment name.")] string environment,
            [Description("Start date for the search.")] DateTimeOffset? startDate = null,
            [Description("End date for the search.")] DateTimeOffset? endDate = null)
        {
            if (string.IsNullOrWhiteSpace(environment))
                throw new ArgumentException("Environment cannot be null or empty.", nameof(environment));

            var filter = new SggQueryFilter { Environment = environment, StartDate = startDate, EndDate = endDate };
            return await sggService.GetDelayMetricsAsync(filter);
        }
                               return await sggService.GetDelayMetricsAsync(filter);
                           }

        [McpServerTool(ReadOnly = true), Description("Get a list of view definitions for a given Oracle schema.")]
        public static async Task<IEnumerable<OracleViewDefinition>> GetOracleViewDefinitionsAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to connect to.")] string environmentName,
            [Description("The schema name where the views are located.")] string schema,
            [Description("Optional search term to filter views by name.")] string? search = null,
            [Description("Page size for pagination.")] int pageSize = 100,
            [Description("Page number for pagination.")] int pageNumber = 1)
        {
            if (string.IsNullOrWhiteSpace(schema))
                throw new ArgumentException("Schema cannot be null or empty.", nameof(schema));

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);
            return await oracleSchemaService.GetViewDefinitionsAsync(oracleEnv.ConnectionString, schema, search, pageSize, pageNumber);
        }

        [McpServerTool(ReadOnly = true), Description("Test the connection to a specific Oracle environment.")]
        public static async Task<OracleConnectionTestResult> TestOracleConnectionAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to test.")] string environmentName)
        {
            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);
            return await oracleSchemaService.TestConnectionAsync(oracleEnv.ConnectionString);
        }

        [McpServerTool(ReadOnly = true), Description("Compare view definitions across two Oracle environments.")]
        public static async Task<OracleDiffResult> CompareOracleViewDefinitionsAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the source Oracle environment.")] string sourceEnvironmentName,
            [Description("The name of the target Oracle environment.")] string targetEnvironmentName,
            [Description("The schema name.")] string schema,
            [Description("The name of the view to compare.")] string viewName)
        {
            if (string.IsNullOrWhiteSpace(schema))
                throw new ArgumentException("Schema cannot be null or empty.", nameof(schema));
            if (string.IsNullOrWhiteSpace(viewName))
                throw new ArgumentException("View name cannot be null or empty.", nameof(viewName));

            var sourceEnv = GetOracleEnvironment(configurationService, sourceEnvironmentName);
            var targetEnv = GetOracleEnvironment(configurationService, targetEnvironmentName);

            var sourceView = await oracleSchemaService.GetViewDefinitionAsync(sourceEnv.ConnectionString, schema, viewName);
            var targetView = await oracleSchemaService.GetViewDefinitionAsync(targetEnv.ConnectionString, schema, viewName);

            return await oracleSchemaService.GetViewDiffAsync(viewName, sourceView.Definition, targetView.Definition);
        }

        [McpServerTool(ReadOnly = true), Description("Generate an EF Core mapping class for an Oracle table or view.")]
        public static async Task<string> GenerateOracleEfCoreMappingClassAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment.")] string environmentName,
            [Description("The schema name.")] string schema,
            [Description("The name of the table or view.")] string objectName,
            [Description("The desired C# class name.")] string className)
        {
            if (string.IsNullOrWhiteSpace(schema))
                throw new ArgumentException("Schema cannot be null or empty.", nameof(schema));
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be null or empty.", nameof(objectName));
            if (string.IsNullOrWhiteSpace(className))
                throw new ArgumentException("Class name cannot be null or empty.", nameof(className));

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);
            return await oracleSchemaService.GenerateEfCoreMappingClassAsync(oracleEnv.ConnectionString, schema, objectName, className);
        }

        [McpServerTool(ReadOnly = true), Description("Generate a C# POCO class for an Oracle table or view.")]
        public static async Task<string> GenerateOracleCSharpClassAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment.")] string environmentName,
            [Description("The schema name.")] string schema,
            [Description("The name of the table or view.")] string objectName,
            [Description("The desired C# class name.")] string className)
        {
            if (string.IsNullOrWhiteSpace(schema))
                throw new ArgumentException("Schema cannot be null or empty.", nameof(schema));
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be null or empty.", nameof(objectName));
            if (string.IsNullOrWhiteSpace(className))
                throw new ArgumentException("Class name cannot be null or empty.", nameof(className));

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);
            return await oracleSchemaService.GenerateCSharpClassAsync(oracleEnv.ConnectionString, schema, objectName, className);
        }

        private static OracleEnvironment GetOracleEnvironment(IConfigurationService configurationService, string environmentName)
        {
            if (string.IsNullOrWhiteSpace(environmentName))
                throw new McpProtocolException("Environment name cannot be null or empty.", McpErrorCode.InvalidParams);

            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));

            if (oracleEnv == null)
                throw new McpProtocolException($"Oracle environment '{environmentName}' not found.", McpErrorCode.InvalidParams);

            return oracleEnv;
        }

        private static MongoEnvironment GetMongoEnvironment(IConfigurationService configurationService, string environmentName)
        {
            if (string.IsNullOrWhiteSpace(environmentName))
                throw new ArgumentException("Environment name cannot be null or empty.", nameof(environmentName));

            var config = configurationService.GetConfig();
            var mongoEnv = config.MongoEnvironments.FirstOrDefault(e => e.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));

            if (mongoEnv == null)
                throw new ArgumentException($"MongoDB environment '{environmentName}' not found.");

            return mongoEnv;
        }

        private static EsbServerConfig GetEsbServer(IConfigurationService configurationService, string serverName)
        {
            if (string.IsNullOrWhiteSpace(serverName))
                throw new ArgumentException("Server name cannot be null or empty.", nameof(serverName));

            var config = configurationService.GetConfig();
            var server = config.EsbServers.FirstOrDefault(s => s.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));

            if (server == null)
                throw new ArgumentException($"ESB server '{serverName}' not found.");

            return server;
        }
    }
}
