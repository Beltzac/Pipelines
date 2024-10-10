using Common.Models;

namespace Common.Services
{
    public interface ICommitDataExportService
    {
        Task ExportCommitDataAsync();
        Task FetchCommitDataAsync();
    }
}