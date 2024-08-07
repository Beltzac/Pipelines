using Common;
using LibGit2Sharp;
using LiteDB;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using System.Xml;

namespace BuildInfoBlazorApp.Data
{
    public class BuildInfoService
    {
        private readonly string _azureDevOpsOrganizationUrl = "https://dev.azure.com/terminal-cp";
        private readonly string _personalAccessToken = "2hthfevn4ba7ftrkkjpajj4h5rcej56oje6reabjnupqcwxfzhdq";
        private readonly string _databasePath = @"Filename=C:\Users\Beltzac\Documents\Builds.db;Connection=shared";
        private readonly LiteDatabase _liteDatabase;
        private readonly IHubContext<BuildInfoHub> _hubContext;
        private readonly VssConnection _connection;
        private readonly BuildHttpClient _buildClient;
        private readonly ProjectHttpClient _projectClient;
        private readonly GitHttpClient _gitClient;
        private readonly ILiteCollection<Repository> _reposCollection;
        private readonly ILogger<BuildInfoService> _logger;

        public BuildInfoService(IHubContext<BuildInfoHub> hubContext, ILogger<BuildInfoService> logger)
        {
            _liteDatabase = new LiteDatabase(_databasePath);
            _reposCollection = _liteDatabase.GetCollection<Repository>("repos");
            _hubContext = hubContext;

            _connection = new VssConnection(new Uri(_azureDevOpsOrganizationUrl), new VssBasicCredential(string.Empty, _personalAccessToken));
            _buildClient = _connection.GetClient<BuildHttpClient>();
            _projectClient = _connection.GetClient<ProjectHttpClient>();
            _gitClient = _connection.GetClient<GitHttpClient>();
            _logger = logger;
        }

        public async Task<List<Repository>> GetBuildInfoAsync(string filter = null)
        {
            var query = _reposCollection.Query();

            if (!string.IsNullOrEmpty(filter))
            {
                query = query.Where(x => x.Project.ToUpper().Contains(filter.Trim().ToUpper())
                                          || x.Name.ToUpper().Contains(filter.Trim().ToUpper()));
            }

            var results = query.ToList();

            var ordered = results.AsQueryable().OrderByDescending(GetLatestBuildDetailsExpression()).ToList();

            return await Task.FromResult(ordered);
        }

        public async Task<Repository> GetBuildInfoByIdAsync(Guid id)
        {
            var query = _reposCollection.Query();
            return await Task.FromResult(query.Where(x => x.Id == id).FirstOrDefault());
        }

        public async Task<string> GetBuildErrorLogsAsync(int buildId)
        {
            var query = _reposCollection.Query();

            var lastBuildsSuccessful = query
                .Select(x => x.Pipeline.LastSuccessful)
                .ToList();

            var lastBuilds = query
                .Select(x => x.Pipeline.Last)
                .ToList();

            var allBuilds = lastBuildsSuccessful.Union(lastBuilds).ToList();

            var build = allBuilds
                .Where(x => x.Id == buildId)
                .FirstOrDefault();

            return await Task.FromResult(build?.ErrorLogs);
        }

        public async Task FetchBuildInfoAsync()
        {
            var projects = await _projectClient.GetProjects();

            foreach (var project in projects)
            {
                await FetchProjectBuildInfoAsync(project);
            }
        }

