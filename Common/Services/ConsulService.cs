using Common.Models;
using Common.Services.Interfaces;
using Common.Utils;
using CSharpDiff.Diffs.Models;
using CSharpDiff.Patches;
using CSharpDiff.Patches.Models;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Common.Services
{
    public class ConsulService : IConsulService
    {
        private const string RegexPatternKey = @"{{\s*key\s*'([^']+)'\s*}}";
        private readonly ILogger<ConsulService> _logger;

        private readonly IConfigurationService _configService;
        private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

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

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                _logger.LogWarning("Resposta vazia recebida do endpoint do agente Consul");
                return string.Empty;
            }

            var json = JObject.Parse(responseBody);
            return json["Config"]?["Datacenter"]?.ToString() ?? string.Empty;
        }

        public async Task<Dictionary<string, ConsulKeyValue>> GetConsulKeyValues(ConsulEnvironment consulEnv)
        {
            var keyValues = await FetchConsulKV(consulEnv);
            if (keyValues == null || keyValues.Count == 0)
            {
                _logger.LogWarning("No key-values found in Consul.");
                return new Dictionary<string, ConsulKeyValue>();
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
                    Key = keyValue.Key,
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

            var regex = new Regex(RegexPatternKey);
            if (regex.IsMatch(strInput))
            {
                return false; // Existem chaves que não foram processadas
            }

            var isJson = key.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            var looksLikeJson = strInput.Trim().StartsWith("{");
            var looksLikeArray = strInput.Trim().StartsWith("[");
            var looksLikeProperty = strInput.Trim().StartsWith("\"") && strInput.Contains(':');

            if (looksLikeJson)
            {
                strInput = $"[{strInput}]"; // Pra testar se objetos soltos também podem ser um array
            }

            if (looksLikeProperty)
            {
                strInput = $"{{{strInput}}}"; // Wrap property in object
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

            var isBasicAuth = IsBasicAuth(strInput);
            if (isBasicAuth)
            {
                return true;
            }

            var isSimpleString = IsSimpleString(strInput);
            if (isSimpleString)
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

                return decodedValue.Contains(':');
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool IsSimpleString(string value)
        {
            // No whitespace, line breaks, or special characters that might indicate a complex value
            return !value.Contains(' ') &&
                   !value.Contains('\n') &&
                   !value.Contains('{') &&
                   !value.Contains('}') &&
                   !value.Contains(':');
        }

        public bool IsPath(string value)
        {
            char[] invalidPathChars = Path.GetInvalidPathChars();
            return (value.Contains('\\') || value.Contains('/')) && !value.Any(c => invalidPathChars.Contains(c));
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
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                var obj = JToken.Parse(value);
                return true;
            }
            catch (JsonReaderException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error parsing JSON");
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
            var regex = new Regex(RegexPatternKey);
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
                _logger.LogInformation("Download concluído com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Erro: {Message}", ex.Message);
            }
        }

        async Task<Dictionary<string, string>> FetchConsulKV(ConsulEnvironment consulEnv)
        {
            try
            {
                return await FetchConsulKVBatch(consulEnv);
            }
            catch (Exception)
            {
                return await FetchConsulKVSequential(consulEnv);
            }
        }

        async Task<Dictionary<string, string>> FetchConsulKVBatch(ConsulEnvironment consulEnv)
        {
            Dictionary<string, string> keyValues = new Dictionary<string, string>();

            var responseBody = await consulEnv.ConsulUrl
                .AppendPathSegment("v1")
                .AppendPathSegment("kv")
                .SetQueryParams(new { recurse = "" })
                .WithHeader("X-Consul-Token", consulEnv.ConsulToken)
                .GetStringAsync();

            foreach(var keyDetail in JArray.Parse(responseBody))
            {
                string keyy = keyDetail["Key"].ToString();
                string value = keyDetail["Value"]?.ToString() ?? string.Empty;
                byte[] valueBytes = Convert.FromBase64String(value);
                string decodedValue = Encoding.UTF8.GetString(valueBytes);
                keyValues[keyy] = decodedValue;
            }

            return keyValues;
        }

        async Task<Dictionary<string, string>> FetchConsulKVSequential(ConsulEnvironment consulEnv)
        {
            var keyValues = new Dictionary<string, string>();

            // Fetch all keys at the root level

            var keysResponse = await consulEnv.ConsulUrl
                .AppendPathSegment("v1")
                .AppendPathSegment("kv/") // Precisa da barra no final
                .SetQueryParam("keys")
                .WithHeader("X-Consul-Token", consulEnv.ConsulToken)
                .GetStringAsync();

            if (string.IsNullOrEmpty(keysResponse))
            {
                return keyValues;
            }

            var keys = JArray.Parse(keysResponse);

            foreach (var key in keys)
            {
                try
                {
                    var keyDetailResponse = await consulEnv.ConsulUrl
                        .AppendPathSegment("v1")
                        .AppendPathSegment("kv")
                        .AppendPathSegment(key.ToString())
                        .WithHeader("X-Consul-Token", consulEnv.ConsulToken)
                        .GetStringAsync();

                    if (!string.IsNullOrEmpty(keyDetailResponse))
                    {
                        var keyDetail = JArray.Parse(keyDetailResponse);
                        if (keyDetail.Count > 0)
                        {
                            string keyy = keyDetail[0]["Key"]?.ToString() ?? string.Empty;
                            string value = keyDetail[0]["Value"]?.ToString() ?? string.Empty;

                            byte[] valueBytes = Convert.FromBase64String(value);
                            string decodedValue = Encoding.UTF8.GetString(valueBytes);
                            keyValues[keyy] = decodedValue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error fetching key {Key}", key);
                    continue;
                }
            }

            return keyValues;
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
                    await SaveKvToFileAsync(pathNormal, kv.Key, kv.Value.Value);
                    await SaveKvToFileAsync(pathRecursive, kv.Key, kv.Value.ValueRecursive);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Error: {Message}", ex.Message);
                }
            }
        }

        public async Task SaveKvToFileAsync(string folderPath, string key, string value)
        {
            try
            {
                // Replace "/" with "\" for Windows paths and ensure it does not end with a backslash

                string filePath = JoinPathKey(folderPath, key);
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

                await File.WriteAllTextAsync(filePath, value);

                _logger.LogInformation("Salvo: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar: {FilePath} {Key} {Value}", folderPath, key, value);
            }
        }

        public static string JoinPathKey(string folderPath, string key)
        {
            return Path.Combine(folderPath, key.Replace("/", "\\"));
        }

        public async Task OpenInVsCode(ConsulEnvironment env)
        {
            if (!Path.Exists(env.ConsulFolder))
                await DownloadConsulAsync(env);

            OpenFolderUtils.OpenWithVSCode(_logger, _configService, env.ConsulFolder);
        }

        private string Normalize(ConsulKeyValue json, bool recursive)
        {
            if (json == null)
                return string.Empty;

            var value = recursive ? json.ValueRecursive : json.Value;
            if (string.IsNullOrEmpty(value))
                return value;

            if (!json.IsValidJson)
                return value.Trim();

            try
            {
                // Parse once, then serialize again with indentation
                using var doc = JsonDocument.Parse(value);
                // Re-serialize with indentation
            
                return System.Text.Json.JsonSerializer.Serialize(doc.RootElement, _jsonSerializerOptions);
            }
            catch
            {
                // If not valid JSON, return original string
                return value.Trim();
            }
        }


        public ConsulDiffResult GetDiff(string key, ConsulKeyValue oldValue, ConsulKeyValue newValue, bool recursive)
        {
            var keyFormatted = key;

            var value1 = Normalize(oldValue, recursive);
            var value2 = Normalize(newValue, recursive);

            if (value1 == value2)
                return new ConsulDiffResult(key, string.Empty, false);

            var ps = new Patch(new PatchOptions(), new DiffOptions());

            var patchResult = ps.createPatchResult(
                keyFormatted,
                keyFormatted,
                value1,
                value2,
                null,
                null
            );

            if (!patchResult.Hunks.Any())
                return new ConsulDiffResult(key, string.Empty, false);

            var diffString = ps.formatPatch(patchResult);
            return new ConsulDiffResult(key, diffString, true);
        }
    }
}
