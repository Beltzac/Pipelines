using Blazored.Toast;
using Common.ExternalApis;
using Common.Repositories;
using Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Common.Utils
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            services.AddSignalR();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddEntityFrameworkSqlite();
            services.AddDbContextFactory<RepositoryDbContext>(options =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var config = configService.GetConfig();
                var databasePath = Path.Combine(config.LocalCloneFolder, "Builds.db");
                var connectionString = $"Data Source={databasePath}";
                options.UseSqlite(connectionString);
            });

            services.AddScoped<IRepositoryDatabase, SqliteRepositoryDatabase>();

            // Register VssConnection as a singleton
            services.AddSingleton<IVssConnection>(provider =>
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
                var connection = provider.GetRequiredService<IVssConnection>();
                return connection.GetClient<BuildHttpClient>();
            });

            // Register GitHttpClient
            services.AddSingleton(provider =>
            {
                var connection = provider.GetRequiredService<IVssConnection>();
                return connection.GetClient<GitHttpClient>();
            });

            // Register ProjectHttpClient
            services.AddSingleton(provider =>
            {
                var connection = provider.GetRequiredService<IVssConnection>();
                return connection.GetClient<ProjectHttpClient>();
            });

            services.AddSingleton<IBuildHttpClient, BuildHttpClientFacade>();
            services.AddSingleton<IProjectHttpClient, ProjectHttpClientFacade>();
            services.AddSingleton<IGitHttpClient, GitHttpClientFacade>();

            services.AddScoped<ICommitDataExportService, CommitDataExportService>();
            services.AddScoped<ISignalRClientService, SignalRClientService>();
            services.AddLogging();
            services.AddScoped<IOracleSchemaService, OracleSchemaService>();
            services.AddScoped<IBuildInfoService, BuildInfoService>();
            services.AddScoped<IConsulService, ConsulService>();
            services.AddBlazoredToast();
            services.AddBlazorContextMenu();

            services.AddAutoMapper(typeof(RepositoryDbContext));

            return services;
        }
    }
}
