using Common.Models;

namespace Common.Services.Interfaces
{

    public interface IConsulService
    {
        Task DownloadConsulAsync(ConsulEnvironment consulEnv);
        Task<Dictionary<string, ConsulKeyValue>> GetConsulKeyValues(ConsulEnvironment consulEnv);
        Task OpenInVsCode(ConsulEnvironment env);
        void SaveKvToFile(string folderPath, string key, string value);
        Task UpdateConsulKeyValue(ConsulEnvironment consulEnv, string key, string value);
        Task<ConsulDiffResult> GetDiff(string key, ConsulKeyValue oldValue, ConsulKeyValue newValue, bool recursive);
    }
}
