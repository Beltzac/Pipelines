using Common.Models;

namespace Common.Services
{
    public interface ICommitDataExportService
    {
        Task<List<Commit>> GetCommitDataAsync();
        Task ExportCommitDataAsync();
        Task FetchCommitDataAsync();
    }
}
