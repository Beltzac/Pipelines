using Microsoft.Extensions.DependencyInjection;
using Xunit;

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
            Assert.NotNull(serviceProvider.GetService<OracleSchemaService>());
            Assert.NotNull(serviceProvider.GetService<OracleDiffService>());
            Assert.NotNull(serviceProvider.GetService<IBuildHttpClient>());
            Assert.NotNull(serviceProvider.GetService<IProjectHttpClient>());
            Assert.NotNull(serviceProvider.GetService<IGitHttpClient>());
            Assert.NotNull(serviceProvider.GetService<BuildInfoService>());
            Assert.NotNull(serviceProvider.GetService<SignalRClientService>());
            Assert.NotNull(serviceProvider.GetService<ConsulService>());
            Assert.NotNull(serviceProvider.GetService<IRepositoryDatabase>());
            Assert.NotNull(serviceProvider.GetService<ConfigurationService>());
        }
    }
}
