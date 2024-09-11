using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common
{
    public interface IProjectHttpClient
    {
        Task<IPagedList<TeamProjectReference>> GetProjects();
        Task<TeamProject> GetProject(string projectName);
    }
}
