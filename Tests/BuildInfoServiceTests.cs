using System;
using System.Threading.Tasks;
using Xunit;

namespace BuildInfoBlazorApp.Tests
{
    public class BuildInfoServiceTests
    {
        private readonly BuildInfoService _buildInfoService;

        public BuildInfoServiceTests()
        {
            _buildInfoService = new BuildInfoService();
        }

        [Fact]
        public async Task FetchBuildInfoAsync_ReturnsData()
        {
            var result = await _buildInfoService.FetchBuildInfoAsync();

            Assert.NotNull(result);
            // Add more assertions based on the expected structure of the result
        }
    }
}
