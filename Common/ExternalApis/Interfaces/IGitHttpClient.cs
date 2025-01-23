using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Common.ExternalApis.Interfaces
{
    public interface IGitHttpClient
    {
        Task<List<GitRepository>> GetRepositoriesAsync(Guid projectId);
        Task<GitRepository> GetRepositoryAsync(Guid repositoryId);
        Task<GitRepository> GetRepositoryAsync(string projectName, Guid repositoryId);
        Task<GitCommit> GetCommitAsync(string projectName, string commitId, Guid repositoryId);
        Task<List<GitRef>> GetBranchesAsync(Guid projectId, Guid repositoryId);
        Task<List<GitCommitRef>> GetCommitsAsync(Guid projectId, Guid repositoryId, string branchName, string author, DateTime fromDate, DateTime toDate);
    }
}
