using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Common.ExternalApis
{
    public class ProjectHttpClientFacade : IProjectHttpClient
    {
        private readonly ProjectHttpClient _projectHttpClient;

        public ProjectHttpClientFacade(ProjectHttpClient projectHttpClient)
        {
            _projectHttpClient = projectHttpClient;
        }

        public Task<IPagedList<TeamProjectReference>> GetProjects()
        {
            return _projectHttpClient.GetProjects();
        }

        public Task<TeamProject> GetProject(string projectName)
        {
            return _projectHttpClient.GetProject(projectName);
        }
    }
}
