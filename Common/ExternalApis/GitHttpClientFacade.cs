// Common/GitHttpClientFacade.cs
using Common.ExternalApis.Interfaces;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Common.ExternalApis
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

        // New Methods Implementation

        /// <summary>
        /// Retrieves all branches for a given project and repository.
        /// </summary>
        /// <param name="projectId">The GUID of the project.</param>
        /// <param name="repositoryId">The GUID of the repository.</param>
        /// <returns>A list of GitRef objects representing the branches.</returns>
        public async Task<List<GitRef>> GetBranchesAsync(Guid projectId, Guid repositoryId)
        {
            var refs = await _gitHttpClient.GetRefsAsync(
                projectId.ToString(),
                repositoryId,
                filter: "heads/");

            return refs;
        }

        /// <summary>
        /// Retrieves commits based on specified criteria.
        /// </summary>
        /// <param name="projectId">The GUID of the project.</param>
        /// <param name="repositoryId">The GUID of the repository.</param>
        /// <param name="branchName">The name of the branch.</param>
        /// <param name="author">The author of the commits.</param>
        /// <param name="fromDate">The start date for commit search.</param>
        /// <param name="toDate">The end date for commit search.</param>
        /// <returns>A list of GitCommit objects matching the criteria.</returns>
        public async Task<List<GitCommitRef>> GetCommitsAsync(
            Guid projectId,
            Guid repositoryId,
            string branchName,
            string author,
            DateTime fromDate,
            DateTime toDate)
        {
            // Define search criteria
            var searchCriteria = new GitQueryCommitsCriteria
            {
                Author = author,
                FromDate = fromDate.ToString("M/d/yyyy HH:mm:ss"),
                ToDate = toDate.ToString("M/d/yyyy HH:mm:ss"),
                ItemVersion = new GitVersionDescriptor
                {
                    Version = branchName,
                    VersionType = GitVersionType.Branch
                }
            };

            // Fetch commits based on criteria
            var commits = await _gitHttpClient.GetCommitsAsync(
                projectId.ToString(),
                repositoryId,
                searchCriteria);

            return commits;
        }
    }
}
