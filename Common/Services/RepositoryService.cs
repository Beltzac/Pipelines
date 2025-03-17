using Common.ExternalApis.Interfaces;
using Common.Repositories.Interno.Interfaces;
using Common.Services.Interfaces;
using Common.Utils;


using Flurl;
using LibGit2Sharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using SmartComponents.LocalEmbeddings;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace Common.Services
{
    public class RepositoryService : IRepositoryService
    {
        private readonly IRepositoryDatabase _repositoryDatabase;
        private readonly IBuildHttpClient _buildClient;
        private readonly IProjectHttpClient _projectClient;
        private readonly IGitHttpClient _gitClient;
        private readonly ILogger<RepositoryService> _logger;
        private readonly string _localCloneFolder;
        private readonly string _privateToken;
        private readonly string _name;
        private readonly List<string> _repoRegexFilters;
        private readonly string _organizationUrl;
        private readonly IConfigurationService _configService;
        private readonly LocalEmbedder _embedder;

        public RepositoryService(
            ILogger<RepositoryService> logger,
            IConfigurationService configService,
            IRepositoryDatabase repositoryDatabase,
            IBuildHttpClient buildClient,
            IProjectHttpClient projectClient,
            IGitHttpClient gitClient,
            LocalEmbedder embeder)
        {
            var config = configService.GetConfig();

            _name = config.Username;
            _localCloneFolder = config.LocalCloneFolder;
            _repositoryDatabase = repositoryDatabase;
            _buildClient = buildClient;
            _projectClient = projectClient;
            _gitClient = gitClient;
            _logger = logger;
            _privateToken = config.PAT;
            _repoRegexFilters = config.IgnoreRepositoriesRegex;
            _organizationUrl = config.OrganizationUrl;
            _embedder = embeder;
            _configService = configService;
        }

        public void NavigateToPRCreation(Repository repo)
        {
            if (repo == null)
            {
                _logger.LogWarning("Nenhum repositório selecionado para criação de PR.");
                return;
            }

            var url = _organizationUrl
                .AppendPathSegment(repo.Project)
                .AppendPathSegment("_git")
                .AppendPathSegment(repo.Name)
                .AppendPathSegment("pullrequests")
                .SetQueryParams(new { _a = "mine" });
            OpenFolderUtils.OpenUrl(url);
        }

        public async Task<Repository> FetchRepoBuildInfoAsync(Guid repoId, bool force = false)
        {
            var existingRepo = await _repositoryDatabase.FindByIdAsync(repoId);

            if (existingRepo == null)
            {
                _logger.LogInformation($"Repositório {repoId} não encontrado.");
                return null;
            }

            if (_repoRegexFilters.Any(pattern => Regex.IsMatch(existingRepo.Project, pattern))
                || _repoRegexFilters.Any(pattern => Regex.IsMatch(existingRepo.Name, pattern)))
            {
                _logger.LogInformation($"Repositório {existingRepo.Path} caiu no filtro. Excluindo.");
                await Delete(existingRepo.Id);
                return null;
            }

            var buildDefinitions = await _buildClient.GetDefinitionsAsync(existingRepo.Project, repositoryId: existingRepo.Id.ToString(), repositoryType: RepositoryTypes.TfsGit, includeLatestBuilds: true);
            var buildDefinition = buildDefinitions.FirstOrDefault();

            //if (buildDefinition?.LatestBuild != null)
            //{
            //    var existingDate = existingRepo?.Pipeline?.Last?.Changed;
            //    var latestDate = buildDefinition.LatestBuild.LastChangedDate;

            //    // Check if the difference is within a reasonable tolerance (e.g., 1 second)
            //    bool areDatesEqual = existingDate.HasValue &&
            //                            Math.Abs((existingDate.Value - latestDate).TotalSeconds) < 1;

            //    if (areDatesEqual && !force)
            //    {
            //        _logger.LogInformation($"Pipeline {buildDefinition.Name} não alterado. Pulando.");
            //        return existingRepo;
            //    }

            //    _logger.LogInformation($"Pipeline {buildDefinition.Name} alterado. Atualizando informações de build.");
            //}
            //else
            //{
            //    _logger.LogInformation($"Repositório {existingRepo.Name} não tem pipeline/build. Criando informações de build.");
            //}

            GitRepository repo = null;

            try
            {
                repo = await _gitClient.GetRepositoryAsync(repoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao buscar repositório git {repoId}");
            }

            if (repo?.IsDisabled ?? false)
            {
                await Delete(repo.Id);
                _logger.LogInformation($"Repositório {repo.Name} está desabilitado. Excluindo.");
                return null;
            }

            var buildInfo = await CreateBuildInfoAsync(repo, buildDefinition);
            await GenerateEmbeddingsForReposAsync(buildInfo);
            await UpsertAndPublish(buildInfo);
            return buildInfo;
        }

        public async Task<List<Repository>> GetBuildInfoAsync(string filter = null)
        {
            //https://github.com/dotnet-smartcomponents/smartcomponents/blob/main/docs/local-embeddings.md

            var query = _repositoryDatabase.Query()
                .Where(repo => !_repoRegexFilters.Any(pattern => Regex.IsMatch(repo.Name, pattern))
                               && !_repoRegexFilters.Any(pattern => Regex.IsMatch(repo.Project, pattern)));

            var repos = query.ToList();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var filtroEmbedding = _embedder.Embed(filter);
                var results = await GenerateEmbeddingsForReposAsync(repos);

                var results2 = LocalEmbedder.FindClosest(filtroEmbedding, results, maxResults: 999, minSimilarity: 0.6f);

                return results2.ToList();
            }

            //var results = query.ToList();
            var ordered = repos.AsQueryable()
                                 .OrderByDescending(GetLatestBuildDetailsExpression())
                                 .ToList();

            return ordered;
        }

        private async Task<IEnumerable<(Repository Item, EmbeddingF32 Embedding)>> GenerateEmbeddingsForReposAsync(params List<Repository> repos)
        {
            // Embbed only what doesnt have one yet

            foreach (var repo in repos)
            {
                if (repo.Embedding == null)
                {
                    repo.Embedding = _embedder.Embed(repo.Path);
                    // TODO: melhorar eficiencia
                    await _repositoryDatabase.UpsertAsync(repo);
                }
            }

            return repos.Select(x => (x, x.Embedding.Value));
        }

        public async Task<Repository> GetBuildInfoByIdAsync(Guid id)
        {
            return await _repositoryDatabase.Query().Where(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<string?> GetBuildErrorLogsAsync(Guid id)
        {
            var repo = await _repositoryDatabase.FindByIdAsync(id);
            return repo?.Pipeline?.Last?.ErrorLogs;
        }

        public async Task FetchBuildInfoAsync()
        {
            var projects = await _projectClient.GetProjects();
            var fetchTasks = projects.Select(FetchProjectBuildInfoAsync);
            await Task.WhenAll(fetchTasks);
        }

        private async Task FetchProjectBuildInfoAsync(TeamProjectReference project)
        {
            _logger.LogInformation($"Fetching builds for project: {project.Name}");
            var repos = await _gitClient.GetRepositoriesAsync(project.Id);
            var fetchTasks = repos.Select(repo => FetchRepoBuildInfoAsync(repo.Id));
            await Task.WhenAll(fetchTasks);
        }

        public async Task<List<Guid>> FetchProjectsRepos()
        {
            var projects = await _projectClient.GetProjects();

            var projectsRepos = new List<Guid>();

            foreach (var project in projects)
            {
                if (_repoRegexFilters.Any(pattern => Regex.IsMatch(project.Name, pattern)))
                {
                    continue;
                }

                var projectRepos = await _gitClient.GetRepositoriesAsync(project.Id);
                projectsRepos.AddRange(projectRepos.Select(repo => repo.Id));

                // Cadastrar o basico se ele nao existir
                foreach (var repo in projectRepos)
                {
                    if (_repoRegexFilters.Any(pattern => Regex.IsMatch(repo.Name, pattern)))
                    {
                        continue;
                    }

                    if (!await _repositoryDatabase.ExistsByIdAsync(repo.Id))
                    {
                        var buildInfo = await CreateBuildInfoAsync(repo, null);
                        await GenerateEmbeddingsForReposAsync(buildInfo);
                        await UpsertAndPublish(buildInfo);
                    }
                }
            }

            return projectsRepos;
        }

        public async Task<Repository> CreateBuildInfoAsync(GitRepository repo, BuildDefinitionReference buildDefinition)
        {
            var projectName = repo.ProjectReference.Name;

            var localPath = Path.Combine(_localCloneFolder, projectName, repo.Name);
            var projectType = OpenFolderUtils.DetermineProjectType(localPath);

            var projectNames = OpenFolderUtils.GetCSharpProjectNames(localPath);

            var buildInfo = new Repository
            {
                Id = repo.Id,
                Project = projectName,
                Name = repo.Name,
                MasterClonned = Directory.Exists(Path.Combine(_localCloneFolder, projectName, repo.Name)),
                Url = repo.WebUrl,
                CloneUrl = repo.RemoteUrl,
                Pipeline = buildDefinition != null ? new Pipeline { Id = buildDefinition.Id } : null,
                ProjectType = projectType,
                ProjectNames = projectNames
            };

            if (buildDefinition?.LatestBuild != null)
            {
                var buildDetails = await _buildClient.GetBuildAsync(projectName, buildDefinition.LatestBuild.Id);
                buildInfo.Pipeline.Last = new Build
                {
                    Id = buildDetails.Id,
                    Changed = buildDetails.LastChangedDate,
                    Queued = buildDetails.QueueTime,
                    Result = buildDetails.Result?.ToString(),
                    Status = buildDetails.Status?.ToString(),
                    Url = (buildDetails.Links.Links["web"] as ReferenceLink).Href
                };

                await FetchCommitInfoAsync(buildInfo, projectName, buildDetails.SourceBranch, buildInfo.Id, buildDetails.SourceVersion);

                // TODO: REVER
                //if (buildDetails.Result == BuildResult.Failed)
                //{
                //    buildInfo.Pipeline.Last.ErrorLogs = await FetchBuildLogsAsync(projectName, buildDetails.Id);
                //}

                _logger.LogInformation($"Pipeline: {buildDefinition.Name}, Latest Build: {buildDetails.FinishTime}, Status: {buildDetails.Status}, Result: {buildDetails.Result}, Commit: {buildDetails.SourceVersion}");
            }

            return buildInfo;
        }

        private async Task<string> FetchBuildLogsAsync(string projectName, int buildId)
        {
            var logs = await _buildClient.GetBuildLogsAsync(projectName, buildId);
            if (logs == null) return string.Empty;

            var content = new StringBuilder();
            foreach (var log in logs)
            {
                var logLines = await _buildClient.GetBuildLogLinesAsync(projectName, buildId, log.Id);

                if (logLines.Contains("error", StringComparer.OrdinalIgnoreCase) || logLines.Contains("exception", StringComparer.OrdinalIgnoreCase))
                {
                    content.AppendLine(string.Join("\n", logLines));
                }
            }

            _logger.LogInformation($"Got logs for Build ID {buildId}");
            return content.ToString();
        }

        private async Task FetchCommitInfoAsync(Repository buildInfo, string projectName, string branch, Guid repoId, string commitId)
        {
            try
            {
                var commit = await _gitClient.GetCommitAsync(projectName, commitId, repoId);
                buildInfo.Pipeline.Last.Commit = new Common.Models.Commit
                {
                    Id = commit.CommitId,
                    CommitMessage = commit.Comment,
                    Url = commit.RemoteUrl,
                    AuthorName = commit.Author.Name,
                    AuthorEmail = commit.Author.Email,
                    ProjectName = buildInfo.Project,
                    RepoName = buildInfo.Name,
                    BranchName = CommitExportService.GetBranchName(branch),
                    CommitDate = commit.Author.Date.ToUniversalTime(),
                    JiraCardID = CommitExportService.ExtractJiraCardID(commit.Comment)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching commit {commitId}");
            }
        }

        private static DateTime GetLatestBuildDate(Repository repository)
        {
            return repository.Pipeline != null && repository.Pipeline.Last != null
                ? repository.Pipeline.Last.Changed
                : DateTime.MinValue;
        }

        public Expression<Func<Repository, DateTime>> GetLatestBuildDetailsExpression()
        {
            return x => GetLatestBuildDate(x);
        }

        public async Task CloneAllRepositoriesAsync()
        {
            var buildInfos = await GetBuildInfoAsync();

            foreach (var buildInfo in buildInfos)
            {
                await CloneRepositoryByBuildInfoAsync(buildInfo);
            }
        }

        public async Task CloneRepositoryByBuildInfoIdAsync(Guid buildInfoId)
        {
            var buildInfo = await _repositoryDatabase.FindByIdAsync(buildInfoId);
            if (buildInfo != null)
            {
                await CloneRepositoryByBuildInfoAsync(buildInfo);
            }
            else
            {
                _logger.LogInformation($"Build information with ID {buildInfoId} not found");
            }
        }

        public async Task CloneRepositoryByBuildInfoAsync(Repository buildInfo)
        {
            var localPath = Path.Combine(_localCloneFolder, buildInfo.Project, buildInfo.Name);

            if (Directory.Exists(localPath))
            {
                _logger.LogInformation($"Repository {buildInfo.Name} already cloned to {localPath}");
                buildInfo.MasterClonned = true;
                await UpsertAndPublish(buildInfo, false);
                return;
            }

            try
            {
                Directory.CreateDirectory(localPath);
                var cloneOptions = new CloneOptions
                {
                    Checkout = true
                };

                cloneOptions.FetchOptions.CertificateCheck = (cert, valid, host) => true;
                cloneOptions.FetchOptions.CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = "Anything", Password = _privateToken };

                LibGit2Sharp.Repository.Clone(buildInfo.CloneUrl, localPath, cloneOptions);

                buildInfo.MasterClonned = true;
                await UpsertAndPublish(buildInfo, false);

                _logger.LogInformation($"Repository {buildInfo.Name} cloned to {localPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cloning repository {buildInfo.Name}");
            }
        }

        public async Task OpenProjectByBuildInfoIdAsync(Guid buildInfoId)
        {
            var buildInfo = await _repositoryDatabase.FindByIdAsync(buildInfoId);

            if (buildInfo != null)
            {
                var localPath = Path.Combine(_localCloneFolder, buildInfo.Project, buildInfo.Name);
                OpenFolderUtils.OpenProject(_logger, _configService, localPath);
            }
            else
            {
                _logger.LogInformation($"BuildInfo with ID {buildInfoId} not found");
            }
        }

        public void OpenCloneFolderInVsCode()
        {
            OpenFolderUtils.OpenWithVSCode(_logger, _configService, _localCloneFolder, true);
        }

        private async Task UpsertAndPublish(Repository buildInfo, bool notify = true)
        {
            await _repositoryDatabase.UpsertAsync(buildInfo);
            //await _hubContext.Clients.All.SendAsync("Update", buildInfo.Id);

            if (!notify)
            {
                return;
            }

            var isMine = buildInfo.Pipeline?.Last?.Commit?.AuthorName?.Trim().Contains(_name, StringComparison.OrdinalIgnoreCase) ?? false;

            if (isMine)
            {
                //if (HybridSupport.IsElectronActive)
                //{
                //    Electron.Notification.Show(
                //       new NotificationOptions(
                //           buildInfo.Path,
                //           buildInfo.Pipeline?.Last?.Status
                //       ));
                //}
            }
        }

        public async Task Delete(Guid id)
        {
            await _repositoryDatabase.DeleteAsync(id);
            //await _hubContext.Clients.All.SendAsync("Update", id);
        }

        public async Task<string> GenerateCloneCommands()
        {
            var buildInfos = await GetBuildInfoAsync();
            var commands = new StringBuilder();

            commands.AppendLine("@echo off");
            commands.AppendLine($"set REPO_ROOT={_localCloneFolder}");

            foreach (var buildInfo in buildInfos)
            {
                var localPath = Path.Combine("%REPO_ROOT%", buildInfo.Project, buildInfo.Name);

                commands.AppendLine($"IF NOT EXIST \"{localPath}\" (");
                commands.AppendLine($"  mkdir \"{localPath}\"");

                try
                {
                    var repository = await _gitClient.GetRepositoryAsync(buildInfo.Project, buildInfo.Id);
                    if (repository != null)
                    {
                        var cloneUrl = repository.RemoteUrl.Replace("%", "%%");
                        commands.AppendLine($"  git clone \"{cloneUrl}\" \"{localPath}\"");
                        _logger.LogInformation($"Added clone command for repository {buildInfo.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error fetching repository {buildInfo.Id}");
                }

                commands.AppendLine(") ELSE (");
                commands.AppendLine($"  echo \"Repository {buildInfo.Name} already cloned at {localPath}\"");
                commands.AppendLine(")");
            }

            return commands.ToString();
        }

        public async Task UpdateRepositoryAsync(Repository repository)
        {
            await _repositoryDatabase.UpsertAsync(repository);
        }

        public async Task TogglePin(Repository repo)
        {
            var config = _configService.GetConfig();

            if (config.PinnedRepositories.Contains(repo.Id))
            {
                config.PinnedRepositories.Remove(repo.Id);
            }
            else
            {
                config.PinnedRepositories.Add(repo.Id);
            }

            await _configService.SaveConfigAsync(config);
        }

        public bool IsPinned(Repository repo)
        {
            var config = _configService.GetConfig();
            return config.PinnedRepositories.Contains(repo.Id);
        }
    }
}
