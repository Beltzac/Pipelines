using Common.Models;

namespace Common.Services
{
    public interface ICommitDataExportService
    {
        Task<List<Commit>> GetRecentCommitsAsync(string username, int limit = 100);
        Task ExportCommitDataAsync();
        Task FetchCommitDataAsync();
    }
}
