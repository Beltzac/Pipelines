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
            if (pageNumber < 1)
                throw new McpProtocolException("Page number must be greater than 0.", McpErrorCode.InvalidParams);
            if (pageSize < 1 || pageSize > 1000)
                throw new McpProtocolException("Page size must be between 1 and 1000.", McpErrorCode.InvalidParams);

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
                throw new McpProtocolException("Schema cannot be null or empty.", McpErrorCode.InvalidParams);
            if (string.IsNullOrWhiteSpace(objectName))
                throw new McpProtocolException("Object name cannot be null or empty.", McpErrorCode.InvalidParams);

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
                throw new McpProtocolException("Schema cannot be null or empty.", McpErrorCode.InvalidParams);
            if (string.IsNullOrWhiteSpace(viewName))
                throw new McpProtocolException("View name cannot be null or empty.", McpErrorCode.InvalidParams);

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

            // Process environments in parallel with timeout
            var tasks = environments.Select(async env =>
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // 10 second timeout per connection
                    var connectionTest = await oracleSchemaService.TestConnectionAsync(env.ConnectionString);

                    return new OracleEnvironmentInfo
                    {
                        Name = env.Name,
                        IsConnected = connectionTest.IsConnected,
                        IsProduction = env.IsProduction,
                        ConnectionError = connectionTest.ErrorMessage
                    };
                }
                catch (OperationCanceledException)
                {
                    return new OracleEnvironmentInfo
                    {
                        Name = env.Name,
                        IsConnected = false,
                        IsProduction = env.IsProduction,
                        ConnectionError = "Connection test timed out after 10 seconds"
                    };
                }
                catch (Exception ex)
                {
                    return new OracleEnvironmentInfo
                    {
                        Name = env.Name,
                        IsConnected = false,
                        IsProduction = env.IsProduction,
                        ConnectionError = $"Connection test failed: {ex.Message}"
                    };
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.ToList();
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
                throw new McpProtocolException("Schema cannot be null or empty.", McpErrorCode.InvalidParams);
            if (string.IsNullOrWhiteSpace(sql))
                throw new McpProtocolException("SQL query cannot be null or empty.", McpErrorCode.InvalidParams);

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
                throw new McpProtocolException("SQL query cannot be null or empty.", McpErrorCode.InvalidParams);

            // Limit SQL query length to prevent potential DoS
            if (sql.Length > 50000)
                throw new McpProtocolException("SQL query is too long. Maximum length is 50,000 characters.", McpErrorCode.InvalidParams);

            // Remove comments and normalize whitespace for validation
            var normalizedSql = System.Text.RegularExpressions.Regex.Replace(sql, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            normalizedSql = System.Text.RegularExpressions.Regex.Replace(normalizedSql, @"--.*?(\r?\n|$)", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            normalizedSql = normalizedSql.Trim();

            // Basic validation to ensure it's a SELECT query
            if (!normalizedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                !normalizedSql.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            {
                throw new McpProtocolException("Only SELECT and WITH queries are allowed.", McpErrorCode.InvalidParams);
            }

            // Additional security check - prevent certain dangerous keywords (but not in string literals)
            var dangerousKeywords = new[] { "DELETE", "UPDATE", "INSERT", "DROP", "CREATE", "ALTER", "TRUNCATE", "EXEC", "EXECUTE" };
            var upperSql = normalizedSql.ToUpperInvariant();

            // Simple check to avoid false positives with string literals
            var sqlWithoutStrings = System.Text.RegularExpressions.Regex.Replace(upperSql, @"'[^']*'", "''");

            foreach (var keyword in dangerousKeywords)
            {
                // Check for keyword as whole word (not part of another word)
                if (System.Text.RegularExpressions.Regex.IsMatch(sqlWithoutStrings, @"\b" + keyword + @"\b"))
                {
                    throw new McpProtocolException($"Query contains prohibited keyword: {keyword}", McpErrorCode.InvalidParams);
                }
            }

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
            if (string.IsNullOrWhiteSpace(environmentName))
                throw new McpProtocolException("Environment name cannot be null or empty.", McpErrorCode.InvalidParams);

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
                throw new McpProtocolException("Schema cannot be null or empty.", McpErrorCode.InvalidParams);
            if (string.IsNullOrWhiteSpace(searchQuery))
                throw new McpProtocolException("Search query cannot be null or empty.", McpErrorCode.InvalidParams);
            if (searchQuery.Length > 1000)
                throw new McpProtocolException("Search query is too long. Maximum length is 1,000 characters.", McpErrorCode.InvalidParams);
            if (maxResults < 1 || maxResults > 100)
                throw new McpProtocolException("Max results must be between 1 and 100.", McpErrorCode.InvalidParams);

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);

            // Fetch views without search filter - we'll do semantic search on the results
            var viewDefinitions = await oracleSchemaService.GetViewDefinitionsAsync(oracleEnv.ConnectionString, schema, null, maxResults * 10, 1);

            if (!viewDefinitions.Any())
            {
                return new List<OracleViewSearchResult>();
            }

            var allCandidates = new List<(string ViewName, string Line, int LineNumber)>();
            const int maxLinesPerView = 1000; // Limit lines per view for performance

            foreach (var def in viewDefinitions)
            {
                if (string.IsNullOrWhiteSpace(def.Definition))
                    continue;

                var lines = def.Definition.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var linesToProcess = Math.Min(lines.Length, maxLinesPerView);

                for (int i = 0; i < linesToProcess; i++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i]))
                    {
                        var trimmedLine = lines[i].Trim();
                        // Skip very short lines (likely not meaningful for search)
                        if (trimmedLine.Length > 3)
                        {
                            allCandidates.Add((def.Name, trimmedLine, i + 1));
                        }
                    }
                }

                // Limit total candidates for performance
                if (allCandidates.Count > maxResults * 50)
                    break;
            }

            if (!allCandidates.Any())
            {
                return new List<OracleViewSearchResult>();
            }

            try
            {
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
            catch (Exception ex)
            {
                throw new McpProtocolException($"Error performing embedding search: {ex.Message}", McpErrorCode.InternalError);
            }
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
                throw new McpProtocolException("Schema cannot be null or empty.", McpErrorCode.InvalidParams);
            if (string.IsNullOrWhiteSpace(objectName))
                throw new McpProtocolException("Object name cannot be null or empty.", McpErrorCode.InvalidParams);
            if (string.IsNullOrWhiteSpace(objectType))
                throw new McpProtocolException("Object type cannot be null or empty.", McpErrorCode.InvalidParams);

            // Validate object type
            var validTypes = new[] { "TABLE", "VIEW" };
            if (!validTypes.Contains(objectType.ToUpperInvariant()))
            {
                throw new McpProtocolException($"Object type must be one of: {string.Join(", ", validTypes)}", McpErrorCode.InvalidParams);
            }

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);
            return await oracleSchemaService.GetOracleDependenciesAsync(
                oracleEnv.ConnectionString,
                schema,
                objectName,
                objectType.ToUpperInvariant()
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
            if (pageNumber < 1)
                throw new McpProtocolException("Page number must be greater than 0.", McpErrorCode.InvalidParams);
            if (pageSize < 1 || pageSize > 1000)
                throw new McpProtocolException("Page size must be between 1 and 1000.", McpErrorCode.InvalidParams);

            var mongoEnv = GetMongoEnvironment(configurationService, environmentName);
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
                throw new McpProtocolException("Environment cannot be null or empty.", McpErrorCode.InvalidParams);
            if (pageNumber < 1)
                throw new McpProtocolException("Page number must be greater than 0.", McpErrorCode.InvalidParams);
            if (pageSize < 1 || pageSize > 1000)
                throw new McpProtocolException("Page size must be between 1 and 1000.", McpErrorCode.InvalidParams);
            if (startDate.HasValue && endDate.HasValue && startDate > endDate)
                throw new McpProtocolException("Start date cannot be after end date.", McpErrorCode.InvalidParams);

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
                throw new McpProtocolException("SOAP request cannot be null or empty.", McpErrorCode.InvalidParams);

            var server = GetEsbServer(configurationService, serverName);
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
                throw new McpProtocolException("Environment cannot be null or empty.", McpErrorCode.InvalidParams);
            if (pageNumber < 1)
                throw new McpProtocolException("Page number must be greater than 0.", McpErrorCode.InvalidParams);
            if (pageSize < 1 || pageSize > 1000)
                throw new McpProtocolException("Page size must be between 1 and 1000.", McpErrorCode.InvalidParams);
            if (startDate.HasValue && endDate.HasValue && startDate > endDate)
                throw new McpProtocolException("Start date cannot be after end date.", McpErrorCode.InvalidParams);

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
                throw new McpProtocolException("Environment cannot be null or empty.", McpErrorCode.InvalidParams);
            if (startDate.HasValue && endDate.HasValue && startDate > endDate)
                throw new McpProtocolException("Start date cannot be after end date.", McpErrorCode.InvalidParams);

            var filter = new SggQueryFilter { Environment = environment, StartDate = startDate, EndDate = endDate };
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
                throw new McpProtocolException("Schema cannot be null or empty.", McpErrorCode.InvalidParams);
            if (pageNumber < 1)
                throw new McpProtocolException("Page number must be greater than 0.", McpErrorCode.InvalidParams);
            if (pageSize < 1 || pageSize > 1000)
                throw new McpProtocolException("Page size must be between 1 and 1000.", McpErrorCode.InvalidParams);

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);
            return await oracleSchemaService.GetViewDefinitionsAsync(oracleEnv.ConnectionString, schema, search, pageSize, pageNumber);
        }

        [McpServerTool(ReadOnly = true), Description("Test the connection to a specific Oracle environment.")]
        public static async Task<OracleConnectionTestResult> TestOracleConnectionAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            [Description("The name of the Oracle environment to test.")] string environmentName)
        {
            if (string.IsNullOrWhiteSpace(environmentName))
                throw new McpProtocolException("Environment name cannot be null or empty.", McpErrorCode.InvalidParams);

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
                throw new McpProtocolException("Schema cannot be null or empty.", McpErrorCode.InvalidParams);
            if (string.IsNullOrWhiteSpace(viewName))
                throw new McpProtocolException("View name cannot be null or empty.", McpErrorCode.InvalidParams);

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
                throw new McpProtocolException("Schema cannot be null or empty.", McpErrorCode.InvalidParams);
            if (string.IsNullOrWhiteSpace(objectName))
                throw new McpProtocolException("Object name cannot be null or empty.", McpErrorCode.InvalidParams);
            if (string.IsNullOrWhiteSpace(className))
                throw new McpProtocolException("Class name cannot be null or empty.", McpErrorCode.InvalidParams);

            // Basic validation for C# class name - support Unicode identifiers
            if (!System.Text.RegularExpressions.Regex.IsMatch(className, @"^[\p{L}_][\p{L}\p{N}_]*$"))
            {
                throw new McpProtocolException("Class name must be a valid C# identifier.", McpErrorCode.InvalidParams);
            }

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
                throw new McpProtocolException("Schema cannot be null or empty.", McpErrorCode.InvalidParams);
            if (string.IsNullOrWhiteSpace(objectName))
                throw new McpProtocolException("Object name cannot be null or empty.", McpErrorCode.InvalidParams);
            if (string.IsNullOrWhiteSpace(className))
                throw new McpProtocolException("Class name cannot be null or empty.", McpErrorCode.InvalidParams);

            // Basic validation for C# class name - support Unicode identifiers
            if (!System.Text.RegularExpressions.Regex.IsMatch(className, @"^[\p{L}_][\p{L}\p{N}_]*$"))
            {
                throw new McpProtocolException("Class name must be a valid C# identifier.", McpErrorCode.InvalidParams);
            }

            var oracleEnv = GetOracleEnvironment(configurationService, environmentName);
            return await oracleSchemaService.GenerateCSharpClassAsync(oracleEnv.ConnectionString, schema, objectName, className);
        }

        private static void ValidateOracleIdentifier(string value, string parameterName, int maxLength = 128)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new McpProtocolException($"{parameterName} cannot be null or empty.", McpErrorCode.InvalidParams);
            if (value.Length > maxLength)
                throw new McpProtocolException($"{parameterName} is too long. Maximum length is {maxLength} characters.", McpErrorCode.InvalidParams);
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
                throw new McpProtocolException("Environment name cannot be null or empty.", McpErrorCode.InvalidParams);

            var config = configurationService.GetConfig();
            var mongoEnv = config.MongoEnvironments.FirstOrDefault(e => e.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));

            if (mongoEnv == null)
                throw new McpProtocolException($"MongoDB environment '{environmentName}' not found.", McpErrorCode.InvalidParams);

            return mongoEnv;
        }

        private static EsbServerConfig GetEsbServer(IConfigurationService configurationService, string serverName)
        {
            if (string.IsNullOrWhiteSpace(serverName))
                throw new McpProtocolException("Server name cannot be null or empty.", McpErrorCode.InvalidParams);

            var config = configurationService.GetConfig();
            var server = config.EsbServers.FirstOrDefault(s => s.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));

            if (server == null)
                throw new McpProtocolException($"ESB server '{serverName}' not found.", McpErrorCode.InvalidParams);

            return server;
        }
    }
}
