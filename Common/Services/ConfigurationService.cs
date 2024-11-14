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
}
