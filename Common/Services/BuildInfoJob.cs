using Common.Services;
using Microsoft.Extensions.Logging;
using Quartz;

public class BuildInfoJob : IJob
{
    private readonly IBuildInfoService _buildInfoService;
    private readonly ILogger<BuildInfoJob> _logger;

    public BuildInfoJob(IBuildInfoService buildInfoService, ILogger<BuildInfoJob> logger)
    {
        _buildInfoService = buildInfoService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var repos = await _buildInfoService.GetBuildInfoAsync();

        // For each repository, schedule a job to update the repository with a random start time
        foreach (var repo in repos)
        {
            var job = JobBuilder.Create<RepositoryUpdateJob>()
                .WithIdentity($"RepositoryUpdateJob-{repo.Id}")
                .UsingJobData("RepositoryId", repo.Id)
                .Build();

            var secondsToNextUpdate = repo.SecondsToNextUpdate();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"RepositoryUpdateTrigger-{repo.Id}")
                .StartAt(DateTime.UtcNow.AddSeconds(secondsToNextUpdate))
                .Build();

            await context.Scheduler.ScheduleJob(job, trigger);

            _logger.LogInformation($"Scheduled RepositoryUpdateJob for repository {repo.Name} in {secondsToNextUpdate} seconds");
        }
    }
}
