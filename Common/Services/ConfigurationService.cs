using Common.Models;
using System.Text.Json;
using System.IO;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

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
                    var listType = prop.PropertyType.GetGenericArguments()[0];
                    var list = (IList)Activator.CreateInstance(prop.PropertyType);

                    // Find all entries that target this list (start with propName[)
                    var relevant = properties
                        .Where(kvp => kvp.Key.StartsWith($"{propName}[", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // Group by top-level index
                    var grouped = relevant.GroupBy(kvp =>
                    {
                        var startIndex = kvp.Key.IndexOf('[') + 1;
                        var endIndex = kvp.Key.IndexOf(']');
                        return kvp.Key.Substring(startIndex, endIndex - startIndex);
                    }).OrderBy(g => int.Parse(g.Key));

                    foreach (var group in grouped)
                    {
                        var index = group.Key;

                        if (listType == typeof(string) || listType.IsPrimitive)
                        {
                            // Expect direct key: propName[index]=value
                            var directKey = $"{propName}[{index}]";
                            var direct = group.FirstOrDefault(kvp => kvp.Key.Equals(directKey, StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrEmpty(direct.Value))
                            {
                                list.Add(direct.Value);
                            }
                            else
                            {
                                // No other formats allowed for primitive list items
                                list.Add(string.Empty);
                            }

                            continue;
                        }

                        // Complex item - build object and populate using recursive key parsing
                        var item = Activator.CreateInstance(listType);

                        var prefix = $"{propName}[{index}].";
                        foreach (var kvp in group)
                        {
                            if (!kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                continue; // skip keys that aren't nested properties

                            var remainder = kvp.Key.Substring(prefix.Length); // e.g. "child[1].grand.prop"
                            SetValueOnObject(item, remainder, kvp.Value);
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

    // Recursively set a value on a target object given a dotted path which may include indexed lists (e.g. "child[1].prop.sub[0].name")
    private void SetValueOnObject(object target, string path, string value)
    {
        if (target == null || string.IsNullOrEmpty(path)) return;

        var dotIndex = path.IndexOf('.');
        var segment = dotIndex >= 0 ? path.Substring(0, dotIndex) : path;
        var remainder = dotIndex >= 0 ? path.Substring(dotIndex + 1) : string.Empty;

        // segment could be like "name" or "name[2]"
        var listBracket = segment.IndexOf('[');
        if (listBracket >= 0)
        {
            // list property on target
            var propName = segment.Substring(0, listBracket);
            var endBracket = segment.IndexOf(']', listBracket);
            if (endBracket < 0) return; // malformed
            var idxStr = segment.Substring(listBracket + 1, endBracket - listBracket - 1);
            if (!int.TryParse(idxStr, out var idx)) return;

            var propInfo = target.GetType().GetProperty(propName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (propInfo == null) return;

            // Ensure list instance
            var listObj = propInfo.GetValue(target) as IList;
            if (listObj == null)
            {
                var listType = typeof(List<>).MakeGenericType(propInfo.PropertyType.GetGenericArguments()[0]);
                listObj = (IList)Activator.CreateInstance(listType);
                propInfo.SetValue(target, listObj);
            }

            var elemType = propInfo.PropertyType.GetGenericArguments()[0];

            // Ensure list has enough items
            while (listObj.Count <= idx)
            {
                object newItem = elemType.IsValueType && elemType != typeof(string) ? Activator.CreateInstance(elemType) : Activator.CreateInstance(elemType);
                listObj.Add(newItem);
            }

            var element = listObj[idx];

            if (string.IsNullOrEmpty(remainder))
            {
                // setting the list element itself to a primitive value isn't supported for complex types
                // ignore
                return;
            }

            // Recurse into element
            SetValueOnObject(element, remainder, value);
            return;
        }
        else
        {
            // simple property on target
            var propInfo = target.GetType().GetProperty(segment, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (propInfo == null) return;

            if (string.IsNullOrEmpty(remainder))
            {
                // Set value on this property, converting to the correct type
                object converted = ConvertToType(value, propInfo.PropertyType);
                propInfo.SetValue(target, converted);
                return;
            }
            else
            {
                // Need to recurse into nested property
                var nestedObj = propInfo.GetValue(target);
                if (nestedObj == null)
                {
                    nestedObj = Activator.CreateInstance(propInfo.PropertyType);
                    propInfo.SetValue(target, nestedObj);
                }

                SetValueOnObject(nestedObj, remainder, value);
                return;
            }
        }
    }

    private object ConvertToType(string value, Type targetType)
    {
        if (targetType == typeof(string)) return value;
        if (targetType == typeof(int) && int.TryParse(value, out var i)) return i;
        if (targetType == typeof(bool) && bool.TryParse(value, out var b)) return b;
        if (targetType == typeof(Guid) && Guid.TryParse(value, out var g)) return g;

        // Try enum
        if (targetType.IsEnum)
        {
            try { return Enum.Parse(targetType, value, true); } catch { }
        }

        // Try TypeConverter
        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter != null && converter.IsValid(value))
        {
            try { return converter.ConvertFromInvariantString(value); } catch { }
        }

        // Fallback: return default
        return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
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
                    var list = (IList)value;
                    var itemType = prop.PropertyType.GetGenericArguments()[0];

                    for (int i = 0; i < list.Count; i++)
                    {
                        var item = list[i];

                        // For primitive/string list items, write as propName[index]=value
                        if (itemType == typeof(string) || itemType.IsPrimitive)
                        {
                            properties[$"{propName}[{i}]"] = item?.ToString() ?? string.Empty;
                            continue;
                        }

                        // For complex items, flatten their properties recursively
                        FlattenObjectProperties(properties, item, $"{propName}[{i}]");
                    }
                }
                else if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(HashSet<>))
                {
                    var hashSet = (IEnumerable)value;
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

    // Recursively flatten object properties into the properties dictionary using prefix like "parent[0]" or "parent[0].child[1]"
    private void FlattenObjectProperties(Dictionary<string, string> properties, object obj, string prefix)
    {
        if (obj == null) return;

        var type = obj.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var p in props)
        {
            if (ShouldSkipProperty(p.Name) || !p.CanWrite) continue;
            var val = p.GetValue(obj);
            if (val == null) continue;

            if (p.PropertyType == typeof(string) || p.PropertyType.IsPrimitive)
            {
                properties[$"{prefix}.{p.Name.ToLower()}"] = val.ToString();
            }
            else if (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)val;
                var elemType = p.PropertyType.GetGenericArguments()[0];
                for (int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    if (elemType == typeof(string) || elemType.IsPrimitive)
                    {
                        properties[$"{prefix}.{p.Name.ToLower()}[{i}]"] = item?.ToString() ?? string.Empty;
                    }
                    else
                    {
                        FlattenObjectProperties(properties, item, $"{prefix}.{p.Name.ToLower()}[{i}]");
                    }
                }
            }
            else
            {
                FlattenObjectProperties(properties, val, $"{prefix}.{p.Name.ToLower()}");
            }
        }
    }

    private bool ShouldSkipProperty(string propertyName)
    {
        // Skip internal .NET collection properties
        var skipProperties = new[] { "Count", "Capacity", "Comparer", "Keys", "Values" };
        return skipProperties.Contains(propertyName);
    }
}
