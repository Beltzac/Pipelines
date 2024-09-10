using Microsoft.TeamFoundation.Core.WebApi;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common
{
    public interface IProjectHttpClient
    {
        Task<List<TeamProjectReference>> GetProjects();
        Task<TeamProjectReference> GetProject(string projectName);
    }
}
