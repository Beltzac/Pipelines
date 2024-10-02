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
        var guids = await _buildInfoService.FetchReposGuids();

        // For each repository, schedule a job to update the repository with a random start time
        foreach (var guid in guids)
        {
            var job = JobBuilder.Create<RepositoryUpdateJob>()
                .WithIdentity($"RepositoryUpdateJob-{guid}")
                .UsingJobData("RepositoryId", guid)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"RepositoryUpdateTrigger-{guid}")
                .StartAt(DateTime.UtcNow.AddSeconds(new Random().Next(60)))
                .Build();

            await context.Scheduler.ScheduleJob(job, trigger);

            _logger.LogInformation($"Scheduled RepositoryUpdateJob for repository {guid}");
        }
    }
}
