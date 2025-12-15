using Common.Services;
using Common.Models;
using FluentAssertions;
using Moq;
using Common.Services.Interfaces;
using Common.Repositories.Interno.Interfaces;
using Microsoft.Extensions.Logging;
using SmartComponents.LocalEmbeddings;
using Common.ExternalApis.Interfaces;
using TUnit.Core;

namespace Tests
{
    public class RepositoryServiceTests
    {
        [Test]
        public void GenerateSonarCloudUrl_ShouldGenerateCorrectUrl()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<RepositoryService>>();
            var mockConfigService = new Mock<IConfigurationService>();
            var mockRepoDb = new Mock<IRepositoryDatabase>();
            var mockBuildClient = new Mock<IBuildHttpClient>();
            var mockProjectClient = new Mock<IProjectHttpClient>();
            var mockGitClient = new Mock<IGitHttpClient>();

            var config = new ConfigModel { PinnedRepositories = new HashSet<Guid>() };
            mockConfigService.Setup(s => s.GetConfig()).Returns(config);

            var service = new RepositoryService(
                mockLogger.Object,
                mockConfigService.Object,
                mockRepoDb.Object,
                mockBuildClient.Object,
                mockProjectClient.Object,
                mockGitClient.Object,
                null);

            var repo = new Repository
            {
                Project = "Libraries -NET Core",
                Name = "Tcp-Core-Export-Excel",
                CurrentBranch = "feature/formatador-excel-v2"
            };

            var expectedUrl = "https://sonarcloud.io/summary/new_code?id=Libraries-NET-Core-Tcp-Core-Export-Excel&branch=feature%2Fformatador-excel-v2";

            // Act
            var actualUrl = service.GenerateSonarCloudUrl(repo);

            // Assert
            actualUrl.Should().Be(expectedUrl);
        }
    }
}
