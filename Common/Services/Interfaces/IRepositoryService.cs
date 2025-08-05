using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using System.Linq.Expressions;

namespace Common.Services.Interfaces
{
    public interface IRepositoryService
    {
        Task<(int Successful, int Failed)> CloneAllRepositoriesAsync(Func<int, string, Task> reportProgress, CancellationToken cancellationToken);
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
        void OpenRepoInVsCode(Repository repo);
        void NavigateToPRCreation(Repository repo);
        Task<List<Guid>> FetchProjectsRepos();
        Task UpdateRepositoryAsync(Repository repository);
        Task TogglePin(Repository repo);
        bool IsPinned(Repository repo);
        string GetLocalCloneFolder();
        Task<List<string>> GetRemoteBranches(string repoPath);
        Task<(bool Success, string ErrorMessage)> CheckoutBranch(Repository buildInfo, string branchName);
        Task<int> GetActivePullRequestCountAsync(Guid repositoryId);
        Task<Guid> GetIdFromPathAsync(string? path);
        Task<(bool Success, string ErrorMessage)> PullRepositoryAsync(Guid buildInfoId, CancellationToken cancellationToken);
        Task<(int Successful, int Failed)> PullAllRepositoriesAsync(Func<int, string, Task> reportProgress, CancellationToken cancellationToken);
        Task FetchBuildInfoAsync();
    }
}
