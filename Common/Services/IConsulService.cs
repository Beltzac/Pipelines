namespace Common.Services
{
    public interface IConsulService
    {
        Task DownloadConsul();
        Task<List<string>> GetConsulKeys();
        Task<Dictionary<string, (string Value, bool IsValidJson)>> GetConsulKeyValues(bool isRecursive);
    }
}