        public async Task<Repository> CreateBuildInfoAsync(TeamProjectReference project, GitRepository repo, BuildDefinitionReference buildDefinition)
        {
            var buildInfo = new Repository
            {
                Id = repo.Id,
                Project = project.Name,
                Name = repo.Name,
                MasterClonned = false,
                Url = repo.WebUrl,
                CloneUrl = repo.RemoteUrl
            };

            if(buildDefinition != null)
            {

                buildInfo.Pipeline = new Pipeline()
                {
                    Id = buildDefinition.Id,
                };


                var latestBuild = buildDefinition.LatestBuild;
                if (latestBuild != null)
                {
                    var buildDetails = await _buildClient.GetBuildAsync(project.Name, latestBuild.Id);
                    buildInfo.Pipeline.Last = new Build
                    {
                        Id= buildDetails.Id,
                        Changed = buildDetails.LastChangedDate,
                        Queued = buildDetails.QueueTime,
                        Result = buildDetails.Result?.ToString(),
                        Status = buildDetails.Status?.ToString(),
                        Url = (buildDetails.Links.Links["web"] as ReferenceLink).Href
                    };

                    await FetchCommitInfoAsync(buildInfo, project.Name, buildInfo.Id, buildDetails.SourceVersion);

                    if (latestBuild.Result == BuildResult.Failed)
                    {
                        buildInfo.Pipeline.Last.ErrorLogs = await FetchBuildLogsAsync(project.Name, latestBuild.Id);
                    }

                    string localPath = $@"C:\repos\{project.Name}\{buildDefinition.Name}";
                    buildInfo.MasterClonned = Directory.Exists(localPath);

                    _logger.LogInformation($"\tPipeline: {buildDefinition.Name}, Latest Build: {latestBuild.FinishTime}, Status: {latestBuild.Status}, Result: {latestBuild.Result}, Commit: {buildDetails.SourceVersion}");
                }
                else
                {
                    _logger.LogInformation($"\tPipeline: {buildDefinition.Name} has no latest build.");
                }

                var latestCompletedBuild = buildDefinition.LatestCompletedBuild;
                if (latestCompletedBuild != null)
                {
                    _logger.LogInformation($"\tLatest Completed Build: {latestCompletedBuild.FinishTime}, Status: {latestCompletedBuild.Status}, Result: {latestCompletedBuild.Result}");
                }
                else
                {
                    _logger.LogInformation($"\tPipeline: {buildDefinition.Name} has no latest completed build.");
                }
            }

            return buildInfo;
        }

        public async Task<string> FetchBuildLogsAsync(string projectName, int buildId)
        {
            var logs = await _buildClient.GetBuildLogsAsync(projectName, buildId);
            var content = string.Empty;

            if(logs == null) 
            {             
                return content;
            }

            foreach (var log in logs)
            {
                var logLines = await _buildClient.GetBuildLogLinesAsync(projectName, buildId, log.Id);
                content += string.Join("\n", logLines);
            }

            _logger.LogInformation($"\tGot logs for Build ID {buildId}");
            return content;
        }

        private async Task FetchProjectBuildInfoAsync(TeamProjectReference project)
        {
            _logger.LogInformation($"Project: {project.Name}");

            var repos = await _gitClient.GetRepositoriesAsync(project.Id);

            foreach (var repo in repos)
            {
                try
                {
                    _logger.LogInformation($"Repo: {repo.Name}");

                    if (repo.IsDisabled ?? false)
                    {
                        await Delete(repo.Id);
                        _logger.LogInformation($"Repo {repo.Name} is disabled. Deleting.");
                        continue;
                    }

                    var buildDefinitions = await _buildClient.GetDefinitionsAsync(project.Name, repositoryId: repo.Id.ToString(), repositoryType: RepositoryTypes.TfsGit, includeLatestBuilds: true);
                    var buildDefinition = buildDefinitions.FirstOrDefault();

                    //Test if the build definition has changed

                    if (buildDefinition?.LatestBuild != null)
                    {
                        var actualBuild = _reposCollection.Query().Where(x => x.Pipeline.Id == buildDefinition.Id).FirstOrDefault();

                        if (actualBuild?.Pipeline.Last != null)
                        {
                            if (actualBuild.Pipeline.Last.Changed.ToUniversalTime() != buildDefinition.LatestBuild?.LastChangedDate.ToUniversalTime())
                            {
                                _logger.LogInformation($"Pipeline {buildDefinition.Name} has changed. Updating build info.");
                            }
                            else
                            {
                                _logger.LogInformation($"Pipeline {buildDefinition.Name} has not changed. Skipping.");
                                continue;
                            }
                        }
                    }
                    else
                    {
                        // Check if the repo exists in the database
                        var actualBuild = _reposCollection.Query().Where(x => x.Id == repo.Id).FirstOrDefault();
                        if (actualBuild != null)
                        {
                            // Só cadastramos, não tem pipeline
                            _logger.LogInformation($"Repo {repo.Name} has no pipeline/build. Skipping.");
                            continue;
                        }
                    }

                    var buildInfo = await CreateBuildInfoAsync(project, repo, buildDefinition);
                    await UpsertAndPublish(buildInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar repo {Repo}", repo.Name);
                }
            }
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
            catch(Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter commit {Id}", commitId);
            }
        }

        public Expression<Func<Repository, DateTime>> GetLatestBuildDetailsExpression()
        {
            return x => x.Pipeline == null || x.Pipeline.Last == null 
                        ? DateTime.MinValue
                        : x.Pipeline.Last.Changed;
        }

        public async Task CloneAllRepositoriesAsync()
        {
            var buildInfos = _reposCollection.FindAll().ToList();

            foreach (var buildInfo in buildInfos)
            {
                await CloneRepositoryByBuildInfoAsync(buildInfo);
            }
        }

        public async Task CloneRepositoryByBuildInfoIdAsync(Guid buildInfoId)
        {
            var buildInfo = _reposCollection.FindById(buildInfoId);
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
            var projectName = buildInfo.Project;
            var repoId = buildInfo.Id;
            var repoName = buildInfo.Name;

            if (repoId == null) {                    
                _logger.LogInformation($"Repository ID not found in build info {buildInfo.Id}");
                return;
            }

            string localPath = $@"C:\repos\{projectName}\{repoName}";

            var exists = Directory.Exists(localPath);

            if (exists)
            {
                _logger.LogInformation($"Repository {repoName} already cloned to {localPath}");
                buildInfo.MasterClonned = exists;
                await UpsertAndPublish(buildInfo);
                return;
            }

            // Fetch repository details
            GitRepository? repository;
            try
            {
                repository = await _gitClient.GetRepositoryAsync(projectName, repoId);
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error fetching repository {repoId}: {ex.Message}");
                return;
            }
                
            if (repository != null)
            {
                string cloneUrl = repository.RemoteUrl;
                Directory.CreateDirectory(localPath);

                // Clone options with bypassing certificate check
                var cloneOptions = new CloneOptions
                {
                    Checkout = true
                };

                cloneOptions.FetchOptions.CertificateCheck = (cert, valid, host) => true;
                cloneOptions.FetchOptions.CredentialsProvider = (_url, _user, _cred) =>  new UsernamePasswordCredentials { Username = "Anything", Password = _personalAccessToken };

                // Clone the repository
                try
                {
                    LibGit2Sharp.Repository.Clone(cloneUrl, localPath, cloneOptions);

                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"Error cloning repository {repository.Name}: {ex.Message}");
                    return;
                }
                    
                buildInfo.MasterClonned = true;

                await UpsertAndPublish(buildInfo);

                _logger.LogInformation($"Repository {repository.Name} cloned to {localPath}");
            }
            else
            {
                _logger.LogInformation($"Repository with ID {repoId} not found in project {projectName}");
            }
        }

