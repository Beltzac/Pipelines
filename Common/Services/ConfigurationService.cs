using Common.Models;
using System.Text.Json;
using System.IO;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configPath;
    private ConfigModel _config;
    private Dictionary<string, string> _preservedKeys = new();

    public ConfigurationService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TugboatCaptainsPlayground",
            "config.properties"))
    {
    }

    // Este construtor é usado para testes
    public ConfigurationService(string configPath)
    {
        _configPath = configPath;
        string appFolder = Path.GetDirectoryName(_configPath);

        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }

        LoadConfig();
    }

    private void LoadConfig()
    {
        if (File.Exists(_configPath))
        {
            var properties = LoadPropertiesFile(_configPath);
            _config = MapPropertiesToConfigModel(properties);

            // Preserve unknown keys
            _preservedKeys = properties
                .Where(kvp => !IsKnownProperty(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return;
        }

        // Try to load from JSON file for retrocompatibility
        var jsonPath = Path.ChangeExtension(_configPath, ".json");
        if (File.Exists(jsonPath))
        {
            string jsonConfig = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _config = JsonSerializer.Deserialize<ConfigModel>(jsonConfig, options) ?? new ConfigModel();
            // Migrate to properties by saving
            SaveConfigAsync().Wait();
            return;
        }

        _config = new ConfigModel
        {
            OrganizationUrl = "https://dev.azure.com/terminal-cp",
            LocalCloneFolder = @"C:\repos",
        };
    }

    private async Task SaveConfigAsync()
    {
        var allProperties = new Dictionary<string, string>();

        // Add known properties
        var knownProperties = MapConfigModelToProperties(_config);
        foreach (var kvp in knownProperties)
        {
            allProperties[kvp.Key] = kvp.Value;
        }

        // Add preserved unknown properties
        foreach (var kvp in _preservedKeys)
        {
            allProperties[kvp.Key] = kvp.Value;
        }

        // Save to file
        var lines = allProperties.Select(kvp => $"{kvp.Key}={kvp.Value}");
        await File.WriteAllLinesAsync(_configPath, lines);
    }

    public ConfigModel GetConfig()
    {
        return _config;
    }

    public async Task SaveConfigAsync(ConfigModel config)
    {
        _config = config;
        await SaveConfigAsync();
    }

    public async Task SaveSavedQueriesAsync(List<SavedQuery> savedQueries)
    {
        _config.SavedQueries = savedQueries;
        await SaveConfigAsync();
    }

    public List<SavedQuery> LoadSavedQueries()
    {
        return _config.SavedQueries ?? new List<SavedQuery>();
    }

    public string ExportConfig()
    {
        return JsonSerializer.Serialize(_config, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    public async Task ImportConfigAsync(string jsonConfig)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var importedConfig = JsonSerializer.Deserialize<ConfigModel>(jsonConfig, options);
            if (importedConfig == null)
                throw new ArgumentException("Formato de configuração inválido");

            ValidateConfig(importedConfig);
            _config = importedConfig;
            await SaveConfigAsync();
        }
        catch (JsonException)
        {
            throw new ArgumentException("Formato JSON inválido");
        }
    }

    private void ValidateConfig(ConfigModel config)
    {
        if (config == null)
            throw new ArgumentException("A configuração não pode ser nula");

        if (string.IsNullOrWhiteSpace(config.OrganizationUrl))
            throw new ArgumentException("A URL da organização é obrigatória");

        if (config.OracleEnvironments != null)
        {
            foreach (var env in config.OracleEnvironments)
            {
                if (string.IsNullOrWhiteSpace(env.Name))
                    throw new ArgumentException("O nome do ambiente Oracle é obrigatório");
                if (string.IsNullOrWhiteSpace(env.ConnectionString))
                    throw new ArgumentException("A string de conexão Oracle é obrigatória");
                if (string.IsNullOrWhiteSpace(env.Schema))
                    throw new ArgumentException("O esquema Oracle é obrigatório");
            }
        }

        if (config.ConsulEnvironments != null)
        {
            foreach (var env in config.ConsulEnvironments)
            {
                if (string.IsNullOrWhiteSpace(env.Name))
                    throw new ArgumentException("O nome do ambiente Consul é obrigatório");
                if (string.IsNullOrWhiteSpace(env.ConsulUrl))
                    throw new ArgumentException("A URL do Consul é obrigatória");
            }
        }
    }

    private Dictionary<string, string> LoadPropertiesFile(string path)
    {
        var properties = new Dictionary<string, string>();
        if (!File.Exists(path)) return properties;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

            var index = trimmed.IndexOf('=');
            if (index > 0)
            {
                var key = trimmed.Substring(0, index).Trim();
                var value = trimmed.Substring(index + 1).Trim();
                properties[key] = value;
            }
        }
        return properties;
    }

    private ConfigModel MapPropertiesToConfigModel(Dictionary<string, string> properties)
    {
        var config = new ConfigModel();

        // Use reflection to automatically map properties
        var configType = typeof(ConfigModel);
        var propertiesInfo = configType.GetProperties();

        foreach (var prop in propertiesInfo)
        {
            var propName = prop.Name.ToLower(); // lowercase

            if (properties.TryGetValue(propName, out var value))
            {
                try
                {
                    if (prop.PropertyType == typeof(string))
                    {
                        prop.SetValue(config, value);
                    }
                    else if (prop.PropertyType == typeof(int) && int.TryParse(value, out var intValue))
                    {
                        prop.SetValue(config, intValue);
                    }
                    else if (prop.PropertyType == typeof(bool) && bool.TryParse(value, out var boolValue))
                    {
                        prop.SetValue(config, boolValue);
                    }
                }
                catch
                {
                    // Skip invalid values
                }
            }

            // Handle lists separately
            if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
            {
                try
                {
                    // Handle lists by finding all indexed properties
                    var listType = prop.PropertyType.GetGenericArguments()[0];
                    var list = (System.Collections.IList)Activator.CreateInstance(prop.PropertyType);

                    // Find all properties that start with propName[index] (case-insensitive)
                    var indexedProps = properties.Where(kvp => kvp.Key.StartsWith($"{propName}[", StringComparison.OrdinalIgnoreCase) && kvp.Key.Contains("]"))
                                                .GroupBy(kvp =>
                                                {
                                                    var startIndex = kvp.Key.IndexOf('[') + 1;
                                                    var endIndex = kvp.Key.IndexOf(']');
                                                    return kvp.Key.Substring(startIndex, endIndex - startIndex);
                                                });

                    foreach (var group in indexedProps.OrderBy(g => int.Parse(g.Key)))
                    {
                        var item = Activator.CreateInstance(listType);
                        foreach (var kvp in group)
                        {
                            var keyParts = kvp.Key.Split('.');
                            if (keyParts.Length >= 2)
                            {
                                var itemPropName = keyParts[1];
                                var itemProp = listType.GetProperty(itemPropName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                if (itemProp != null)
                                {
                                    if (itemProp.PropertyType == typeof(string))
                                    {
                                        itemProp.SetValue(item, kvp.Value);
                                    }
                                    else if (itemProp.PropertyType == typeof(int) && int.TryParse(kvp.Value, out var intVal))
                                    {
                                        itemProp.SetValue(item, intVal);
                                    }
                                    else if (itemProp.PropertyType == typeof(bool) && bool.TryParse(kvp.Value, out var boolVal))
                                    {
                                        itemProp.SetValue(item, boolVal);
                                    }
                                }
                            }
                        }
                        list.Add(item);
                    }
                    prop.SetValue(config, list);
                }
                catch
                {
                    // Skip invalid values
                }
            }
            // Handle nested objects separately
            else if (!prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string) && !prop.PropertyType.IsGenericType)
            {
                try
                {
                    // Handle nested objects
                    var nestedObj = Activator.CreateInstance(prop.PropertyType);
                    var nestedProps = prop.PropertyType.GetProperties();

                    foreach (var nestedProp in nestedProps)
                    {
                        var nestedKey = $"{propName}.{nestedProp.Name.ToLower()}";
                        if (properties.TryGetValue(nestedKey, out var nestedValue))
                        {
                            if (nestedProp.PropertyType == typeof(string))
                            {
                                nestedProp.SetValue(nestedObj, nestedValue);
                            }
                            else if (nestedProp.PropertyType == typeof(int) && int.TryParse(nestedValue, out var intVal))
                            {
                                nestedProp.SetValue(nestedObj, intVal);
                            }
                            else if (nestedProp.PropertyType == typeof(bool) && bool.TryParse(nestedValue, out var boolVal))
                            {
                                nestedProp.SetValue(nestedObj, boolVal);
                            }
                        }
                    }
                    prop.SetValue(config, nestedObj);
                }
                catch
                {
                    // Skip invalid values
                }
            }
        }

        return config;
    }

    private bool IsKnownProperty(string key)
    {
        // Use reflection to get all possible property keys
        var configType = typeof(ConfigModel);
        var propertiesInfo = configType.GetProperties();

        foreach (var prop in propertiesInfo)
        {
            var propName = prop.Name.ToLower();

            // Check if key starts with this property name
            if (key.StartsWith(propName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private Dictionary<string, string> MapConfigModelToProperties(ConfigModel config)
    {
        var properties = new Dictionary<string, string>();

        // Use reflection to automatically flatten properties
        var configType = typeof(ConfigModel);
        var propertiesInfo = configType.GetProperties();

        foreach (var prop in propertiesInfo)
        {
            var propName = prop.Name.ToLower(); // lowercase
            var value = prop.GetValue(config);

            if (value != null)
            {
                if (prop.PropertyType == typeof(string))
                {
                    properties[propName] = (string)value;
                }
                else if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(bool))
                {
                    properties[propName] = value.ToString();
                }
                else if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    // Handle lists by flattening each item
                    var list = (System.Collections.IList)value;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var item = list[i];
                        var itemType = item.GetType();
                        var itemProps = itemType.GetProperties();

                        foreach (var itemProp in itemProps)
                        {
                            // Skip internal .NET properties and read-only properties
                            if (ShouldSkipProperty(itemProp.Name) || !itemProp.CanWrite)
                                continue;

                            var itemValue = itemProp.GetValue(item);
                            if (itemValue != null)
                            {
                                var key = $"{propName}[{i}].{itemProp.Name.ToLower()}";
                                properties[key] = itemValue.ToString();
                            }
                        }
                    }
                }
                else if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(HashSet<>))
                {
                    // Handle HashSet by converting to array notation
                    var hashSet = (System.Collections.IEnumerable)value;
                    int i = 0;
                    foreach (var item in hashSet)
                    {
                        properties[$"{propName}[{i}]"] = item.ToString();
                        i++;
                    }
                }
                else if (!prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string))
                {
                    // Handle nested objects with dot notation
                    var nestedProps = prop.PropertyType.GetProperties();
                    foreach (var nestedProp in nestedProps)
                    {
                        // Skip internal .NET properties
                        if (ShouldSkipProperty(nestedProp.Name))
                            continue;

                        var nestedValue = nestedProp.GetValue(value);
                        if (nestedValue != null)
                        {
                            var nestedKey = $"{propName}.{nestedProp.Name.ToLower()}";
                            properties[nestedKey] = nestedValue.ToString();
                        }
                    }
                }
            }
        }

        return properties;
    }

    private bool ShouldSkipProperty(string propertyName)
    {
        // Skip internal .NET collection properties
        var skipProperties = new[] { "Count", "Capacity", "Comparer", "Keys", "Values" };
        return skipProperties.Contains(propertyName);
    }
}
