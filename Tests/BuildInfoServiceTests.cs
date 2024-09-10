using BuildInfoBlazorApp.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Common.Tests
{
    public class BuildInfoServiceTests
    {
        private readonly BuildInfoService _buildInfoService;

        public BuildInfoServiceTests()
        {
            ILogger<BuildInfoService> logger = new NullLogger<BuildInfoService>();
            var hubContext = Mock.Of<IHubContext<BuildInfoHub>>();
            var configService = Mock.Of<ConfigurationService>();
            _buildInfoService = new BuildInfoService(hubContext, logger, configService);
        }

        //[Fact]
        //public async Task FetchBuildInfoAsync_ReturnsData()
        //{
        //    await _buildInfoService.FetchBuildInfoAsync();
        //}
    }
}
