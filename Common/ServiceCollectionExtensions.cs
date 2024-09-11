using BuildInfoBlazorApp.Data;
using LiteDB.Async;
using Microsoft.Extensions.DependencyInjection;

namespace Common
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<ILiteDatabaseAsync, LiteDatabaseAsync>(provider => new LiteDatabaseAsync("Filename=C:\\repos\\Builds.db;Connection=shared"));
            services.AddSingleton<IRepositoryDatabase, LiteDbRepositoryDatabase>();
            services.AddSingleton<Microsoft.TeamFoundation.Build.WebApi.BuildHttpClient>();
            services.AddSingleton<IBuildHttpClient, BuildHttpClientFacade>();
            services.AddSingleton<IProjectHttpClient, ProjectHttpClientFacade>();
            services.AddSingleton<IGitHttpClient, GitHttpClientFacade>();
            services.AddSingleton<SignalRClientService>();
            services.AddLogging();
            services.AddSingleton<OracleSchemaService>();
            services.AddSingleton<OracleDiffService>();
            services.AddSingleton<BuildInfoService>();
            services.AddSingleton<ConsulService>();

            return services;
        }
    }
}
