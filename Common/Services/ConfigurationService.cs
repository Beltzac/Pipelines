using Common.Models;
using System.Text.Json;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configPath;
    private ConfigModel _config;

    public ConfigurationService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TugboatCaptainsPlayground",
            "config.json"))
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
            string jsonConfig = File.ReadAllText(_configPath);
            _config = JsonSerializer.Deserialize<ConfigModel>(jsonConfig);
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
                throw new ArgumentException("Formato de configuração inválido");

            ValidateConfig(importedConfig);
            await SaveConfigAsync(importedConfig);
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
}
