using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Common
{
    public class GitHttpClientFacade : IGitHttpClient
    {
        private readonly GitHttpClient _gitHttpClient;

        public GitHttpClientFacade(GitHttpClient gitHttpClient)
        {
            _gitHttpClient = gitHttpClient;
        }

        public Task<List<GitRepository>> GetRepositoriesAsync(Guid projectId)
        {
            return _gitHttpClient.GetRepositoriesAsync(projectId);
        }

        public Task<GitRepository> GetRepositoryAsync(Guid repositoryId)
        {
            return _gitHttpClient.GetRepositoryAsync(repositoryId);
        }

        public Task<GitRepository> GetRepositoryAsync(string projectName, Guid repositoryId)
        {
            return _gitHttpClient.GetRepositoryAsync(projectName, repositoryId);
        }

        public Task<GitCommit> GetCommitAsync(string projectName, string commitId, Guid repositoryId)
        {
            return _gitHttpClient.GetCommitAsync(projectName, commitId, repositoryId);
        }
    }
}
