using Common.Services.Interfaces;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;

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
            bool isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            return isDevelopment ? Version.Parse("0.0.0.0") : Assembly.GetEntryAssembly().GetName().Version;
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

        /// <summary>
        /// Baixa o instalador e o executa.
        /// </summary>
        /// <param name="latestRelease">As informações da última versão do GitHub.</param>
        public async Task DownloadAndInstallAsync(Release latestRelease, Action<int> progressCallback)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", _userAgent);

                // Adiciona o cabeçalho de Autorização com o token de acesso
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", _accessToken);

                // Adiciona o cabeçalho Accept para obter o conteúdo binário
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                // Encontra o asset do instalador
                foreach (Asset asset in latestRelease.assets)
                {
                    if (asset.name.EndsWith(".exe"))
                    {
                        string installerUrl = asset.url; // Use asset.url
                        string installerFileName = asset.name;

                        // Obtém o caminho da pasta temporária
                        string tempFolder = Path.GetTempPath();
                        string installerFilePath = Path.Combine(tempFolder, installerFileName);

                        Console.WriteLine("Baixando instalador para a pasta temporária...");

                        // Baixa o instalador
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

                        Console.WriteLine("Instalador baixado para " + installerFilePath);

                        // Prepara o comando para executar
                        string cmdCommand = $"/C timeout /T 5 /NOBREAK & start \"\" \"{installerFilePath}\"";

                        // Inicia o processo cmd
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = cmdCommand,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        });

                        // Sai da aplicação Electron
                        //ElectronNET.API.Electron.App.Exit();

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
