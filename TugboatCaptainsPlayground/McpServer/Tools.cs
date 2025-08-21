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
            string environmentName,
            string? schema = null,
            string? search = null,
            int pageSize = 20,
            int pageNumber = 1)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == environmentName);
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

        [McpServerTool, Description("Get column details for a specific table or view.")]
        public static async Task<IEnumerable<OracleColumn>> GetOracleTableOrViewColumnsAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            string environmentName,
            string schema,
            string objectName)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == environmentName);
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
            string environmentName)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == environmentName);
            if (oracleEnv == null)
            {
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");
            }
            return await oracleSchemaService.TestConnectionAsync(oracleEnv.ConnectionString, oracleEnv.Schema);
        }

        [McpServerTool, Description("Get a single view definition for a given Oracle schema.")]
        public static async Task<OracleViewDefinition> GetOracleViewDefinitionAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            string environmentName,
            string schema,
            string viewName)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == environmentName);
            if (oracleEnv == null)
            {
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");
            }
            return await oracleSchemaService.GetViewDefinitionAsync(oracleEnv.ConnectionString, schema, viewName);
        }

        [McpServerTool, Description("Generates an EF Core mapping class for a given Oracle table or view.")]
        public static async Task<string> GenerateOracleEfCoreMappingClassAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            string environmentName,
            string objectName,
            string className)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == environmentName);
            if (oracleEnv == null)
            {
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");
            }
            return await oracleSchemaService.GenerateEfCoreMappingClassAsync(oracleEnv.ConnectionString, oracleEnv.Schema, objectName, className);
        }

        [McpServerTool, Description("Generates a C# class for a given Oracle table or view.")]
        public static async Task<string> GenerateOracleCSharpClassAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            string environmentName,
            string objectName,
            string className)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == environmentName);
            if (oracleEnv == null)
            {
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");
            }
            return await oracleSchemaService.GenerateCSharpClassAsync(oracleEnv.ConnectionString, oracleEnv.Schema, objectName, className);
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

        [McpServerTool, Description("Get a list of all configured MongoDB environments.")]
        public static List<string> GetMongoEnvironments(
            IConfigurationService configurationService)
        {
            var config = configurationService.GetConfig();
            return config.MongoEnvironments
                         .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                         .Select(e => e.Name)
                         .ToList();
        }

        [McpServerTool, Description("Get a list of all configured ESB servers.")]
        public static List<string> GetEsbServers(
            IConfigurationService configurationService)
        {
            var config = configurationService.GetConfig();
            return config.EsbServers
                         .Where(e => !string.IsNullOrWhiteSpace(e.Url))
                         .Select(e => e.Url)
                         .ToList();
        }

        // EsbService methods
        [McpServerTool, Description("Execute a paginated query against ESB execution logs that are on the oracle tables. Optional string parameters can be null.")]
        public static async Task<EsbQueryResult> ExecuteEsbQueryAsync(
            IEsbService esbService,
            IConfigurationService configurationService,
            string environmentName,
            DateTime? startDate,
            DateTime? endDate,
            string? urlFilter,
            string? httpMethod,
            string? genericText,
            long? userId,
            int? execucaoId,
            int pageSize,
            int pageNumber,
            string? httpStatusRange,
            string? responseStatus,
            int? minDelaySeconds)
        {
            var config = configurationService.GetConfig();
            var esbEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == environmentName);
            if (esbEnv == null)
            {
                throw new ArgumentException($"ESB environment '{environmentName}' not found.");
            }

            var (results, totalCount) = await esbService.ExecuteQueryAsync(
                esbEnv.ConnectionString,
                startDate,
                endDate,
                urlFilter,
                httpMethod,
                genericText,
                userId,
                execucaoId,
                pageSize,
                pageNumber,
                httpStatusRange,
                responseStatus,
                minDelaySeconds);

            return new EsbQueryResult { Results = results, TotalCount = totalCount };
        }

        [McpServerTool, Description("Retrieve ESB sequence information.")]
        public static async Task<List<SequenceInfo>> GetEsbSequencesAsync(
            IEsbService esbService,
            IConfigurationService configurationService,
            string environmentName)
        {

            var config = configurationService.GetConfig();
            var esbEnv = config.EsbServers.FirstOrDefault(e => e.Name == environmentName);
            if (esbEnv == null)
            {
                throw new ArgumentException($"ESB environment '{environmentName}' not found.");
            }

            return await esbService.GetSequencesAsync(esbEnv);
        }

        // MongoMessageService methods
        [McpServerTool, Description("Retrieve all messages from a MongoDB collection.")]
        public static async Task<Dictionary<string, MongoMessage>> GetMongoMessagesAsync(
            IMongoMessageService mongoMessageService,
            IConfigurationService configurationService,
            string environmentName)
        {
            var config = configurationService.GetConfig();
            var mongoEnv = config.MongoEnvironments.FirstOrDefault(e => e.Name == environmentName);
            if (mongoEnv == null)
            {
                throw new ArgumentException($"mongo environment '{environmentName}' not found.");
            }

            return await mongoMessageService.GetMessagesAsync(mongoEnv.ConnectionString);
        }

        // SggService methods
        [McpServerTool, Description("Execute a paginated query against SGG tracking data. Optional string parameters can be null.")]
        public static async Task<SggQueryResult> ExecuteSggQueryAsync(
            ISggService sggService,
            IConfigurationService configurationService,
            string environmentName,
            DateTimeOffset? startDate,
            DateTimeOffset? endDate,
            string? genericText,
            string? placa,
            string? motorista,
            string? moveType,
            int? idAgendamento,
            string? status,
            int? minDelay,
            string? codigoBarras,
            string? requestId,
            int pageSize,
            int pageNumber)
        {
            var config = configurationService.GetConfig();
            var sggEnv = config.OracleEnvironments.FirstOrDefault(o => o.Name == environmentName);
            if (sggEnv == null)
            {
                throw new ArgumentException($"SGG environment '{environmentName}' not found.");
            }

            var filter = new SggQueryFilter
            {
                Environment = sggEnv.ConnectionString,
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
            try
            {
                var (results, totalCount) = await sggService.ExecuteQueryAsync(filter);
                return new SggQueryResult { Results = results, TotalCount = totalCount };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error executing SGG query: {ex.Message}", ex);
            }
        }

        [McpServerTool, Description("Get delay metrics for SGG tracking data. Optional string parameters can be null.")]
        public static async Task<List<DelayMetric>> GetSggDelayMetricsAsync(
            ISggService sggService,
            IConfigurationService configurationService,
            string environmentName,
            DateTimeOffset? startDate,
            DateTimeOffset? endDate,
            string? genericText,
            string? placa,
            string? motorista,
            string? moveType,
            int? idAgendamento,
            string? status,
            int? minDelay,
            string? codigoBarras,
            string? requestId)
        {
            var config = configurationService.GetConfig();
            var sggEnv = config.OracleEnvironments.FirstOrDefault(o => o.Name == environmentName);
            if (sggEnv == null)
            {
                throw new ArgumentException($"SGG environment '{environmentName}' not found.");
            }

            var filter = new SggQueryFilter
            {
                Environment = sggEnv.ConnectionString,
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
                RequestId = requestId
            };
            try
            {
                return await sggService.GetDelayMetricsAsync(filter);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting SGG delay metrics: {ex.Message}", ex);
            }
        }
    
        [McpServerTool, Description("Analyze performance of a given Oracle SQL query, returning execution plan and elapsed time.")]
        public static async Task<string> AnalyzeOracleQueryPerformanceAsync(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configurationService,
            string environmentName,
            string schema,
            string sql)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name == environmentName);
            if (oracleEnv == null)
            {
                throw new ArgumentException($"Oracle environment '{environmentName}' not found.");
            }
            return await oracleSchemaService.AnalyzeQueryPerformanceAsync(
                oracleEnv.ConnectionString,
                schema,
                sql);
        }
    }
}