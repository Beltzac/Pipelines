using BuildInfoBlazorApp.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Common.Tests
{
    public class BuildInfoServiceTests
    {
        private readonly Mock<IHubContext<BuildInfoHub>> _hubContextMock;
        private readonly Mock<ILogger<BuildInfoService>> _loggerMock;
        private readonly Mock<ConfigurationService> _configServiceMock;
        private readonly Mock<LiteDatabaseAsync> _liteDatabaseMock;
        private readonly Mock<BuildHttpClient> _buildClientMock;
        private readonly Mock<ProjectHttpClient> _projectClientMock;
        private readonly Mock<GitHttpClient> _gitClientMock;
        private readonly BuildInfoService _buildInfoService;

        public BuildInfoServiceTests()
        {
            _hubContextMock = new Mock<IHubContext<BuildInfoHub>>();
            _loggerMock = new Mock<ILogger<BuildInfoService>>();
            _configServiceMock = new Mock<ConfigurationService>();
            _liteDatabaseMock = new Mock<LiteDatabaseAsync>();
            _buildClientMock = new Mock<BuildHttpClient>();
            _projectClientMock = new Mock<ProjectHttpClient>();
            _gitClientMock = new Mock<GitHttpClient>();

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
            reposCollectionMock.Setup(c => c.FindAllAsync()).ReturnsAsync(expectedRepositories);
            _liteDatabaseMock.Setup(db => db.GetCollection<Repository>("repos")).Returns(reposCollectionMock.Object);

            // Act
            var result = await _buildInfoService.GetBuildInfoAsync();

            // Assert
            Assert.Equal(expectedRepositories.Count, result.Count);
        }

        // Additional tests...
    }
}
