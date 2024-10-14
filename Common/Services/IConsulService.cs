using Common.Models;

namespace Common.Services
{
    public interface IConsulService
    {
        Task DownloadConsulAsync(ConsulEnvironment consulEnv);
        Task<List<string>> GetConsulKeys(ConsulEnvironment consulEnv);
        Task<Dictionary<string, ConsulKeyValue>> GetConsulKeyValues(ConsulEnvironment consulEnv);
        Task OpenInVsCode(ConsulEnvironment env);
        Task UpdateConsulKeyValue(ConsulEnvironment consulEnv, string key, string value);
    }
}
