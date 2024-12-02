using Microsoft.TeamFoundation.Build.WebApi;
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
        Task<string> GenerateCloneCommands();
        Task<string> GetBuildErrorLogsAsync(Guid id);
        Task<List<Repository>> GetBuildInfoAsync(string filter = null);
        Task<Repository> GetBuildInfoByIdAsync(Guid id);
        Expression<Func<Repository, DateTime>> GetLatestBuildDetailsExpression();
        Task OpenProjectByBuildInfoIdAsync(Guid buildInfoId);
        Task OpenCloneFolderInVsCode();
        Task NavigateToPRCreationAsync(Repository repo);
        Task<List<Guid>> FetchProjectsRepos();
    }
}
