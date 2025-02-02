using Common.Services.Interfaces;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;

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
            _accessToken = config.AccessToken;
        }

        /// <summary>
        /// Obtém a versão atual da aplicação.
        /// </summary>
        public static Version GetCurrentVersion()
        {
#if DEBUG
            // Return a default version when in debug mode.
            return Version.Parse("0.0.0.0");
#else
    // Return the version from your Git tag (or similar) in release mode.
    return Version.Parse(ThisAssembly.Git.BaseTag);
#endif
        }


        /// <summary>
        /// Verifica atualizações e realiza a atualização se disponível.
        /// </summary>
        public async Task<Release> CheckForUpdatesAsync()
        {
            // Obtém a versão atual
            // verifica se é dev ou release


            Version currentVersion = GetCurrentVersion();

            Console.WriteLine("Versão atual: " + currentVersion);

            // URL da API do GitHub para a última versão
            string latestReleaseUrl = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/releases/latest";

            using (HttpClient client = new HttpClient())
            {
                // A API do GitHub requer um cabeçalho User-Agent
                client.DefaultRequestHeaders.Add("User-Agent", _userAgent);

                // Adiciona o cabeçalho de Autorização com o token de acesso
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", _accessToken);

                try
                {
                    // Busca as informações da última versão
                    HttpResponseMessage response = await client.GetAsync(latestReleaseUrl);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();

                    // Desserializa a resposta JSON
                    Release latestRelease = JsonConvert.DeserializeObject<Release>(responseBody);

                    string latestVersionString = latestRelease.tag_name; // e.g., "0.0.14"
                    Version latestVersion = ParseVersion(latestVersionString);

                    Console.WriteLine("Última versão: " + latestVersion);

                    // Compara as versões
                    if (latestVersion > currentVersion)
                    {
                        Console.WriteLine("Uma atualização está disponível.");
                        return latestRelease;
                    }
                    else
                    {
                        Console.WriteLine("Você está usando a última versão.");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Erro ao verificar atualizações: " + ex.Message);
                }
            }

            return null;
        }
        public async Task DownloadAndInstallAsync(Release latestRelease, Action<int> progressCallback)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", _userAgent);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", _accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                // Find the update asset containing the zip with new files.
                foreach (Asset asset in latestRelease.assets)
                {
                    if (asset.name.StartsWith("TugboatCaptainsPlayground") && asset.name.EndsWith(".zip"))
                    {
                        string updateUrl = asset.url;
                        string updateZipName = asset.name;

                        // Use the system temporary folder.
                        string tempFolder = Path.GetTempPath();
                        string updateZipPath = Path.Combine(tempFolder, updateZipName);

                        Console.WriteLine("Downloading update zip to temporary folder...");

                        using (var response = await client.GetAsync(updateUrl, HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            var totalBytes = response.Content.Headers.ContentLength ?? 1;
                            var downloadedBytes = 0;
                            using (var contentStream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(updateZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, false))
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

                        Console.WriteLine("Update zip downloaded to: " + updateZipPath);

                        // Extract the update zip into a dedicated update folder.
                        string updateFolder = Path.Combine(tempFolder, Path.GetFileNameWithoutExtension(updateZipName));
                        if (Directory.Exists(updateFolder))
                        {
                            Directory.Delete(updateFolder, true);
                        }
                        Directory.CreateDirectory(updateFolder);

                        Console.WriteLine("Extracting update zip to: " + updateFolder);
                        ZipFile.ExtractToDirectory(updateZipPath, updateFolder);
                        Console.WriteLine("Extraction complete.");

                        // Determine the current application's folder.

                        // Determine the current application's folder from the process main module.
                        string currentExePath = Process.GetCurrentProcess().MainModule.FileName;
                        string appFolder = Path.GetDirectoryName(currentExePath);

                        // Create a temporary batch file that will wait for the current process to exit,
                        // then copy the update files over the application folder, and finally restart the app.
                        string updaterBatchPath = Path.Combine(tempFolder, "update.bat");
                        string batchContent = $@"
    @echo off
    REM Wait for the current application to exit.
    timeout /t 5 /nobreak > NUL
    echo Copying update files...
    xcopy /E /Y /I ""{updateFolder}\*"" ""{appFolder}""
    echo Update complete. Restarting application...
    start """" ""{Path.Combine(appFolder, "TugboatCaptainsPlayground.exe")}""
    ";
                        File.WriteAllText(updaterBatchPath, batchContent);

                        Console.WriteLine("Launching updater batch file: " + updaterBatchPath);
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = updaterBatchPath,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });

                        // Exit the current application so that the updater can replace locked files.
                        Environment.Exit(0);
                        break;
                    }
                }
            }
        }


        /// <summary>
        /// Analisa a string de versão do tag da release do GitHub.
        /// </summary>
        /// <param name="versionString">A string de versão a ser analisada.</param>
        /// <returns>Um objeto Version.</returns>
        private Version ParseVersion(string versionString)
        {
            // Remove qualquer 'v' ou 'V' inicial
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
                // Analisa manualmente a string de versão
                string[] parts = versionString.Split('.');
                int major = 0, minor = 0, build = 0, revision = 0;

                if (parts.Length > 0) int.TryParse(parts[0], out major);
                if (parts.Length > 1) int.TryParse(parts[1], out minor);
                if (parts.Length > 2) int.TryParse(parts[2], out build);
                if (parts.Length > 3) int.TryParse(parts[3], out revision);

                return new Version(major, minor, build, revision);
            }
        }

        // Classes para desserializar a resposta JSON
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
