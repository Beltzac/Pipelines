using BuildInfoBlazorApp.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void Services_AreRegisteredCorrectly()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
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
