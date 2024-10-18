using Common.Services;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Common.Jobs
{
    public class RepositoryUpdateJob : IJob
    {
        private readonly IBuildInfoService _buildInfoService;
        private readonly ILogger<BuildInfoJob> _logger;
        private TelemetryClient _telemetryClient;

        public RepositoryUpdateJob(IBuildInfoService buildInfoService, ILogger<BuildInfoJob> logger, TelemetryClient telemetryClient)
        {
            _buildInfoService = buildInfoService;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            using (_telemetryClient.StartOperation<RequestTelemetry>("repository-update-job"))
            {
                var repositoryId = Guid.Parse(context.MergedJobDataMap.GetString("RepositoryId"));


                var nextRun = 0;
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

                await context.Scheduler.RescheduleJob(context.Trigger.Key, trigger);

                _logger.LogInformation($"Rescheduled RepositoryUpdateJob for repository {repo.Name} in {nextRun} seconds");

                _telemetryClient.TrackEvent("Update Completed");
            }
        }
    }
}
