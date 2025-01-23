using Common.Models;

public interface IConfigurationService
{
    ConfigModel GetConfig();
    Task SaveConfigAsync(ConfigModel config);
    string ExportConfig();
    Task ImportConfigAsync(string jsonConfig);
}
