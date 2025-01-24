using Common.Models;

namespace Common.Services.Interfaces
{
    public interface ICommitExportService
    {
        Task<List<Commit>> GetRecentCommitsAsync(string username, DateTime? dateFilter = null);
        Task ExportCommitDataAsync();
        Task FetchCommitDataAsync(IProgress<int> progress = null, DateTime? dateFilter = null, CancellationToken cancellationToken = default);
    }
}
