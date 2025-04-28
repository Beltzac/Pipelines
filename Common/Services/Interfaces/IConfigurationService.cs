using Common.Models;

public interface IConfigurationService
{
    ConfigModel GetConfig();
    Task SaveConfigAsync(ConfigModel config);
    string ExportConfig();
    Task ImportConfigAsync(string jsonConfig);
    Task SaveSavedQueriesAsync(List<SavedQuery> savedQueries);
    List<SavedQuery> LoadSavedQueries();
}
