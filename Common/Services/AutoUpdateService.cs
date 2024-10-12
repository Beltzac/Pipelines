using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;

namespace Common.Services
{
    public class AutoUpdateService : IAutoUpdateService
    {
        private readonly string _repositoryOwner;
        private readonly string _repositoryName;
        private readonly string _userAgent;
        private readonly string _accessToken;

        public AutoUpdateService(IConfigurationService configService)
        {
            var config = configService.GetConfig();
            _repositoryOwner = config.RepositoryOwner;
            _repositoryName = config.RepositoryName;
            _userAgent = config.UserAgent;
            _accessToken = config.AccessToken; // Added this line
        }

        /// <summary>
        /// Checks for updates and performs the update if available.
        /// </summary>
        public async Task<Release> CheckForUpdatesAsync()
        {
            // Get current version
            // check if it is dev or release


            Version currentVersion = Version.Parse("0.0.0");            
            bool isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

            if (!isDevelopment)
            {
                currentVersion = Assembly.GetEntryAssembly().GetName().Version;
            }
                
            Console.WriteLine("Current version: " + currentVersion);

            // GitHub API URL for the latest release
            string latestReleaseUrl = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/releases/latest";

            using (HttpClient client = new HttpClient())
            {
                // GitHub API requires a User-Agent header
                client.DefaultRequestHeaders.Add("User-Agent", _userAgent);

                // Add the Authorization header with the access token
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", _accessToken);

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
                        return latestRelease;
                    }
                    else
                    {
                        Console.WriteLine("You are using the latest version.");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error checking for updates: " + ex.Message);
                }
            }

            return null;
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

                // Add the Authorization header with the access token
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", _accessToken);

                // Add the Accept header to get the binary content
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                // Find the installer asset
                foreach (Asset asset in latestRelease.assets)
                {
                    if (asset.name.EndsWith(".exe"))
                    {
                        string installerUrl = asset.url; // Use asset.url
                        string installerFileName = asset.name;

                        // Get the temp folder path
                        string tempFolder = Path.GetTempPath();
                        string installerFilePath = Path.Combine(tempFolder, installerFileName);

                        Console.WriteLine("Downloading installer to temp folder...");

                        // Download the installer
                        using (var response = await client.GetAsync(installerUrl, HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            var totalBytes = response.Content.Headers.ContentLength ?? 1;
                            var downloadedBytes = 0;

                            using (var contentStream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(installerFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, false))
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

                        Console.WriteLine("Installer downloaded to " + installerFilePath);

                        System.Diagnostics.Process.Start(installerFilePath);

                        ElectronNET.API.Electron.App.Exit();

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
            public string url { get; set; }
        }
    }
}
