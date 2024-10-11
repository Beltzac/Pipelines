using Common.Models;

namespace Common.Services
{
    public interface IConsulService
    {
        Task DownloadConsul();
        Task<List<string>> GetConsulKeys();
        Task<Dictionary<string, ConsulKeyValue>> GetConsulKeyValues();
        Task UpdateConsulKeyValue(string key, string value);
    }
}
