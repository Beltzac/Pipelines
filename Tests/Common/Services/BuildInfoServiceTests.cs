using Common.ExternalApis.Interfaces;
using Common.Models;
using Common.Repositories.Interno.Interfaces;
using Common.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SmartComponents.LocalEmbeddings;

namespace Tests.Common.Services
{
    public class BuildInfoServiceTests
    {
        private readonly Mock<ILogger<RepositoryService>> _loggerMock;
        private readonly Mock<IConfigurationService> _configServiceMock;
        private readonly Mock<IRepositoryDatabase> _repositoryDatabaseMock;
        private readonly Mock<IBuildHttpClient> _buildClientMock;
        private readonly Mock<IProjectHttpClient> _projectClientMock;
        private readonly Mock<IGitHttpClient> _gitClientMock;
        private readonly LocalEmbedder _embedderMock;
        private readonly RepositoryService _buildInfoService;

        public BuildInfoServiceTests()
        {
            _loggerMock = new Mock<ILogger<RepositoryService>>();
            _configServiceMock = new Mock<IConfigurationService>();
            _repositoryDatabaseMock = new Mock<IRepositoryDatabase>();
            _buildClientMock = new Mock<IBuildHttpClient>();
            _projectClientMock = new Mock<IProjectHttpClient>();
            _gitClientMock = new Mock<IGitHttpClient>();
            _embedderMock = new LocalEmbedder();// Mock<LocalEmbedder>();

            var configModel = new ConfigModel
            {
                LocalCloneFolder = "some/path",
                PAT = "someToken"
            };
            _configServiceMock.Setup(cs => cs.GetConfig()).Returns(configModel);

            _buildInfoService = new RepositoryService(
                _loggerMock.Object,
                _configServiceMock.Object,
                _repositoryDatabaseMock.Object,
                _buildClientMock.Object,
                _projectClientMock.Object,
                _gitClientMock.Object,
                _embedderMock);
        }

        // ...

        [Test]
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
            var result = await _buildInfoService.GetBuildInfoAsync("Repo2");

            // Assert
            result.Should().HaveCount(2);
            result.First().Name.Should().Be("Repo2"); // Check ordering, Repo2 should come first as per filter and sorting
        }
    }
}
