using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Common
{
    public interface IGitHttpClient
    {
        Task<List<GitRepository>> GetRepositoriesAsync(Guid projectId);
        Task<GitRepository> GetRepositoryAsync(Guid repositoryId);
        Task<GitRepository> GetRepositoryAsync(string projectName, Guid repositoryId);
        Task<GitCommit> GetCommitAsync(string projectName, string commitId, Guid repositoryId);
    }
}
