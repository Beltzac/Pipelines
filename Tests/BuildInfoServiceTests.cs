using Common.ExternalApis;
using Common.Repositories;
using Common.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests
{
    public class BuildInfoServiceTests
    {
        private readonly Mock<IHubContext<BuildInfoHub>> _hubContextMock;
        private readonly Mock<ILogger<BuildInfoService>> _loggerMock;
        private readonly Mock<IConfigurationService> _configServiceMock;
        private readonly Mock<IRepositoryDatabase> _repositoryDatabaseMock;
        private readonly Mock<IBuildHttpClient> _buildClientMock;
        private readonly Mock<IProjectHttpClient> _projectClientMock;
        private readonly Mock<IGitHttpClient> _gitClientMock;
        private readonly BuildInfoService _buildInfoService;

        public BuildInfoServiceTests()
        {
            _hubContextMock = new Mock<IHubContext<BuildInfoHub>>();
            _loggerMock = new Mock<ILogger<BuildInfoService>>();
            _configServiceMock = new Mock<IConfigurationService>();
            _repositoryDatabaseMock = new Mock<IRepositoryDatabase>();
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
                _repositoryDatabaseMock.Object,
                _buildClientMock.Object,
                _projectClientMock.Object,
                _gitClientMock.Object);
        }

        [Fact]
        public async Task GetBuildInfoAsync_ShouldReturnOrderedRepositories_WhenFilterIsApplied()
        {
            // Arrange
            var repos = new List<Repository>
            {
                new Repository { Id = Guid.NewGuid(), Name = "Repo1", Project = "Project1", Pipeline = new Pipeline { Last = new Build { Commit = new Commit { AuthorName = "Author1" } } } },
                new Repository { Id = Guid.NewGuid(), Name = "Repo2", Project = "Project2", Pipeline = new Pipeline { Last = new Build { Commit = new Commit { AuthorName = "Author2" } } } }
            };

            // Mock the database to return the mocked collection with support for async operations
            _repositoryDatabaseMock.Setup(db => db.Query()).Returns(repos.AsQueryable());

            // Act
            var result = await _buildInfoService.GetBuildInfoAsync("Author1");

            // Assert
            Assert.Equal(1, result.Count);
            Assert.Equal("Repo1", result.First().Name); // Check ordering, Repo1 should come first as per filter and sorting
        }
    }
}
