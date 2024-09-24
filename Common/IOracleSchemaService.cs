
namespace Common
{
    public interface IOracleSchemaService
    {
        Dictionary<string, string> Compare();
        Dictionary<string, string> CompareViewDefinitions(Dictionary<string, string> devViews, Dictionary<string, string> qaViews);
        Dictionary<string, string> GetViewDefinitions(string connectionString, string schema);
    }
}