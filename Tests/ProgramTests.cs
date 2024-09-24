using BuildInfoBlazorApp.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;

namespace Common.Tests
{
    public class ProgramTests
    {

        [Fact]
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
            Assert.NotNull(serviceProvider.GetService<IOracleSchemaService>());
            Assert.NotNull(serviceProvider.GetService<IBuildHttpClient>());
            Assert.NotNull(serviceProvider.GetService<IProjectHttpClient>());
            Assert.NotNull(serviceProvider.GetService<IGitHttpClient>());
            Assert.NotNull(serviceProvider.GetService<IBuildInfoService>());
            Assert.NotNull(serviceProvider.GetService<ISignalRClientService>());
            Assert.NotNull(serviceProvider.GetService<IConsulService>());
            Assert.NotNull(serviceProvider.GetService<IRepositoryDatabase>());
            Assert.NotNull(serviceProvider.GetService<IConfigurationService>());
        }
    }
}
