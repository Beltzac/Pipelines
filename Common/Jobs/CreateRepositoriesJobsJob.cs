using Common.Jobs;
using Common.Services.Interfaces;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Quartz;

public class CreateRepositoriesJobsJob : IJob
{
    private readonly IRepositoryService _buildInfoService;
    private readonly ILogger<CreateRepositoriesJobsJob> _logger;
    private TelemetryClient _telemetryClient;

    public CreateRepositoriesJobsJob(IRepositoryService buildInfoService, ILogger<CreateRepositoriesJobsJob> logger, TelemetryClient telemetryClient)
    {
        _buildInfoService = buildInfoService;
        _logger = logger;
        _telemetryClient = telemetryClient;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            using (_telemetryClient.StartOperation<RequestTelemetry>("criar-jobs-update"))
            {
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

                _telemetryClient.TrackEvent("Job Creation Completed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating jobs");
            _telemetryClient.TrackException(ex);
        }     
    }
}
