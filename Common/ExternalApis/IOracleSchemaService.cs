namespace Common.ExternalApis
{
    public interface IOracleSchemaService
    {
        Dictionary<string, string> Compare(string sourceEnvName, string targetEnvName);
        Dictionary<string, string> CompareViewDefinitions(Dictionary<string, string> sourceViews, Dictionary<string, string> targetViews);
        Dictionary<string, string> GetViewDefinitions(string connectionString, string schema);
    }
}
