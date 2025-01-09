using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Models;

namespace Common.Services
{
    public interface IOracleSchemaService
    {
        IEnumerable<OracleDiffResult> Compare(string sourceEnvName, string targetEnvName);
        Task<bool> TestConnectionAsync(string connectionString, string schema);
        Task<OracleViewDefinition> GetViewDefinitionAsync(string connectionString, string schema, string viewName);
        Task<IEnumerable<OracleViewDefinition>> GetViewDefinitionsAsync(string connectionString, string schema);
        OracleDiffResult GetViewDiff(string viewName, string oldContent, string newContent);
    }
}