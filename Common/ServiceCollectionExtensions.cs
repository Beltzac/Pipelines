using Blazored.Toast;
using BuildInfoBlazorApp.Data;
using LiteDB.Async;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Common
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<ILiteDatabaseAsync, LiteDatabaseAsync>(provider =>
            {
                var configService = provider.GetRequiredService<IConfigurationService>();
                var config = configService.GetConfig();
                var databasePath = $@"Filename={Path.Combine(config.LocalCloneFolder, "Builds.db")};Connection=shared";
                return new LiteDatabaseAsync(databasePath);
            });
            services.AddSingleton<IRepositoryDatabase, LiteDbRepositoryDatabase>();

            // Register VssConnection as a singleton
            services.AddSingleton(provider =>
            {
                var configService = provider.GetRequiredService<IConfigurationService>();
                var config = configService.GetConfig();
                var connection = new VssConnection(
                    new Uri(config.OrganizationUrl),
                    new VssBasicCredential(string.Empty, config.PAT)
                );
                return connection;
            });

            // Register BuildHttpClient
            services.AddSingleton(provider =>
            {
                var connection = provider.GetRequiredService<VssConnection>();
                return connection.GetClient<BuildHttpClient>();
            });

            // Register GitHttpClient
            services.AddSingleton(provider =>
            {
                var connection = provider.GetRequiredService<VssConnection>();
                return connection.GetClient<GitHttpClient>();
            });

            // Register ProjectHttpClient
            services.AddSingleton(provider =>
            {
                var connection = provider.GetRequiredService<VssConnection>();
                return connection.GetClient<ProjectHttpClient>();
            });

            services.AddSingleton<IBuildHttpClient, BuildHttpClientFacade>();
            services.AddSingleton<IProjectHttpClient, ProjectHttpClientFacade>();
            services.AddSingleton<IGitHttpClient, GitHttpClientFacade>();
            services.AddSingleton<SignalRClientService>();
            services.AddLogging();
            services.AddSingleton<OracleSchemaService>();
            services.AddSingleton<OracleDiffService>();
            services.AddSingleton<BuildInfoService>();
            services.AddSingleton<ConsulService>();
            services.AddSingleton<BuildInfoBlazorApp.Data.BuildInfoService>();
            services.AddBlazoredToast();
            services.AddBlazorContextMenu();

            return services;
        }
    }
}
