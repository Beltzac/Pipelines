using Common.Models;

namespace Common.Services.Interfaces
{
    public interface IOracleSchemaService
    {
        Task<IEnumerable<OracleDiffResult>> Compare(string sourceEnvName, string targetEnvName);
        Task<bool> TestConnectionAsync(string connectionString, string schema);
        Task<OracleViewDefinition> GetViewDefinitionAsync(string connectionString, string schema, string viewName);
        Task<IEnumerable<OracleViewDefinition>> GetViewDefinitionsAsync(string connectionString, string schema);
        Task<OracleDiffResult> GetViewDiffAsync(string viewName, string oldContent, string newContent);
        Task<OracleTablesAndViewsResult> GetTablesAndViewsAsync(string connectionString, string? schema = null, string? search = null, int pageSize = 50, int pageNumber = 1);
        Task<IEnumerable<OracleColumn>> GetTableOrViewColumnsAsync(string connectionString, string schema, string objectName);
        Task<string> GenerateEfCoreMappingClassAsync(string connectionString, string schema, string objectName, string className);
        Task<string> GenerateCSharpClassAsync(string connectionString, string schema, string objectName, string className);
        Task<string> AnalyzeQueryPerformanceAsync(string connectionString, string schema, string sql);
        Task<OracleQueryResult> ExecuteSelectQueryAsync(string connectionString, string sql);
    }
}