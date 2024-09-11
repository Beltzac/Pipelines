using Common;
using LibGit2Sharp;
using LiteDB;
using LiteDB.Async;
using LiteDB.Queryable;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Options;

namespace BuildInfoBlazorApp.Data
{
    public class BuildInfoService
    {
        private readonly IRepositoryDatabase _repositoryDatabase;
        private readonly IHubContext<BuildInfoHub> _hubContext;
        private readonly IBuildHttpClient _buildClient;
        private readonly IProjectHttpClient _projectClient;
        private readonly IGitHttpClient _gitClient;
        private readonly ILiteCollectionAsync<Repository> _reposCollection;
        private readonly ILogger<BuildInfoService> _logger;
        private readonly IConfigurationService _configService;
        private readonly string _localCloneFolder;
        private readonly string _privateToken;

        public BuildInfoService(
            IHubContext<BuildInfoHub> hubContext,
            ILogger<BuildInfoService> logger,
            IConfigurationService configService,
            IRepositoryDatabase repositoryDatabase,
            IBuildHttpClient buildClient,
            IProjectHttpClient projectClient,
            IGitHttpClient gitClient)
        {
            _configService = configService;
            var config = _configService.GetConfig();

            _localCloneFolder = config.LocalCloneFolder;
            _repositoryDatabase = repositoryDatabase;
            _hubContext = hubContext;
            _buildClient = buildClient;
            _projectClient = projectClient;
            _gitClient = gitClient;
            _logger = logger;
            _privateToken = config.PAT;
        }

