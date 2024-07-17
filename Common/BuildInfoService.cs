using Common;
using LiteDB;
using Microsoft.AspNetCore.SignalR;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System.Linq.Expressions;

namespace BuildInfoBlazorApp.Data
{
    public class BuildInfoService
    {
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

            string azureDevOpsOrganizationUrl = "https://dev.azure.com/terminal-cp";
            string personalAccessToken = "2hthfevn4ba7ftrkkjpajj4h5rcej56oje6reabjnupqcwxfzhdq";

            _connection = new VssConnection(new Uri(azureDevOpsOrganizationUrl), new VssBasicCredential(string.Empty, personalAccessToken));
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

        public async Task<BuildInfo> CreateBuildInfoAsync(string projectName, BuildDefinitionReference definition)
        {
            var buildInfo = new BuildInfo
            {
                Id = definition.Id,
                Project = BsonMapper.Global.ToDocument(projectName),
                Pipeline = BsonMapper.Global.ToDocument(definition),
            };

            var latestBuild = definition.LatestBuild;
            if (latestBuild != null)
            {
                var buildDetails = await _buildClient.GetBuildAsync(projectName, latestBuild.Id);
                buildInfo.LatestBuildDetails = BsonMapper.Global.ToDocument(buildDetails);

                await FetchCommitInfoAsync(projectName, buildDetails, buildInfo);

                if (latestBuild.Result == BuildResult.Failed)
                {
                    buildInfo.ErrorLogs = await FetchBuildLogsAsync(projectName, latestBuild.Id);
                }

                Console.WriteLine($"\tPipeline: {definition.Name}, Latest Build: {latestBuild.FinishTime}, Status: {latestBuild.Status}, Result: {latestBuild.Result}, Commit: {buildDetails.SourceVersion}");
            }
            else
            {
                Console.WriteLine($"\tPipeline: {definition.Name} has no latest build.");
            }

            var latestCompletedBuild = definition.LatestCompletedBuild;
            if (latestCompletedBuild != null)
            {
                Console.WriteLine($"\tLatest Completed Build: {latestCompletedBuild.FinishTime}, Status: {latestCompletedBuild.Status}, Result: {latestCompletedBuild.Result}");
            }
            else
            {
                Console.WriteLine($"\tPipeline: {definition.Name} has no latest completed build.");
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
                var buildInfo = await CreateBuildInfoAsync(project.Name, definition);
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
    }
}
