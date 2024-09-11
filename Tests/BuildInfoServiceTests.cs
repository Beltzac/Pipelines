using BuildInfoBlazorApp.Data;
using LiteDB.Async;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Common.Tests
{
    public class BuildInfoServiceTests
    {
        private readonly Mock<IHubContext<BuildInfoHub>> _hubContextMock;
        private readonly Mock<ILogger<BuildInfoService>> _loggerMock;
        private readonly Mock<IConfigurationService> _configServiceMock;
        private readonly Mock<ILiteDatabaseAsync> _liteDatabaseMock;
        private readonly Mock<IBuildHttpClient> _buildClientMock;
        private readonly Mock<IProjectHttpClient> _projectClientMock;
        private readonly Mock<IGitHttpClient> _gitClientMock;
        private readonly BuildInfoService _buildInfoService;

        public BuildInfoServiceTests()
        {
            _hubContextMock = new Mock<IHubContext<BuildInfoHub>>();
            _loggerMock = new Mock<ILogger<BuildInfoService>>();
            _configServiceMock = new Mock<IConfigurationService>();
            _liteDatabaseMock = new Mock<ILiteDatabaseAsync>();
            _buildClientMock = new Mock<IBuildHttpClient>();
            _projectClientMock = new Mock<IProjectHttpClient>();
            _gitClientMock = new Mock<IGitHttpClient>();

            var configModel = new ConfigModel
            {
                LocalCloneFolder = "some/path",
                PAT = "someToken"
            };
            _configServiceMock.Setup(cs => cs.GetConfig()).Returns(configModel);

            _buildInfoService = new BuildInfoService(
                _hubContextMock.Object,
                _loggerMock.Object,
                _configServiceMock.Object,
                _liteDatabaseMock.Object,
                _buildClientMock.Object,
                _projectClientMock.Object,
                _gitClientMock.Object);
        }

        [Fact]
        public async Task GetBuildInfoAsync_ShouldReturnRepositories()
        {
            // Arrange
            var expectedRepositories = new List<Repository>
            {
                new Repository { Id = Guid.NewGuid(), Name = "Repo1" },
                new Repository { Id = Guid.NewGuid(), Name = "Repo2" }
            };

            var reposCollectionMock = new Mock<ILiteCollectionAsync<Repository>>();
            var liteDatabaseAsyncMock = new Mock<LiteDatabaseAsync>();
            reposCollectionMock.Setup(c => c.Query()).Returns(new LiteQueryableAsync<Repository>(expectedRepositories.AsQueryable(), liteDatabaseAsyncMock.Object));
            _liteDatabaseMock.Setup(db => db.GetCollection<Repository>("repos")).Returns(reposCollectionMock.Object);
            reposCollectionMock.Setup(c => c.FindAllAsync()).ReturnsAsync(expectedRepositories);

            // Act
            var result = await _buildInfoService.GetBuildInfoAsync();

            // Assert
            Assert.Equal(expectedRepositories.Count, result.Count);
        }

        // Additional tests...
    }
}
