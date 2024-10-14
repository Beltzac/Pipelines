using Common.ExternalApis;
using Common.Models;
using Common.Repositories;
using Common.Services;
using Common.Utils;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;

namespace Tests
{
    public class ProgramTests
    {
        //  [Test]
        public void Services_AreRegisteredCorrectly()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();

            var config = new ConfigModel
            {
                OrganizationUrl = "https://dummy.url",
                PAT = "dummyPAT",
                LocalCloneFolder = "C:\\Temp"
            };

            // Mock IConfigurationService
            var mockConfigService = new Mock<IConfigurationService>();
            mockConfigService.Setup(s => s.GetConfig()).Returns(config);
            serviceCollection.AddSingleton(mockConfigService.Object);

            // Mock VssConnection to prevent actual Azure calls
            var mockVssConnection = new Mock<IVssConnection>();
            serviceCollection.AddSingleton(mockVssConnection.Object);

            // Optionally, mock the HTTP clients if they are used immediately upon registration
            //var mockBuildHttpClient = new Mock<BuildHttpClient>(mockVssConnection.Object);
            //serviceCollection.AddSingleton(mockBuildHttpClient.Object);

            //var mockGitHttpClient = new Mock<GitHttpClient>(mockVssConnection.Object);
            //serviceCollection.AddSingleton(mockGitHttpClient.Object);

            //var mockProjectHttpClient = new Mock<ProjectHttpClient>(mockVssConnection.Object);
            //serviceCollection.AddSingleton(mockProjectHttpClient.Object);

            // Register other necessary services
            serviceCollection.AddCustomServices();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act & Assert
            serviceProvider.GetService<IOracleSchemaService>().Should().NotBeNull();
            serviceProvider.GetService<IBuildHttpClient>().Should().NotBeNull();
            serviceProvider.GetService<IProjectHttpClient>().Should().NotBeNull();
            serviceProvider.GetService<IGitHttpClient>().Should().NotBeNull();
            serviceProvider.GetService<IBuildInfoService>().Should().NotBeNull();
            serviceProvider.GetService<ISignalRClientService>().Should().NotBeNull();
            serviceProvider.GetService<IConsulService>().Should().NotBeNull();
            serviceProvider.GetService<IRepositoryDatabase>().Should().NotBeNull();
            serviceProvider.GetService<IConfigurationService>().Should().NotBeNull();
        }
    }
}
