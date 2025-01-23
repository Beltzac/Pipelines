using Common.Models;

namespace Common.Services.Interfaces
{
    public interface ICommitExportService
    {
        Task<List<Commit>> GetRecentCommitsAsync(string username);
        Task ExportCommitDataAsync();
        Task FetchCommitDataAsync(IProgress<int> progress = null);
    }
}
