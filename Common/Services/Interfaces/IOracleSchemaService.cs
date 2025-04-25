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
    }
}