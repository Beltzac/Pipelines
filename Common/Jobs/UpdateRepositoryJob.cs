using Common.Services.Interfaces;
using Common.Utils;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Quartz;

namespace Common.Jobs
{
    public class UpdateRepositoryJob : IJob
    {
        private readonly IRepositoryService _buildInfoService;
        private readonly ILogger<CreateRepositoriesJobsJob> _logger;
        private TelemetryClient _telemetryClient;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly IConfigurationService _configurationService;

        public UpdateRepositoryJob(IRepositoryService buildInfoService, ILogger<CreateRepositoriesJobsJob> logger, TelemetryClient telemetryClient, IConfigurationService configurationService)
        {
            _buildInfoService = buildInfoService;
            _logger = logger;
            _telemetryClient = telemetryClient;

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Retry {retryCount} encountered an error: {exception.Message}. Waiting {timeSpan} before next retry.");
                    });

            _configurationService = configurationService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            using (_telemetryClient.StartOperation<RequestTelemetry>("repository-update-job"))
            {
                if (string.IsNullOrEmpty(_configurationService.GetConfig().PAT))
                    return;

                var repositoryId = Guid.Parse(context.MergedJobDataMap.GetString("RepositoryId"));

                var nextRun = 60;
                Repository? repo = null;

                try
                {
                    repo = await _buildInfoService.FetchRepoBuildInfoAsync(repositoryId);

                    if (repo == null)
                    {
                        _logger.LogWarning($"Repository {repositoryId} not found");
                        return;
                    }

                    nextRun = repo.SecondsToNextUpdate();

                    _logger.LogInformation($"Updating repository {repo.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error updating repository {repositoryId}");
                }

                context.JobDetail.JobDataMap["RepositoryId"] = repositoryId;

                var trigger = TriggerBuilder.Create()
                    .WithIdentity(context.Trigger.Key)
                    .StartAt(DateTime.UtcNow.AddSeconds(nextRun))
                    .Build();

                await _retryPolicy.ExecuteAsync(async () =>
                {
                    await context.Scheduler.RescheduleJob(context.Trigger.Key, trigger);
                });

                _logger.LogInformation($"Rescheduled RepositoryUpdateJob for repository {repo?.Name ?? "MISSING"} in {nextRun} seconds");

                _telemetryClient.TrackEvent("Update Completed");
            }
        }
    }
}
