using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Common
{
    public class ConsulService : IConsulService
    {
        private readonly ILogger<ConsulService> _logger;

        public ConsulService(ILogger<ConsulService> logger)
        {
            _logger = logger;
        }

        public async Task DownloadConsul()
        {
            string consulUrl = "https://consul-qa.tcp.com.br/v1/kv/?recurse";
            string downloadFolder = "C:\\ConsulKV"; // Change this to your desired download folder

            if (!Directory.Exists(downloadFolder))
            {
                Directory.CreateDirectory(downloadFolder);
            }

            try
            {
                var kvData = await FetchConsulKV(consulUrl);
                await SaveKVToFiles(kvData, downloadFolder);
                _logger.LogInformation("Download completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Error: {Message}", ex.Message);
            }
        }

        async Task<JArray> FetchConsulKV(string url)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            return JArray.Parse(responseBody);
        }

        async Task SaveKVToFiles(JArray kvData, string folderPath)
        {
            foreach (var kv in kvData)
            {
                try
                {
                    string key = kv["Key"].ToString();
                    string value = kv["Value"]?.ToString() ?? string.Empty;

                    // Replace "/" with "\" for Windows paths and ensure it does not end with a backslash
                    string filePath = Path.Combine(folderPath, key.Replace("/", "\\"));
                    string directory = Path.GetDirectoryName(filePath);

                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // If the path ends with a slash, treat it as a directory
                    if (kv["Value"] == null)
                    {
                        continue;
                    }

                    byte[] valueBytes = Convert.FromBase64String(value);
                    await File.WriteAllBytesAsync(filePath, valueBytes);

                    _logger.LogInformation("Saved: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Error: {Message}", ex.Message);
                }
            }
        }
    }
}
