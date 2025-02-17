using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using System.Linq.Expressions;

namespace Common.Services.Interfaces
{
    public interface IRepositoryService
    {
        Task CloneAllRepositoriesAsync();
        Task CloneRepositoryByBuildInfoAsync(Repository buildInfo);
        Task CloneRepositoryByBuildInfoIdAsync(Guid buildInfoId);
        Task<Repository> CreateBuildInfoAsync(GitRepository repo, BuildDefinitionReference buildDefinition);
        Task Delete(Guid id);
        Task<Repository> FetchRepoBuildInfoAsync(Guid repoId, bool force = false);
        Task<string> GenerateCloneCommands();
        Task<string> GetBuildErrorLogsAsync(Guid id);
        Task<List<Repository>> GetBuildInfoAsync(string filter = null);
        Task<Repository> GetBuildInfoByIdAsync(Guid id);
        Expression<Func<Repository, DateTime>> GetLatestBuildDetailsExpression();
        Task OpenProjectByBuildInfoIdAsync(Guid buildInfoId);
        void OpenCloneFolderInVsCode();
        void NavigateToPRCreation(Repository repo);
        Task<List<Guid>> FetchProjectsRepos();
        Task UpdateRepositoryAsync(Repository repository);
    }
}