        public void OpenFolder(string localPath)
        {
            if (Directory.Exists(localPath))
            {
                // Open the cloned folder in File Explorer
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
            var buildInfo = _reposCollection.FindById(buildInfoId);

            if (buildInfo != null)
            {
                var projectName = buildInfo.Project;
                var repoName = buildInfo.Name;
               
                string localPath = $@"C:\repos\{projectName}\{repoName}";
                OpenProject(localPath);
            }
            else
            {
                _logger.LogInformation($"BuildInfo with ID {buildInfoId} not found");
            }
        }

        public void OpenProject(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                var slnFile = Directory.GetFiles(folderPath, "*.sln", SearchOption.AllDirectories).FirstOrDefault();

                if (slnFile != null)
                {
                    OpenWithVisualStudio(slnFile);
                }
                // Check if the folder contains a src folder any level deep
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
                bool requiresAdmin = SolutionContainsTopshelf(slnFile);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "devenv.exe", // Path to Visual Studio executable
                    Arguments = $"\"{slnFile}\"",
                    UseShellExecute = true
                };

                if (requiresAdmin)
                {
                    processStartInfo.Verb = "runas"; // Run as administrator
                }

                System.Diagnostics.Process.Start(processStartInfo);
                _logger.LogInformation($"Opening {slnFile} with Visual Studio{(requiresAdmin ? " as Administrator" : string.Empty)}.");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error opening {slnFile} with Visual Studio: {ex.Message}");
            }
        }

