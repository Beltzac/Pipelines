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
            string environmentName,
            string schema,
            string objectName)
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
            string environmentName)
        {
            var config = configurationService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(e => e.Name.ToLower() == environmentName.ToLower());

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
            string environmentName,
            string schema,
            string sql)
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
            string environmentName,
            string sql)
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
    }
}