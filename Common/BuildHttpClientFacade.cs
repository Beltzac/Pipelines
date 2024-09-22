using Microsoft.TeamFoundation.Build.WebApi;

namespace Common
{
    public class BuildHttpClientFacade : IBuildHttpClient
    {
        private readonly BuildHttpClient _buildHttpClient;

        public BuildHttpClientFacade(BuildHttpClient buildHttpClient)
        {
            _buildHttpClient = buildHttpClient;
        }

        public Task<List<BuildDefinitionReference>> GetDefinitionsAsync(string project, string repositoryId, string repositoryType, bool includeLatestBuilds)
        {
            return _buildHttpClient.GetDefinitionsAsync(project, repositoryId: repositoryId, repositoryType: repositoryType, includeLatestBuilds: includeLatestBuilds);
        }

        public Task<Microsoft.TeamFoundation.Build.WebApi.Build> GetBuildAsync(string project, int buildId)
        {
            return _buildHttpClient.GetBuildAsync(project, buildId);
        }

        public Task<List<BuildLog>> GetBuildLogsAsync(string project, int buildId)
        {
            return _buildHttpClient.GetBuildLogsAsync(project, buildId);
        }

        public Task<List<string>> GetBuildLogLinesAsync(string project, int buildId, int logId)
        {
            return _buildHttpClient.GetBuildLogLinesAsync(project, buildId, logId);
        }
    }
}