        private bool SolutionContainsTopshelf(string slnFile)
        {
            // Get the directory containing the solution file
            string solutionDirectory = Path.GetDirectoryName(slnFile);

            // Get all .csproj files in the solution directory and subdirectories
            var projectFiles = Directory.GetFiles(solutionDirectory, "*.csproj", SearchOption.AllDirectories);

            foreach (var projectFile in projectFiles)
            {
                if (ProjectContainsTopshelfReference(projectFile))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ProjectContainsTopshelfReference(string projectFile)
        {
            // Load the .csproj file as an XML document
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(projectFile);

            // Check for any Reference elements that mention Topshelf
            XmlNodeList referenceNodes = xmlDoc.GetElementsByTagName("Reference");

            foreach (XmlNode node in referenceNodes)
            {
                if (node.Attributes["Include"] != null && node.Attributes["Include"].Value.Contains("Topshelf"))
                {
                    return true;
                }
            }

            // Optionally, check for PackageReference as well if using newer .NET projects
            XmlNodeList packageReferenceNodes = xmlDoc.GetElementsByTagName("PackageReference");

            foreach (XmlNode node in packageReferenceNodes)
            {
                if (node.Attributes["Include"] != null && node.Attributes["Include"].Value == "Topshelf")
                {
                    return true;
                }
            }

            return false;
        }

        private void OpenWithVSCode(string folderPath)
        {
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = "code-insiders.cmd", // Path to VS Code executable
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                _logger.LogInformation($"Opening {folderPath} with VS Code.");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error opening {folderPath} with VS Code: {ex.Message}");
            }
        }

        public async Task OpenCloneFolderInVsCode()
        {
            string localPath = $@"C:\repos";
            OpenWithVSCode(localPath);
        }

        private async Task UpsertAndPublish(Repository buildInfo)
        {
            _reposCollection.Upsert(buildInfo);
            await _hubContext.Clients.All.SendAsync("Update", buildInfo.Id);
        }

        public async Task Delete(Guid id)
        {
            _reposCollection.Delete(id);
            await _hubContext.Clients.All.SendAsync("Update", id);
        }

        public async Task DownloadConsul()
        {
            string consulUrl = "https://consul-qa.tcp.com.br/v1/kv/?recurse";
            string downloadFolder = "C:\\ConsulKV"; // Change this to your desired download folder

            if (!Directory.Exists(downloadFolder))
            {
                Directory.CreateDirectory(downloadFolder);
            }

            try
            {
                var kvData = await FetchConsulKV(consulUrl);
                await SaveKVToFiles(kvData, downloadFolder);
                _logger.LogInformation("Download completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error: {ex.Message}");
            }
        }

        async Task<JArray> FetchConsulKV(string url)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            return JArray.Parse(responseBody);
        }

        async Task SaveKVToFiles(JArray kvData, string folderPath)
        {
            foreach (var kv in kvData)
            {
                try
                {
                    string key = kv["Key"].ToString();
                    string value = kv["Value"]?.ToString() ?? string.Empty;

                    // Replace "/" with "\" for Windows paths and ensure it does not end with a backslash
                    string filePath = Path.Combine(folderPath, key.Replace("/", "\\"));
                    string directory = Path.GetDirectoryName(filePath);

                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // If the path ends with a slash, treat it as a directory
                    if (kv["Value"] == null)
                    {
                        continue;
                    }

                    byte[] valueBytes = Convert.FromBase64String(value);
                    await File.WriteAllBytesAsync(filePath, valueBytes);

                    _logger.LogInformation($"Saved: {filePath}");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"Error: {ex.Message}");
                }
            }
        }

        public async Task<string> GenerateCloneCommands()
        {
            var buildInfos = _reposCollection.FindAll().ToList();
            var commands = new StringBuilder();

            commands.AppendLine("@echo off");
            commands.AppendLine("set REPO_ROOT=C:\\repos");

            foreach (var buildInfo in buildInfos)
            {
                var projectName = buildInfo.Project;
                var repoId = buildInfo.Id;
                var repoName = buildInfo.Name;

                if (repoId == null || repoName == null)
                {
                    _logger.LogInformation($"Repository ID or Name not found in build info {buildInfo.Id}");
                    continue;
                }

                string localPath = $"%REPO_ROOT%\\{projectName}\\{repoName}";

                // Add check if the repository is already cloned for Windows Command Prompt
                commands.AppendLine($"IF NOT EXIST \"{localPath}\" (");
                commands.AppendLine($"  mkdir \"{localPath}\"");
                try
                {
                    var repository = await _gitClient.GetRepositoryAsync(projectName, repoId);
                    if (repository != null)
                    {
                        string cloneUrl = repository.RemoteUrl.Replace("%", "%%");

                        commands.AppendLine($"  git clone \"{cloneUrl}\" \"{localPath}\"");
                        _logger.LogInformation($"Added clone command for repository {repoName}");
                    }
                    else
                    {
                        _logger.LogInformation($"Repository with ID {repoId} not found in project {projectName}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"Error fetching repository {repoId}: {ex.Message}");
                }
                commands.AppendLine(") ELSE (");
                commands.AppendLine($"  echo \"Repository {repoName} already cloned at {localPath}\"");
                commands.AppendLine(")");
            }

            return commands.ToString();
        }
    }
}
