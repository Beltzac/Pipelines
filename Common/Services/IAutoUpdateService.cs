using static Common.Services.AutoUpdateService;

namespace Common.Services
{
    public interface IAutoUpdateService
    {
        Task<AutoUpdateService.Release> CheckForUpdatesAsync();
        Task DownloadAndInstallAsync(Release latestRelease, Action<int> progressCallback);
    }
}
