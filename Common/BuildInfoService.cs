using Common;
using LiteDB;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Pipelines.WebApi;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System.Linq.Expressions;
using LibGit2Sharp;
using System.Diagnostics;

namespace BuildInfoBlazorApp.Data
{
    public class BuildInfoService
    {
        private readonly string _azureDevOpsOrganizationUrl = "https://dev.azure.com/terminal-cp";
        private readonly string _personalAccessToken = "2hthfevn4ba7ftrkkjpajj4h5rcej56oje6reabjnupqcwxfzhdq";
        private readonly string _databasePath = @"C:\Users\Beltzac\Documents\Builds.db";
        private readonly LiteDatabase _liteDatabase;
        private readonly IHubContext<BuildInfoHub> _hubContext;
        private readonly VssConnection _connection;
        private readonly BuildHttpClient _buildClient;
        private readonly ProjectHttpClient _projectClient;
        private readonly GitHttpClient _gitClient;

        public BuildInfoService(IHubContext<BuildInfoHub> hubContext)
        {
            _liteDatabase = new LiteDatabase(_databasePath);
            _hubContext = hubContext;



            _connection = new VssConnection(new Uri(_azureDevOpsOrganizationUrl), new VssBasicCredential(string.Empty, _personalAccessToken));
            _buildClient = _connection.GetClient<BuildHttpClient>();
            _projectClient = _connection.GetClient<ProjectHttpClient>();
            _gitClient = _connection.GetClient<GitHttpClient>();
        }

        public async Task<List<BuildInfo>> GetBuildInfoAsync(string filter = null)
        {
            var buildsCollection = _liteDatabase.GetCollection<BuildInfo>("builds");
            var query = buildsCollection.FindAll().AsQueryable();

            if (!string.IsNullOrEmpty(filter))
            {
                query = query.Where(x => x.Project["Name"].AsString.ToUpper().Contains(filter.Trim().ToUpper())
                                          || x.Pipeline["Name"].AsString.ToUpper().Contains(filter.Trim().ToUpper()));
            }

            var results = query.OrderByDescending(GetLatestBuildDetailsExpression()).ToList();
            return await Task.FromResult(results);
        }

        public async Task<BuildInfo> GetBuildInfoByIdAsync(int id)
        {
            var collection = _liteDatabase.GetCollection<BuildInfo>("builds");
            var query = collection.FindAll().AsQueryable();
            return await Task.FromResult(query.FirstOrDefault(x => x.Id == id));
        }

        public async Task<string> GetBuildErrorLogsAsync(int buildId)
        {
            var buildsCollection = _liteDatabase.GetCollection<BuildInfo>("builds");
            var query = buildsCollection.FindAll().AsQueryable();
            return await Task.FromResult(query.FirstOrDefault(x => x.Id == buildId)?.ErrorLogs);
        }

        public async Task FetchBuildInfoAsync()
        {
            var projects = await _projectClient.GetProjects();
            var buildsCollection = _liteDatabase.GetCollection<BuildInfo>("builds");

            foreach (var project in projects)
            {
                await FetchProjectBuildInfoAsync(buildsCollection, project);
            }
        }

        public async Task<BuildInfo> CreateBuildInfoAsync(TeamProjectReference project, BuildDefinitionReference buildDefinition)
        {
            var buildInfo = new BuildInfo
            {
                Id = buildDefinition.Id,
                Project = BsonMapper.Global.ToDocument(project),
                Pipeline = BsonMapper.Global.ToDocument(buildDefinition),
            };

            var latestBuild = buildDefinition.LatestBuild;
            if (latestBuild != null)
            {
                var buildDetails = await _buildClient.GetBuildAsync(project.Name, latestBuild.Id);
                buildInfo.LatestBuildDetails = BsonMapper.Global.ToDocument(buildDetails);

                await FetchCommitInfoAsync(project.Name, buildDetails, buildInfo);

                if (latestBuild.Result == BuildResult.Failed)
                {
                    buildInfo.ErrorLogs = await FetchBuildLogsAsync(project.Name, latestBuild.Id);
                }

                Console.WriteLine($"\tPipeline: {buildDefinition.Name}, Latest Build: {latestBuild.FinishTime}, Status: {latestBuild.Status}, Result: {latestBuild.Result}, Commit: {buildDetails.SourceVersion}");
            }
            else
            {
                Console.WriteLine($"\tPipeline: {buildDefinition.Name} has no latest build.");
            }

            var latestCompletedBuild = buildDefinition.LatestCompletedBuild;
            if (latestCompletedBuild != null)
            {
                Console.WriteLine($"\tLatest Completed Build: {latestCompletedBuild.FinishTime}, Status: {latestCompletedBuild.Status}, Result: {latestCompletedBuild.Result}");
            }
            else
            {
                Console.WriteLine($"\tPipeline: {buildDefinition.Name} has no latest completed build.");
            }

            return buildInfo;
        }

