using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Common.ExternalApis
{
    public interface IProjectHttpClient
    {
        Task<IPagedList<TeamProjectReference>> GetProjects();
        Task<TeamProject> GetProject(string projectName);
    }
}
