using Common.Models;
using Common.Utils;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
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

        public async Task UpdateConsulKeyValue(ConsulEnvironment consulEnv, string key, string value)
        {
            var response = await consulEnv.ConsulUrl
                .AppendPathSegment("v1")
                .AppendPathSegment("kv")
                .AppendPathSegment(key)
                .WithHeader("X-Consul-Token", consulEnv.ConsulToken)
                .PutStringAsync(value);

            _logger.LogInformation("Updated key: {Key}", key);
        }

        private async Task<string> GetDatacenterAsync(ConsulEnvironment consulEnv)
        {
            var responseBody = await consulEnv.ConsulUrl
                .AppendPathSegment("v1")
                .AppendPathSegment("agent")
                .AppendPathSegment("self")
                .WithHeader("X-Consul-Token", consulEnv.ConsulToken)
                .GetStringAsync();

            var json = JObject.Parse(responseBody);
            return json["Config"]["Datacenter"].ToString();
        }

        public async Task<Dictionary<string, ConsulKeyValue>> GetConsulKeyValues(ConsulEnvironment consulEnv)
        {
            var kvData = await FetchConsulKV(consulEnv);

            Dictionary<string, string> keyValues = new Dictionary<string, string>();
            foreach (var kv in kvData)
            {
                string key = kv["Key"].ToString();
                string value = kv["Value"]?.ToString() ?? string.Empty;
                byte[] valueBytes = Convert.FromBase64String(value);
                string decodedValue = System.Text.Encoding.UTF8.GetString(valueBytes);
                keyValues[key] = decodedValue;
            }

            Dictionary<string, ConsulKeyValue> keyValuesWithJson = new Dictionary<string, ConsulKeyValue>();
            string datacenter = await GetDatacenterAsync(consulEnv);

            foreach (var keyValue in keyValues)
            {
                string value = keyValue.Value;
                string url = consulEnv.ConsulUrl
                    .AppendPathSegment("ui")
                    .AppendPathSegment(datacenter)
                    .AppendPathSegment("kv")
                    .AppendPathSegment(keyValue.Key)
                    .AppendPathSegment("edit");

                var recursiveValue = ResolveRecursiveValues(value, keyValues);
                bool isValidJson = IsValidFormated(keyValue.Key, recursiveValue);

                keyValuesWithJson[keyValue.Key] = new ConsulKeyValue
                {
                    Value = value,
                    ValueRecursive = recursiveValue,
                    IsValidJson = isValidJson,
                    Url = url
                };
            }

            return keyValuesWithJson;
        }

        public bool IsValidFormated(string key, string strInput)
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
            var looksLikeJson = strInput.Trim().StartsWith("{");
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

            var isDateTime = IsDateTime(strInput);
            if (isDateTime)
            {
                return true;
            }

            var isConnectionString = IsConnectionString(strInput);
            if (isConnectionString)
            {
                return true;
            }

            var isPath = IsPath(strInput);
            if (isPath)
            {
                return true;
            }

            var isSimpleString = IsSimpleString(strInput);
            if (isSimpleString)
            {
                return true;
            }

            var isBasicAuth = IsBasicAuth(strInput);
            if (isBasicAuth)
            {
                return true;
            }

            return false;
        }

        public bool IsBasicAuth(string value)
        {
            // Validate if the value is a Basic Auth header
            // And if it is base64 encoded
            // with : separator

            if (!value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                var base64Value = value.Substring("Basic ".Length);

                var decodedValue = Encoding.UTF8.GetString(Convert.FromBase64String(base64Value));

                return decodedValue.Contains(":");
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool IsSimpleString(string value)
        {
            // No whitespace, no line breaks
            return !value.Contains(" ") && !value.Contains("\n");
        }

        public bool IsPath(string value)
        {
            char[] invalidPathChars = Path.GetInvalidPathChars();
            return (value.Contains("\\") || value.Contains("/")) && !value.Any(c => invalidPathChars.Contains(c));
        }

        public bool IsConnectionString(string value)
        {
            return value.Contains("Data Source=") || value.Contains("Database=") || value.Contains("mongodb://");
        }

        public bool IsDateTime(string value)
        {
            // 09/18/2023 13:16:28 não é considerado uma data válida (foma americana)
            return DateTime.TryParse(value, out _);
        }

        public bool IsJson(string value)
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

        public bool IsNumeric(string text)
        {
            return double.TryParse(text, out _);
        }

        public bool IsValidURL(string URL)
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

        public async Task DownloadConsulAsync(ConsulEnvironment consulEnv)
        {
            try
            {
                var consulData = await GetConsulKeyValues(consulEnv);
                await SaveKVToFiles(consulEnv, consulData);
                _logger.LogInformation("Download completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Error: {Message}", ex.Message);
            }
        }

        async Task<JArray> FetchConsulKV(ConsulEnvironment consulEnv)
        {
            var responseBody = await consulEnv.ConsulUrl
                .AppendPathSegment("v1")
                .AppendPathSegment("kv")
                .SetQueryParams(new { recurse = "" })
                .WithHeader("X-Consul-Token", consulEnv.ConsulToken)
                .GetStringAsync();

            return JArray.Parse(responseBody);
        }

        async Task SaveKVToFiles(ConsulEnvironment consulEnv, Dictionary<string, ConsulKeyValue> consulData)
        {

            string folderPath = consulEnv.ConsulFolder;

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            foreach (var kv in consulData)
            {
                try
                {
                    var pathNormal = Path.Combine(folderPath, "original");
                    var pathRecursive = Path.Combine(folderPath, "recursive");
                    SaveKvToFile(pathNormal, kv.Key, kv.Value.Value);
                    SaveKvToFile(pathRecursive, kv.Key, kv.Value.ValueRecursive);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Error: {Message}", ex.Message);
                }
            }
        }

        public void SaveKvToFile(string folderPath, string key, string value)
        {
            // Replace "/" with "\" for Windows paths and ensure it does not end with a backslash

            string filePath = Path.Combine(folderPath, key.Replace("/", "\\"));
            string directory = Path.GetDirectoryName(filePath);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // If the path ends with a slash, treat it as a directory
            if (value == null)
            {
                return;
            }

            File.WriteAllText(filePath, value);

            _logger.LogInformation("Saved: {FilePath}", filePath);
        }

        public async Task OpenInVsCode(ConsulEnvironment env)
        {
            OpenFolderUtils.OpenWithVSCode(_logger, env.ConsulFolder);
        }
    }
}
