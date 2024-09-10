using Microsoft.TeamFoundation.Core.WebApi;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common
{
    public class ProjectHttpClientFacade : IProjectHttpClient
    {
        private readonly ProjectHttpClient _projectHttpClient;

        public ProjectHttpClientFacade(ProjectHttpClient projectHttpClient)
        {
            _projectHttpClient = projectHttpClient;
        }

        public Task<List<TeamProjectReference>> GetProjects()
        {
            return _projectHttpClient.GetProjects();
        }

        public Task<TeamProjectReference> GetProject(string projectName)
        {
            return _projectHttpClient.GetProject(projectName);
        }
    }
}
