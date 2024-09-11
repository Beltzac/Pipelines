using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Common.Tests
{
    public class DependencyInjectionTests
    {
        [Fact]
        public void Services_AreRegisteredCorrectly()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IConfigurationService, ConfigurationService>();
            serviceCollection.AddSingleton<IRepositoryDatabase, LiteDbRepositoryDatabase>();
            serviceCollection.AddSingleton<IBuildHttpClient, BuildHttpClientFacade>();
            serviceCollection.AddSingleton<IProjectHttpClient, ProjectHttpClientFacade>();
            serviceCollection.AddSingleton<IGitHttpClient, GitHttpClientFacade>();
            serviceCollection.AddSingleton<SignalRClientService>();

            serviceCollection.AddSingleton<ILiteDatabaseAsync, LiteDatabaseAsync>(provider => 
                new LiteDatabaseAsync("Filename=MyData.db;Connection=shared"));

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act & Assert
            Assert.NotNull(serviceProvider.GetService<IConfigurationService>());
            Assert.NotNull(serviceProvider.GetService<IRepositoryDatabase>());
            Assert.NotNull(serviceProvider.GetService<IBuildHttpClient>());
            Assert.NotNull(serviceProvider.GetService<IProjectHttpClient>());
            Assert.NotNull(serviceProvider.GetService<IGitHttpClient>());
            Assert.NotNull(serviceProvider.GetService<SignalRClientService>());
        }
    }
}
