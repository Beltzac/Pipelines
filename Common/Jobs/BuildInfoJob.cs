using Common.Jobs;
using Common.Services;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Abstractions;
using Quartz;

public class BuildInfoJob : IJob
{
    private readonly IBuildInfoService _buildInfoService;
    private readonly ILogger<BuildInfoJob> _logger;
    private TelemetryClient _telemetryClient;

    public BuildInfoJob(IBuildInfoService buildInfoService, ILogger<BuildInfoJob> logger, TelemetryClient telemetryClient)
    {
        _buildInfoService = buildInfoService;
        _logger = logger;
        _telemetryClient = telemetryClient;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using (_telemetryClient.StartOperation<RequestTelemetry>("criar-jobs-update"))
        {
            var repos = await _buildInfoService.GetBuildInfoAsync();

            // For each repository, schedule a job to update the repository with a random start time
            foreach (var repo in repos)
            {
                var secondsToNextUpdate = repo.SecondsToNextUpdate();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity($"RepositoryUpdateTrigger-{repo.Id}")
                    .StartAt(DateTime.UtcNow.AddSeconds(secondsToNextUpdate))
                    .Build();

                var job = JobBuilder.Create<RepositoryUpdateJob>()
                    .WithIdentity($"RepositoryUpdateJob-{repo.Id}")
                    .UsingJobData("RepositoryId", repo.Id.ToString())
                    .Build();

                // Check if the trigger already exists
                if (await context.Scheduler.CheckExists(trigger.Key))
                {
                    _logger.LogInformation($"Trigger {trigger.Key} already exists, skipping job creation");
                    continue;
                }

                await context.Scheduler.ScheduleJob(job, trigger);

                _logger.LogInformation($"Scheduled RepositoryUpdateJob for repository {repo.Name} in {secondsToNextUpdate} seconds");
            }

            _telemetryClient.TrackEvent("Job Creation Completed");
        }
    }
}
