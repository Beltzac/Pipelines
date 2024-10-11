namespace Common.Services
{
    public interface IConsulService
    {
        Task DownloadConsul();
        Task<List<string>> GetConsulKeys();
    }
}
