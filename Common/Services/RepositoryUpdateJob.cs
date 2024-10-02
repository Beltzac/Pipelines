using Microsoft.Extensions.Logging;
using Quartz;

namespace Common.Services
{
    public class RepositoryUpdateJob : IJob
    {
        private readonly IBuildInfoService _buildInfoService;
        private readonly ILogger<BuildInfoJob> _logger;

        public RepositoryUpdateJob(IBuildInfoService buildInfoService, ILogger<BuildInfoJob> logger)
        {
            _buildInfoService = buildInfoService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var repositoryId = context.MergedJobDataMap.GetGuid("RepositoryId");

            _logger.LogInformation($"Updating repository {repositoryId}");

            DateTime? lastUpdated = null;

            try
            {
                var repo = await _buildInfoService.FetchRepoBuildInfoAsync(repositoryId);
                lastUpdated = repo.Pipeline?.Last?.Changed;
            }
            catch (Exception ex)
            {
                lastUpdated = null;
                _logger.LogError(ex, $"Error updating repository {repositoryId}");
            }

            // Requeue the job, more time if the repository was not updated recently
            // Less if it is more active
            // With a minimum of 60 seconds
            // With a maximum of 3600 seconds
            // And a random offset to stagger the jobs

            var nextRun = Math.Max(60, Math.Min(3600, (int)(DateTime.UtcNow - (lastUpdated ?? DateTime.MinValue)).TotalSeconds)) + new Random().Next(60);

            context.JobDetail.JobDataMap["RepositoryId"] = repositoryId;

            var trigger = TriggerBuilder.Create()
                .WithIdentity(context.Trigger.Key)
                .StartAt(DateTime.UtcNow.AddSeconds(nextRun))
                .Build();

            await context.Scheduler.RescheduleJob(context.Trigger.Key, trigger);

            _logger.LogInformation($"Rescheduled RepositoryUpdateJob for repository {repositoryId} in {nextRun} seconds");
        }
    }
}
