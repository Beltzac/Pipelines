using Common.Models;
using System.Text.Json;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configPath;
    private ConfigModel _config;

    public ConfigurationService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BuildInfoBlazorApp",
            "config.json"))
    {
    }

    // This constructor is used for testing
    public ConfigurationService(string configPath)
    {
        _configPath = configPath;
        string appFolder = Path.GetDirectoryName(_configPath);

        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }

        LoadConfigAsync().GetAwaiter().GetResult();
    }

    private async Task LoadConfigAsync()
    {
        if (File.Exists(_configPath))
        {
            string jsonConfig = await File.ReadAllTextAsync(_configPath);
            _config = JsonSerializer.Deserialize<ConfigModel>(jsonConfig);
        }
        else
        {
            _config = new ConfigModel
            {
                OrganizationUrl = "https://dev.azure.com/terminal-cp",
                LocalCloneFolder = @"C:\repos",
                IgnoreRepositoriesRegex = new List<string>()
            };
            await SaveConfigAsync();
        }
    }

    private async Task SaveConfigAsync()
    {
        string jsonConfig = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_configPath, jsonConfig);
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
            var importedConfig = JsonSerializer.Deserialize<ConfigModel>(jsonConfig);
            if (importedConfig == null)
                throw new ArgumentException("Invalid configuration format");

            ValidateConfig(importedConfig);
            await SaveConfigAsync(importedConfig);
        }
        catch (JsonException)
        {
            throw new ArgumentException("Invalid JSON format");
        }
    }

    private void ValidateConfig(ConfigModel config)
    {
        if (config == null)
            throw new ArgumentException("Configuration cannot be null");

        if (string.IsNullOrWhiteSpace(config.OrganizationUrl))
            throw new ArgumentException("Organization URL is required");

        if (config.OracleEnvironments != null)
        {
            foreach (var env in config.OracleEnvironments)
            {
                if (string.IsNullOrWhiteSpace(env.Name))
                    throw new ArgumentException("Oracle environment name is required");
                if (string.IsNullOrWhiteSpace(env.ConnectionString))
                    throw new ArgumentException("Oracle connection string is required");
                if (string.IsNullOrWhiteSpace(env.Schema))
                    throw new ArgumentException("Oracle schema is required");
            }
        }

        if (config.ConsulEnvironments != null)
        {
            foreach (var env in config.ConsulEnvironments)
            {
                if (string.IsNullOrWhiteSpace(env.Name))
                    throw new ArgumentException("Consul environment name is required");
                if (string.IsNullOrWhiteSpace(env.ConsulUrl))
                    throw new ArgumentException("Consul URL is required");
            }
        }
    }
}
