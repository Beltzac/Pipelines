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

        public BuildInfoService(IHubContext<BuildInfoHub> hubContext)
        {
            _liteDatabase = new LiteDatabase(_databasePath);
            _hubContext = hubContext;
        }

        public Task<List<BuildInfo>> GetBuildInfoAsync(string filter = null)
        {

            Console.WriteLine("teste");

            var buildsCollection = _liteDatabase.GetCollection<BuildInfo>("builds");
            var query = buildsCollection.FindAll().AsQueryable();

            if (!string.IsNullOrEmpty(filter))
            {
                query = query.Where(x => x.Project["Name"].AsString.ToUpper().Contains(filter.Trim().ToUpper())
                || x.Pipeline["Name"].AsString.ToUpper().Contains(filter.Trim().ToUpper()));
            }

            var results = query.OrderByDescending(GetLatestBuildDetailsExpression()).ToList();

            return Task.FromResult(results);

        }

        public Expression<Func<BuildInfo, DateTime>> GetLatestBuildDetailsExpression() {
            return x => x.LatestBuildDetails == null || x.LatestBuildDetails["QueueTime"] == null || x.LatestBuildDetails["QueueTime"].IsNull ? DateTime.MinValue : x.LatestBuildDetails["QueueTime"].AsDateTime;
        }

        public async Task<BuildInfo> GetBuildInfoAsync(int id)
        {
            var collection = _liteDatabase.GetCollection<BuildInfo>("builds");
            var query = collection.FindAll().AsQueryable();
            return query.FirstOrDefault(x => x.Id == id);
        }

        public async Task<string> GetBuildErrorLogsAsync(int buildId)
        {

            Console.WriteLine("teste");

            var buildsCollection = _liteDatabase.GetCollection<BuildInfo>("builds");
            var query = buildsCollection.FindAll().AsQueryable();

            return query.FirstOrDefault(x => x.Id == buildId)?.ErrorLogs;

        }

        public async Task FetchBuildInfoAsync()
        {
            string azureDevOpsOrganizationUrl = "https://dev.azure.com/terminal-cp";
            string personalAccessToken = "2hthfevn4ba7ftrkkjpajj4h5rcej56oje6reabjnupqcwxfzhdq";

            VssConnection connection = new VssConnection(new Uri(azureDevOpsOrganizationUrl), new VssBasicCredential(string.Empty, personalAccessToken));
            BuildHttpClient buildClient = connection.GetClient<BuildHttpClient>();
            ProjectHttpClient projectClient = connection.GetClient<ProjectHttpClient>();
            GitHttpClient gitClient = connection.GetClient<GitHttpClient>();

            // Get all projects
            var projects = await projectClient.GetProjects();


            var buildsCollection = _liteDatabase.GetCollection<BuildInfo>("builds");

            foreach (var project in projects)
            {
                Console.WriteLine($"Project: {project.Name}");

                // Get all definitions (pipelines) for the current project, including latest build information
                List<BuildDefinitionReference> buildDefinitions = await buildClient.GetDefinitionsAsync(
                    project: project.Name,
                    includeLatestBuilds: true
                );

                foreach (var definition in buildDefinitions)
                {
                    var latestBuild = definition.LatestBuild;
                    var latestCompletedBuild = definition.LatestCompletedBuild;

                    var buildInfo = new BuildInfo
                    {
                        Id = definition.Id,
                        Project = BsonMapper.Global.ToDocument(project),
                        Pipeline = BsonMapper.Global.ToDocument(definition),
                    };


                    if (latestBuild != null)
                    {
                        // Fetch the details of the latest build to get the commit information
                        var buildDetails = await buildClient.GetBuildAsync(project.Name, latestBuild.Id);
                        buildInfo.LatestBuildDetails = BsonMapper.Global.ToDocument(buildDetails);
                        var commitId = buildDetails.SourceVersion;

                        // Fetch the commit details to get the commit message
                        try
                        {
                            var repositoryId = buildDetails.Repository.Id;
                            var commit = await gitClient.GetCommitAsync(project.Name, commitId, repositoryId);
                            buildInfo.LatestBuildCommit = BsonMapper.Global.ToDocument(commit);
                        }
                        catch
                        {
                        }

                        Console.WriteLine($"\tPipeline: {definition.Name}, Latest Build: {latestBuild.FinishTime}, Status: {latestBuild.Status}, Result: {latestBuild.Result}, Commit: {buildDetails.SourceVersion}");

                        // If the build failed, get the logs
                        if (latestBuild.Result == BuildResult.Failed)
                        {
                            var logs = await buildClient.GetBuildLogsAsync(project.Name, latestBuild.Id);

                            var content = "";

                            var lastLog = logs.OrderBy(x => x.CreatedOn);


                            foreach (var log in logs)
                            {
                                var logLines = await buildClient.GetBuildLogLinesAsync(project.Name, latestBuild.Id, log.Id);
                                var logContent = string.Join("\n", logLines);
                                content += string.Join("\n", logLines);
                            }

                            Console.WriteLine($"\tGot logs for Build ID {latestBuild.Id}");

                            buildInfo.ErrorLogs = content;


                        }

                    }
                    else
                    {
                        Console.WriteLine($"\tPipeline: {definition.Name} has no latest build.");
                    }

                    if (latestCompletedBuild != null)
                    {
                        Console.WriteLine($"\tLatest Completed Build: {latestCompletedBuild.FinishTime}, Status: {latestCompletedBuild.Status}, Result: {latestCompletedBuild.Result}");
                    }
                    else
                    {
                        Console.WriteLine($"\tPipeline: {definition.Name} has no latest completed build.");
                    }

                    await _hubContext.Clients.All.SendAsync("ReceiveMessage", $"New build info fetched for project {project.Name}, pipeline {definition.Name}");
                    await _hubContext.Clients.All.SendAsync("Update", buildInfo.Id);

                    buildsCollection.Upsert(buildInfo);

                }
            }

        }
    }
}
