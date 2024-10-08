namespace Common.Services
{
    public interface IAutoUpdateService
    {
        Task CheckForUpdatesAsync();
        Task DownloadAndInstallAsync(Release latestRelease);
    }
}