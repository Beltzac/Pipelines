using Microsoft.Extensions.DependencyInjection;

namespace Common
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<IRepositoryDatabase, LiteDbRepositoryDatabase>();
            services.AddSingleton<IBuildHttpClient, BuildHttpClientFacade>();
            services.AddSingleton<IProjectHttpClient, ProjectHttpClientFacade>();
            services.AddSingleton<IGitHttpClient, GitHttpClientFacade>();
            services.AddSingleton<SignalRClientService>();
            services.AddSingleton<OracleSchemaService>();
            services.AddSingleton<OracleDiffService>();
            services.AddSingleton<BuildInfoService>();
            services.AddSingleton<ConsulService>();

            return services;
        }
    }
}
