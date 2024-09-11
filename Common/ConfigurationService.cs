using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

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

        LoadConfig();
    }

    private void LoadConfig()
    {
        if (File.Exists(_configPath))
        {
            string jsonConfig = File.ReadAllText(_configPath);
            _config = JsonSerializer.Deserialize<ConfigModel>(jsonConfig);
        }
        else
        {
            _config = new ConfigModel
            {
                PAT = "",
                OrganizationUrl = "https://dev.azure.com/terminal-cp",
                LocalCloneFolder = @"C:\repos"
            };
            SaveConfig();
        }
    }

    private void SaveConfig()
    {
        string jsonConfig = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, jsonConfig);
    }

    public ConfigModel GetConfig()
    {
        return _config;
    }

    public async Task SaveConfigAsync(ConfigModel config)
    {
        _config = config;
        await Task.Run(() => SaveConfig());
    }
}

public class ConfigModel
{
    public string PAT { get; set; }
    public string OrganizationUrl { get; set; }
    public string LocalCloneFolder { get; set; }
}