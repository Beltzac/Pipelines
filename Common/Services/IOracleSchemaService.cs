using System.Collections.Generic;
using System.Threading.Tasks;
using CSharpDiff.Patches.Models;

namespace Common.Services
{
    public interface IOracleSchemaService
    {
        Task<IEnumerable<string>> GetAllKeysAsync();
        Task<Dictionary<string, string>> GetSourceKeyValuesAsync();
        Task<Dictionary<string, string>> GetTargetKeyValuesAsync();
        Task<Dictionary<string, string>> GetDifferencesAsync();
        Dictionary<string, string> Compare(string sourceEnvName, string targetEnvName);
        Task<bool> TestConnectionAsync(string connectionString, string schema);
        Task<string> GetViewDefinitionAsync(string connectionString, string schema, string viewName);
        Task<IEnumerable<string>> GetViewDefinitionsAsync(string connectionString, string schema);
        PatchResult GetDiff(string view, string old, string newString);
        string Format(PatchResult diff);
    }
}