using Common.Jobs;
using Common.Services.Interfaces;
using Common.Utils;
using Microsoft.Extensions.Logging;
using Quartz;

public class CreateRepositoriesJobsJob : IJob
{
    private readonly IRepositoryService _buildInfoService;
    private readonly ILogger<CreateRepositoriesJobsJob> _logger;
    private readonly IConfigurationService _configurationService;

    public CreateRepositoriesJobsJob(IRepositoryService buildInfoService, ILogger<CreateRepositoriesJobsJob> logger, IConfigurationService configurationService)
    {
        _buildInfoService = buildInfoService;
        _logger = logger;
        _configurationService = configurationService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            if (string.IsNullOrEmpty(_configurationService.GetConfig().PAT))
                return;

            var repos = await _buildInfoService.FetchProjectsRepos();

            // For each repository, schedule a job to update the repository with a random start time
            foreach (var repoId in repos)
            {
                var trigger = TriggerBuilder.Create()
                    .WithIdentity($"RepositoryUpdateTrigger-{repoId}")
                    .StartNow()
                    .Build();

                var job = JobBuilder.Create<UpdateRepositoryJob>()
                    .WithIdentity($"RepositoryUpdateJob-{repoId}")
                    .UsingJobData("RepositoryId", repoId.ToString())
                    .Build();

                // If trigger exists, reschedule it with updated timing
                if (await context.Scheduler.CheckExists(trigger.Key))
                {
                    var existingTrigger = await context.Scheduler.GetTrigger(trigger.Key);
                    var buildInfo = await _buildInfoService.GetBuildInfoByIdAsync(repoId);
                    var nextRun = buildInfo.SecondsToNextUpdate();

                    var newTrigger = TriggerBuilder.Create()
                        .WithIdentity(trigger.Key)
                        .StartAt(DateTime.UtcNow.AddSeconds(nextRun))
                        .Build();

                    await context.Scheduler.RescheduleJob(trigger.Key, newTrigger);
                    _logger.LogInformation($"Rescheduled RepositoryUpdateJob for repository {repoId} in {nextRun} seconds");
                }
                else
                {
                    await context.Scheduler.ScheduleJob(job, trigger);
                    _logger.LogInformation($"Scheduled RepositoryUpdateJob for repository {repoId} now");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating jobs");
        }
    }
}
