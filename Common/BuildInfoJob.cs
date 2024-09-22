using BuildInfoBlazorApp.Data;
using Microsoft.Extensions.Logging;
using Quartz;

public class BuildInfoJob : IJob
{
    private readonly BuildInfoService _buildInfoService;
    private readonly ILogger<BuildInfoJob> _logger;

    public BuildInfoJob(BuildInfoService buildInfoService, ILogger<BuildInfoJob> logger)
    {
        _buildInfoService = buildInfoService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        // Your existing code to fetch and process build information
        try
        {
            await _buildInfoService.FetchBuildInfoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex.Message);
        }
    }
}
