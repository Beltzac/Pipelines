using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace Common.Services
{
    public class ConsulService : IConsulService
    {
        private readonly ILogger<ConsulService> _logger;

        private readonly IConfigurationService _configService;

        public ConsulService(ILogger<ConsulService> logger, IConfigurationService configService)
        {
            var config = _configService.GetConfig();
            string consulUrl = config.ConsulUrl + "/v1/kv/?recurse";
            var kvData = await FetchConsulKV(consulUrl);

            Dictionary<string, (string Value, bool IsValidJson)> keyValues = new Dictionary<string, (string, bool)>();
            foreach (var kv in kvData)
            {
                string key = kv["Key"].ToString();
                string value = kv["Value"]?.ToString() ?? string.Empty;
                byte[] valueBytes = Convert.FromBase64String(value);
                string decodedValue = System.Text.Encoding.UTF8.GetString(valueBytes);

                if (isRecursive)
                {
                    decodedValue = ResolveRecursiveValues(decodedValue, keyValues.ToDictionary(k => k.Key, k => k.Value.Value));
                }

                bool isValidJson = IsValidJson(decodedValue);
                keyValues[key] = (decodedValue, isValidJson);
            }

            return keyValues;
        }

        private bool IsValidJson(string strInput)
        {
            if (string.IsNullOrWhiteSpace(strInput)) return false;
            strInput = strInput.Trim();
            if ((strInput.StartsWith("{") && strInput.EndsWith("}")) || // For object
                (strInput.StartsWith("[") && strInput.EndsWith("]")))   // For array
            {
                try
                {
                    var obj = JToken.Parse(strInput);
                    return true;
                }
                catch (JsonReaderException)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
            _logger = logger;
            _configService = configService;
        }

        public async Task<Dictionary<string, (string Value, bool IsValidJson)>> GetConsulKeyValues(bool isRecursive)
        {
            var config = _configService.GetConfig();
            string consulUrl = config.ConsulUrl + "/v1/kv/?recurse";
            var kvData = await FetchConsulKV(consulUrl);

            Dictionary<string, string> keyValues = new Dictionary<string, string>();
            foreach (var kv in kvData)
            {
                string key = kv["Key"].ToString();
                string value = kv["Value"]?.ToString() ?? string.Empty;
                byte[] valueBytes = Convert.FromBase64String(value);
                string decodedValue = System.Text.Encoding.UTF8.GetString(valueBytes);

                if (isRecursive)
                {
                    decodedValue = ResolveRecursiveValues(decodedValue, keyValues);
                }

                keyValues[key] = decodedValue;
            }

            return keyValues;
        }

        private string ResolveRecursiveValues(string value, Dictionary<string, string> keyValues)
        {
            var regex = new Regex(@"{{\s*key\s*'([^']+)'\s*}}");
            return regex.Replace(value, match =>
            {
                var referencedKey = match.Groups[1].Value;
                if (keyValues.TryGetValue(referencedKey, out var referencedValue))
                {
                    return ResolveRecursiveValues(referencedValue, keyValues);
                }
                return match.Value; // Return the original if not found
            });
        }

        public async Task<List<string>> GetConsulKeys()
        {
            var config = _configService.GetConfig();
            string consulUrl = config.ConsulUrl + "/v1/kv/?recurse";
            var kvData = await FetchConsulKV(consulUrl);

            List<string> keys = new List<string>();
            foreach (var kv in kvData)
            {
                string key = kv["Key"].ToString();
                keys.Add(key);
            }

            return keys;
        }

        public async Task DownloadConsul()
        {
            var config = _configService.GetConfig();
            string consulUrl = config.ConsulUrl + "/v1/kv/?recurse";
            string downloadFolder = config.ConsulFolder;

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