        public async Task<string> FetchBuildLogsAsync(string projectName, int buildId)
        {
            var logs = await _buildClient.GetBuildLogsAsync(projectName, buildId);
            var content = string.Empty;

            foreach (var log in logs)
            {
                var logLines = await _buildClient.GetBuildLogLinesAsync(projectName, buildId, log.Id);
                content += string.Join("\n", logLines);
            }

            Console.WriteLine($"\tGot logs for Build ID {buildId}");
            return content;
        }

        private async Task FetchProjectBuildInfoAsync(ILiteCollection<BuildInfo> buildsCollection, TeamProjectReference project)
        {
            Console.WriteLine($"Project: {project.Name}");
            var buildDefinitions = await _buildClient.GetDefinitionsAsync(project.Name, includeLatestBuilds: true);

            foreach (var definition in buildDefinitions)
            {
                var buildInfo = await CreateBuildInfoAsync(project, definition);
                buildsCollection.Upsert(buildInfo);
                await _hubContext.Clients.All.SendAsync("Update", buildInfo.Id);
            }
        }

        private async Task FetchCommitInfoAsync(string projectName, Build buildDetails, BuildInfo buildInfo)
        {
            var commitId = buildDetails.SourceVersion;
            try
            {
                var commit = await _gitClient.GetCommitAsync(projectName, commitId, buildDetails.Repository.Id);
                buildInfo.LatestBuildCommit = BsonMapper.Global.ToDocument(commit);
            }
            catch
            {
                // Handle commit fetch exception
            }
        }

        public Expression<Func<BuildInfo, DateTime>> GetLatestBuildDetailsExpression()
        {
            return x => x.LatestBuildDetails == null || x.LatestBuildDetails["QueueTime"] == null || x.LatestBuildDetails["QueueTime"].IsNull
                        ? DateTime.MinValue
                        : x.LatestBuildDetails["QueueTime"].AsDateTime;
        }

        public async Task CloneRepositoryByBuildInfoIdAsync(int buildInfoId)
        {
            var buildsCollection = _liteDatabase.GetCollection<BuildInfo>("builds");
            var buildInfo = buildsCollection.FindById(buildInfoId);

            if (buildInfo != null)
            {
                var projectName = buildInfo.Project["Name"].AsString;
                var repoId = buildInfo.LatestBuildDetails["Repository"]["_id"].AsString;

                // Fetch repository details
                var repository = await _gitClient.GetRepositoryAsync(projectName, repoId);
                if (repository != null)
                {
                    string cloneUrl = repository.RemoteUrl;
                    string localPath = $@"C:\repos\{projectName}\{repository.Name}";

                    // Ensure the directory exists
                    if (!Directory.Exists(localPath))
                    {
                        Directory.CreateDirectory(localPath);
                    }
                    else
                    {
                        OpenFolder(localPath);
                        return;
                    }

                    // Clone options with bypassing certificate check
                    var cloneOptions = new CloneOptions
                    {
                        Checkout = true
                    };

                    cloneOptions.FetchOptions.CertificateCheck = (cert, valid, host) => true;
                    cloneOptions.FetchOptions.CredentialsProvider = (_url, _user, _cred) =>  new UsernamePasswordCredentials { Username = "Anything", Password = _personalAccessToken };

                    // Clone the repository
                    LibGit2Sharp.Repository.Clone(cloneUrl, localPath, cloneOptions);

                    OpenFolder(localPath);

                    Console.WriteLine($"Repository {repository.Name} cloned to {localPath}");
                }
                else
                {
                    Console.WriteLine($"Repository with ID {repoId} not found in project {projectName}");
                }
            }
            else
            {
                Console.WriteLine($"BuildInfo with ID {buildInfoId} not found");
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

        public async Task OpenProjectByBuildInfoIdAsync(int buildInfoId)
        {
            var buildsCollection = _liteDatabase.GetCollection<BuildInfo>("builds");
            var buildInfo = buildsCollection.FindById(buildInfoId);

            if (buildInfo != null)
            {
                var projectName = buildInfo.Project["Name"].AsString;
                var repoName = buildInfo.LatestBuildDetails["Repository"]["Name"].AsString;
               
                string localPath = $@"C:\repos\{projectName}\{repoName}";
                OpenProject(localPath);
            }
            else
            {
                Console.WriteLine($"BuildInfo with ID {buildInfoId} not found");
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
                else
                {
                    OpenWithVSCode(folderPath);
                }
            }
            else
            {
                Console.WriteLine($"Directory {folderPath} does not exist.");
            }
        }

        private void OpenWithVisualStudio(string slnFile)
        {
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = "devenv.exe", // Path to Visual Studio executable
                    Arguments = $"\"{slnFile}\"",
                    UseShellExecute = true
                });
                Console.WriteLine($"Opening {slnFile} with Visual Studio.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening {slnFile} with Visual Studio: {ex.Message}");
            }
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
                Console.WriteLine($"Opening {folderPath} with VS Code.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening {folderPath} with VS Code: {ex.Message}");
            }
        }
    }
}
