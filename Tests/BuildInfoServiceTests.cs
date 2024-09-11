using BuildInfoBlazorApp.Data;
using LiteDB;
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
        public async Task GetBuildInfoAsync_ShouldReturnOrderedRepositories_WhenFilterIsApplied()
        {
            // Arrange
            var expectedRepositories = new List<Repository>
            {
                new Repository { Id = Guid.NewGuid(), Name = "Repo1", Project = "Project1", Pipeline = new Pipeline { Last = new Build { Commit = new Commit { AuthorName = "Author1" } } } },
                new Repository { Id = Guid.NewGuid(), Name = "Repo2", Project = "Project2", Pipeline = new Pipeline { Last = new Build { Commit = new Commit { AuthorName = "Author2" } } } }
            };

            // Mock the ILiteCollectionAsync<Repository>
            var reposCollectionMock = new Mock<ILiteCollectionAsync<Repository>>();
            var liteQueryableMock = new Mock<ILiteQueryableAsync<Repository>>();

            // Setup AsQueryable to return a mocked ILiteQueryableAsync
            reposCollectionMock.Setup(c => c.Query()).Returns(liteQueryableMock.Object);

            // Mock Where to filter repositories using BsonExpression (simulating LiteDB behavior)
            liteQueryableMock.Setup(q => q.Where(It.IsAny<BsonExpression>()))
                .Returns(liteQueryableMock.Object);

            // Mock ToListAsync to return the expected repositories ordered by the commit author name
            liteQueryableMock.Setup(q => q.ToListAsync())
                .ReturnsAsync(expectedRepositories.OrderByDescending(r => r.Pipeline.Last.Commit.AuthorName).ToList());

            // Mock the database to return the mocked collection
            _liteDatabaseMock.Setup(db => db.GetCollection<Repository>("repos")).Returns(reposCollectionMock.Object);

            // Act
            var result = await _buildInfoService.GetBuildInfoAsync("Author1");

            // Assert
            Assert.Equal(expectedRepositories.Count, result.Count);
            Assert.Equal("Repo1", result.First().Name); // Check ordering, Repo1 should come first as per filter and sorting
        }




        // Additional tests...
    }
}
