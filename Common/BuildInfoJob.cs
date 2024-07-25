using BuildInfoBlazorApp.Data;
using Quartz;
using System;
using System.Threading.Tasks;

public class BuildInfoJob : IJob
{
    private readonly BuildInfoService _buildInfoService;
    public BuildInfoJob(BuildInfoService buildInfoService)
    {
        _buildInfoService = buildInfoService;
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
            Console.WriteLine(ex.Message);
        }
    }
}