        private async Task FetchRepoBuildInfoAsync(TeamProjectReference project, GitRepository repo)
        {
            try
            {
                if (repo.IsDisabled ?? false)
                {
                    await Delete(repo.Id);
                    _logger.LogInformation($"Repo {repo.Name} is disabled. Deleting.");
                    return;
                }

                var buildDefinitions = await _buildClient.GetDefinitionsAsync(project.Name, repositoryId: repo.Id.ToString(), repositoryType: RepositoryTypes.TfsGit, includeLatestBuilds: true);
                var buildDefinition = buildDefinitions.FirstOrDefault();

                if (buildDefinition?.LatestBuild != null)
                {
                    var existingRepo = await _repositoryDatabase.Query().Where(x => x.Pipeline.Id == buildDefinition.Id).FirstOrDefaultAsync();

                    if (existingRepo?.Pipeline.Last.Changed.ToUniversalTime() == buildDefinition.LatestBuild.LastChangedDate.ToUniversalTime())
                    {
                        _logger.LogInformation($"Pipeline {buildDefinition.Name} has not changed. Skipping.");
                        return;
                    }

                    _logger.LogInformation($"Pipeline {buildDefinition.Name} has changed. Updating build info.");
                }
                else
                {
                    var actualBuild = await _repositoryDatabase.Query().Where(x => x.Id == repo.Id).FirstOrDefaultAsync();
                    if (actualBuild != null)
                    {
                        _logger.LogInformation($"Repo {repo.Name} has no pipeline/build. Skipping.");
                        return;
                    }
                }

                var buildInfo = await CreateBuildInfoAsync(project, repo, buildDefinition);
                await UpsertAndPublish(buildInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing repo {repo.Name}");
            }
        }

        public async Task<List<Repository>> GetBuildInfoAsync(string filter = null)
        {
            var query = _repositoryDatabase.Query();

            if (!string.IsNullOrEmpty(filter))
            {
                query = query.Where(x => x.Project.ToUpper().Contains(filter.Trim().ToUpper())
                                          || x.Name.ToUpper().Contains(filter.Trim().ToUpper())
                                          || x.Pipeline.Last.Commit.AuthorName.ToUpper().Contains(filter.Trim().ToUpper()));
            }

            var results = await query.ToListAsync();
            var ordered = results.AsQueryable().OrderByDescending(GetLatestBuildDetailsExpression()).ToList();

            return ordered;
        }

        public async Task<Repository> GetBuildInfoByIdAsync(Guid id)
        {
            return await _repositoryDatabase.Query().Where(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<string> GetBuildErrorLogsAsync(int buildId)
        {
            var build = await _repositoryDatabase.Query().Where(x => x.Pipeline.Last.Id == buildId).Select(x => x.Pipeline.Last).FirstOrDefaultAsync();
            return await Task.FromResult(build?.ErrorLogs);
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
            var fetchTasks = repos.Select(repo => FetchRepoBuildInfoAsync(project, repo));
            await Task.WhenAll(fetchTasks);
        }

        public async Task FetchBuildInfoByIdAsync(Guid repoId)
        {
            var repoAtual = await _repositoryDatabase.FindByIdAsync(repoId);
            if (repoAtual == null)
            {
                _logger.LogInformation($"Repository with ID {repoId} not found.");
                return;
            }

            var repo = await _gitClient.GetRepositoryAsync(repoId);

            try
            {
                _logger.LogInformation($"Fetching info for repository: {repoAtual.Name}");

                var project = await _projectClient.GetProject(repoAtual.Project);

                if (project == null)
                {
                    _logger.LogInformation($"Project {repoAtual.Project} not found in Azure DevOps.");
                    return;
                }

                var buildDefinitions = await _buildClient.GetDefinitionsAsync(project.Name, repositoryId: repoAtual.Id.ToString(), repositoryType: RepositoryTypes.TfsGit, includeLatestBuilds: true);
                var buildDefinition = buildDefinitions.FirstOrDefault();

                if (buildDefinition?.LatestBuild != null)
                {
                    _logger.LogInformation($"Updating build info for repository: {repoAtual.Name}");
                    var updatedBuildInfo = await CreateBuildInfoAsync(project, repo, buildDefinition);
                    await UpsertAndPublish(updatedBuildInfo);
                }
                else
                {
                    _logger.LogInformation($"No latest build found for repository: {repoAtual.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching build info for repository {repoAtual.Name}");
            }
        }

        public async Task<Repository> CreateBuildInfoAsync(TeamProjectReference project, GitRepository repo, BuildDefinitionReference buildDefinition)
        {
            var buildInfo = new Repository
            {
                Id = repo.Id,
                Project = project.Name,
                Name = repo.Name,
                MasterClonned = Directory.Exists(Path.Combine(_localCloneFolder, project.Name, repo.Name)),
                Url = repo.WebUrl,
                CloneUrl = repo.RemoteUrl,
                Pipeline = buildDefinition != null ? new Pipeline { Id = buildDefinition.Id } : null
            };

            if (buildDefinition?.LatestBuild != null)
            {
                var buildDetails = await _buildClient.GetBuildAsync(project.Name, buildDefinition.LatestBuild.Id);
                buildInfo.Pipeline.Last = new Build
                {
                    Id = buildDetails.Id,
                    Changed = buildDetails.LastChangedDate,
                    Queued = buildDetails.QueueTime,
                    Result = buildDetails.Result?.ToString(),
                    Status = buildDetails.Status?.ToString(),
                    Url = (buildDetails.Links.Links["web"] as ReferenceLink).Href
                };

                await FetchCommitInfoAsync(buildInfo, project.Name, buildInfo.Id, buildDetails.SourceVersion);

                if (buildDetails.Result == BuildResult.Failed)
                {
                    buildInfo.Pipeline.Last.ErrorLogs = await FetchBuildLogsAsync(project.Name, buildDetails.Id);
                }

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
                content.AppendLine(string.Join("\n", logLines));
            }

            _logger.LogInformation($"Got logs for Build ID {buildId}");
            return content.ToString();
        }

        private async Task FetchCommitInfoAsync(Repository buildInfo, string projectName, Guid repoId, string commitId)
        {
            try
            {
                var commit = await _gitClient.GetCommitAsync(projectName, commitId, repoId);
                buildInfo.Pipeline.Last.Commit = new Commit
                {
                    Id = commit.CommitId,
                    Message = commit.Comment,
                    Url = commit.RemoteUrl,
                    AuthorName = commit.Author.Name,
                    AuthorEmail = commit.Author.Email
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
            var buildInfos = (await _repositoryDatabase.FindAllAsync()).ToList();

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
                _logger.LogInformation($"BuildInfo with ID {buildInfoId} not found");
            }
        }

        public async Task CloneRepositoryByBuildInfoAsync(Repository buildInfo)
        {
            var localPath = Path.Combine(_localCloneFolder, buildInfo.Project, buildInfo.Name);

            if (Directory.Exists(localPath))
            {
                _logger.LogInformation($"Repository {buildInfo.Name} already cloned to {localPath}");
                buildInfo.MasterClonned = true;
                await UpsertAndPublish(buildInfo);
                return;
            }

            try
            {
                var repo = await _gitClient.GetRepositoryAsync(buildInfo.Project, buildInfo.Id);
                if (repo != null)
                {
                    Directory.CreateDirectory(localPath);
                    var cloneOptions = new CloneOptions
                    {
                        Checkout = true
                    };

                    cloneOptions.FetchOptions.CertificateCheck = (cert, valid, host) => true;
                    cloneOptions.FetchOptions.CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = "Anything", Password = _privateToken };

                    LibGit2Sharp.Repository.Clone(repo.RemoteUrl, localPath, cloneOptions);

                    buildInfo.MasterClonned = true;
                    await UpsertAndPublish(buildInfo);

                    _logger.LogInformation($"Repository {repo.Name} cloned to {localPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cloning repository {buildInfo.Name}");
            }
        }

        public void OpenFolder(string localPath)
        {
            if (Directory.Exists(localPath))
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = localPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
        }

        public async Task OpenProjectByBuildInfoIdAsync(Guid buildInfoId)
        {
            var buildInfo = await _repositoryDatabase.FindByIdAsync(buildInfoId);

            if (buildInfo != null)
            {
                var localPath = Path.Combine(_localCloneFolder, buildInfo.Project, buildInfo.Name);
                OpenProject(localPath);
            }
            else
            {
                _logger.LogInformation($"BuildInfo with ID {buildInfoId} not found");
            }
        }

        public string FindSolutionFile(string folderPath)
        {
            // Search in the top directory
            string slnFile = Directory.GetFiles(folderPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (slnFile == null)
            {
                // Search in the src folder if the solution file wasn't found in the top directory
                string srcFolderPath = Path.Combine(folderPath, "src");
                if (Directory.Exists(srcFolderPath))
                {
                    slnFile = Directory.GetFiles(srcFolderPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
                }
            }

            return slnFile;
        }

        private void OpenProject(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                var slnFile = FindSolutionFile(folderPath);

                if (slnFile != null)
                {
                    OpenWithVisualStudio(slnFile);
                }
                else if (Directory.GetDirectories(folderPath, "src", SearchOption.AllDirectories).Any())
                {
                    OpenWithVSCode(folderPath);
                }
                else
                {
                    OpenFolder(folderPath);
                }
            }
            else
            {
                _logger.LogInformation($"Directory {folderPath} does not exist.");
            }
        }

        private void OpenWithVisualStudio(string slnFile)
        {
            try
            {
                var requiresAdmin = SolutionContainsTopshelf(slnFile);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "devenv.exe",
                    Arguments = $"\"{slnFile}\"",
                    UseShellExecute = true,
                    Verb = requiresAdmin ? "runas" : ""
                };

                System.Diagnostics.Process.Start(processStartInfo);
                _logger.LogInformation($"Opening {slnFile} with Visual Studio{(requiresAdmin ? " as Administrator" : "")}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error opening {slnFile} with Visual Studio");
            }
        }

        private bool SolutionContainsTopshelf(string slnFile)
        {
            var solutionDirectory = Path.GetDirectoryName(slnFile);
            var projectFiles = Directory.GetFiles(solutionDirectory, "*.csproj", SearchOption.AllDirectories);

            return projectFiles.Any(ProjectContainsTopshelfReference);
        }

        private bool ProjectContainsTopshelfReference(string projectFile)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(projectFile);

            var references = xmlDoc.GetElementsByTagName("Reference");
            if (references.Cast<XmlNode>().Any(node => node.Attributes["Include"]?.Value.Contains("Topshelf") == true))
                return true;

            var packageReferences = xmlDoc.GetElementsByTagName("PackageReference");
            return packageReferences.Cast<XmlNode>().Any(node => node.Attributes["Include"]?.Value == "Topshelf");
        }

        private void OpenWithVSCode(string folderPath)
        {
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = "code-insiders.cmd",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                _logger.LogInformation($"Opening {folderPath} with VS Code.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error opening {folderPath} with VS Code");
            }
        }

        public async Task OpenCloneFolderInVsCode()
        {
            OpenWithVSCode(_localCloneFolder);
        }

        private async Task UpsertAndPublish(Repository buildInfo)
        {
            await _repositoryDatabase.UpsertAsync(buildInfo);
            await _hubContext.Clients.All.SendAsync("Update", buildInfo.Id);
        }

        public async Task Delete(Guid id)
        {
            await _repositoryDatabase.DeleteAsync(id);
            await _hubContext.Clients.All.SendAsync("Update", id);
        }

        public async Task<string> GenerateCloneCommands()
        {
            var buildInfos = (await _repositoryDatabase.FindAllAsync()).ToList();
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
    }
}
