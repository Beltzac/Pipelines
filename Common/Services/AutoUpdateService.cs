using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;

namespace Common.Services
{
    public class AutoUpdateService : IAutoUpdateService
    {
        private readonly IHubContext<BuildInfoHub> _hubContext;
        private readonly IConfigurationService _configService;

        private readonly string _repositoryOwner;
        private readonly string _repositoryName;
        private readonly string _userAgent;

        public AutoUpdateService(IHubContext<BuildInfoHub> hubContext, IConfigurationService configService)
        {
            _hubContext = hubContext;
            _configService = configService;

            var config = configService.GetConfig();
            _repositoryOwner = config.RepositoryOwner;
            _repositoryName = config.RepositoryName;
            _userAgent = config.UserAgent;
        }

        /// <summary>
        /// Checks for updates and performs the update if available.
        /// </summary>
        public async Task CheckForUpdatesAsync()
        {
            // Get current version
            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine("Current version: " + currentVersion);

            // GitHub API URL for the latest release
            string latestReleaseUrl = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/releases/latest";

            using (HttpClient client = new HttpClient())
            {
                // GitHub API requires a User-Agent header
                client.DefaultRequestHeaders.Add("User-Agent", _userAgent);

                try
                {
                    // Fetch the latest release information
                    HttpResponseMessage response = await client.GetAsync(latestReleaseUrl);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();

                    // Deserialize JSON response
                    Release latestRelease = JsonConvert.DeserializeObject<Release>(responseBody);

                    string latestVersionString = latestRelease.tag_name; // e.g., "0.0.14"
                    Version latestVersion = ParseVersion(latestVersionString);

                    Console.WriteLine("Latest version: " + latestVersion);

                    // Compare versions
                    if (latestVersion > currentVersion)
                    {
                        Console.WriteLine("An update is available.");
                        //await DownloadAndInstallAsync(latestRelease);
                        await _hubContext.Clients.All.SendAsync("Msg", latestVersion);
                    }
                    else
                    {
                        Console.WriteLine("You are using the latest version.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error checking for updates: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Downloads the installer and runs it.
        /// </summary>
        /// <param name="latestRelease">The latest release information from GitHub.</param>
        public async Task DownloadAndInstallAsync(Release latestRelease, Action<int> progressCallback)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", _userAgent);

                // Find the installer asset
                foreach (Asset asset in latestRelease.assets)
                {
                    if (asset.name.EndsWith(".exe"))
                    {
                        string installerUrl = asset.browser_download_url;
                        string installerFileName = asset.name;

                        Console.WriteLine("Downloading installer...");

                        // Download the installer
                        using (var response = await client.GetAsync(installerUrl, HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            var totalBytes = response.Content.Headers.ContentLength ?? 1;
                            var downloadedBytes = 0;

                            using (var contentStream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new System.IO.FileStream(installerFileName, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, 8192, true))
                            {
                                var buffer = new byte[8192];
                                int bytesRead;
                                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    downloadedBytes += bytesRead;
                                    int progress = (int)((double)downloadedBytes / totalBytes * 100);
                                    progressCallback(progress);
                                }
                            }
                        }

                        // Save to file
                        System.IO.File.WriteAllBytes(installerFileName, installerData);

                        Console.WriteLine("Installer downloaded to " + installerFileName);

                        // Start the installer process
                        System.Diagnostics.Process.Start(installerFileName);

                        // Optionally, exit the application
                        Environment.Exit(0);

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Parses the version string from GitHub release tag.
        /// </summary>
        /// <param name="versionString">The version string to parse.</param>
        /// <returns>A Version object.</returns>
        private Version ParseVersion(string versionString)
        {
            // Remove any leading 'v' or 'V'
            if (versionString.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                versionString = versionString.Substring(1);
            }

            Version version;
            if (Version.TryParse(versionString, out version))
            {
                return version;
            }
            else
            {
                // Manually parse the version string
                string[] parts = versionString.Split('.');
                int major = 0, minor = 0, build = 0, revision = 0;

                if (parts.Length > 0) int.TryParse(parts[0], out major);
                if (parts.Length > 1) int.TryParse(parts[1], out minor);
                if (parts.Length > 2) int.TryParse(parts[2], out build);
                if (parts.Length > 3) int.TryParse(parts[3], out revision);

                return new Version(major, minor, build, revision);
            }
        }

        // Classes to deserialize JSON response
        public class Release
        {
            public string tag_name { get; set; }
            public List<Asset> assets { get; set; }
        }

        public class Asset
        {
            public string name { get; set; }
            public string browser_download_url { get; set; }
        }
    }
}
