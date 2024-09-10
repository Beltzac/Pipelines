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
            _buildInfoService = new BuildInfoService();
        }

        [Fact]
        public async Task FetchBuildInfoAsync_ReturnsData()
        {
            var result = await _buildInfoService.FetchBuildInfoAsync();

            Assert.NotNull(result);
            Assert.NotEmpty(result.Builds);
            Assert.All(result.Builds, build => 
            {
                Assert.NotNull(build.Id);
                Assert.NotNull(build.Status);
                Assert.NotNull(build.Result);
            });
        }
    }
}
