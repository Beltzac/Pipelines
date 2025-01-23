using static Common.Services.AutoUpdateService;

namespace Common.Services.Interfaces
{
    public interface IAutoUpdateService
    {
        Task<Release> CheckForUpdatesAsync();
        Task DownloadAndInstallAsync(Release latestRelease, Action<int> progressCallback);
    }
}
