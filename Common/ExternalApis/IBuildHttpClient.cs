using Microsoft.TeamFoundation.Build.WebApi;

namespace Common.ExternalApis
{
    public interface IBuildHttpClient
    {
        Task<List<BuildDefinitionReference>> GetDefinitionsAsync(string project, string repositoryId, string repositoryType, bool includeLatestBuilds);
        Task<Microsoft.TeamFoundation.Build.WebApi.Build> GetBuildAsync(string project, int buildId);
        Task<List<BuildLog>> GetBuildLogsAsync(string project, int buildId);
        Task<List<string>> GetBuildLogLinesAsync(string project, int buildId, int logId);
    }
}
