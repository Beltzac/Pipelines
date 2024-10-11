using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
                keyValues[key] = decodedValue;
            }

            if (isRecursive)
            {
                foreach (var kv in keyValues)
                {
                    keyValues[kv.Key] = ResolveRecursiveValues(kv.Value, keyValues);
                }
            }

            Dictionary<string, (string Value, bool IsValidJson)> keyValuesWithJson = new Dictionary<string, (string Value, bool IsValidJson)>();

            foreach (var keyValue in keyValues)
            {
                string value = keyValue.Value;
                bool isValidJson = IsValidFormated(keyValue.Key, value);
                keyValuesWithJson[keyValue.Key] = (value, isValidJson);
            }

            return keyValuesWithJson;
        }

        private bool IsValidFormated(string key, string strInput)
        {
            if (string.IsNullOrWhiteSpace(strInput))
            {
                return true;
            }

            var isLock = key.EndsWith(".lock", StringComparison.OrdinalIgnoreCase);
            if (isLock)
            {
                return true;
            }

            var isJson = key.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            var looksLikeJson =  strInput.Trim().StartsWith("{");
            var looksLikeArray = strInput.Trim().StartsWith("[");
            var looksLikeProperty = strInput.Trim().StartsWith("\""); 

            if (looksLikeJson)
            {
                strInput = $"[{strInput}]"; // Pra testar se objetos soltos também podem ser um array
            }

            if (looksLikeProperty)
            {
                strInput = $"{{{strInput}}}"; // Pra testar se props soltas podem montar um obj
            }

            if (isJson || looksLikeJson || looksLikeArray || looksLikeProperty)
            {
                return IsJson(strInput);
            }

            // Check simple numbers
            var isNumber = IsNumeric(strInput);
            if (isNumber)
            {
                return true;
            }

            var isUrl = IsValidURL(strInput);
            if (isUrl)
            {
                return true;
            }

            return false;
        }

        private bool IsJson(string value)
        {
            try
            {
                var obj = JToken.Parse(value);
                return true;
            }
            catch (JsonReaderException)
            {
                return false;
            }
        }

        public static bool IsNumeric(string text)
        {
            double test;
            return double.TryParse(text, out test);
        }

        bool IsValidURL(string URL)
        {
            string Pattern = @"^(?:http(s)?:\/\/)?[\w.-]+(?:\.[\w\.-]+)+[\w\-\._~:/?#[\]@!\$&'\(\)\*\+,;=.]+$";
            Regex Rgx = new Regex(Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return Rgx.IsMatch(URL);
        }

        private string ResolveRecursiveValues(string value, Dictionary<string, string> keyValues)
        {
            var regex = new Regex(@"{{\s*key\s*'([^']+)'\s*}}");
            return regex.Replace(value, match =>
            {
                var referencedKey = match.Groups[1].Value.Trim('/');
                if (keyValues.TryGetValue(referencedKey, out var referencedValue))
                {
                    return ResolveRecursiveValues(referencedValue, keyValues);
                }

                _logger.LogWarning("Key not found: {Key}", referencedKey);

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
