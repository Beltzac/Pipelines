using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using System.Linq.Expressions;

namespace Common.Services
{
    public interface IBuildInfoService
    {
        Task CloneAllRepositoriesAsync();
        Task CloneRepositoryByBuildInfoAsync(Repository buildInfo);
        Task CloneRepositoryByBuildInfoIdAsync(Guid buildInfoId);
        Task<Repository> CreateBuildInfoAsync(GitRepository repo, BuildDefinitionReference buildDefinition);
        Task Delete(Guid id);
        Task<Repository> FetchRepoBuildInfoAsync(Guid repoId);
        Task<Repository> FetchRepoBuildInfoAsync(GitRepository repo);
        Task<List<Guid>> FetchReposGuids();
        string FindSolutionFile(string folderPath);
        Task<string> GenerateCloneCommands();
        Task<string> GetBuildErrorLogsAsync(int buildId);
        Task<List<Repository>> GetBuildInfoAsync(string filter = null);
        Task<Repository> GetBuildInfoByIdAsync(Guid id);
        Expression<Func<Repository, DateTime>> GetLatestBuildDetailsExpression();
        Task OpenCloneFolderInVsCode();
        void OpenFolder(string localPath);
        Task OpenProjectByBuildInfoIdAsync(Guid buildInfoId);
    }
}