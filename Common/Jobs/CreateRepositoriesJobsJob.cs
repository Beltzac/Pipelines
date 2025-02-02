using Common.Jobs;
using Common.Services.Interfaces;
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

                // Check if the trigger already exists
                if (await context.Scheduler.CheckExists(trigger.Key))
                {
                    _logger.LogInformation($"Trigger {trigger.Key} already exists, skipping job creation");
                    continue;
                }

                await context.Scheduler.ScheduleJob(job, trigger);

                _logger.LogInformation($"Scheduled RepositoryUpdateJob for repository {repoId} now");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating jobs");
        }
    }
}
