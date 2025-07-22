using Common.ExternalApis;
using Common.ExternalApis.Interfaces;
using Common.Repositories.Interno;
using Common.Repositories.Interno.Interfaces;
using Common.Repositories.TCP.Interfaces;
using Common.Services;
using Common.Services.Interfaces;
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
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddEntityFrameworkSqlite();

            services.AddDbContextFactory<RepositoryDbContext>(options =>
            {
                var databasePath = DBUtils.MainDBPath;
                var connectionString = $"Data Source={databasePath}";
                options.UseSqlite(connectionString)
                    .EnableSensitiveDataLogging();
            });

            services.AddScoped<IRepositoryDatabase, SqliteRepositoryDatabase>();

            // Register VssConnection as a singleton
            services.AddScoped<IVssConnection>(provider =>
            {
                var configService = provider.GetRequiredService<IConfigurationService>();
                var config = configService.GetConfig();

                if (config.PAT == null)
                    return null;

                var connection = new VssConnection(
                    new Uri(config.OrganizationUrl),
                    new VssBasicCredential(string.Empty, config.PAT)
                );
                return connection;
            });

            // Register BuildHttpClient
            services.AddScoped(provider =>
            {
                var connection = provider.GetService<IVssConnection>();
                return connection?.GetClient<BuildHttpClient>();
            });

            // Register GitHttpClient
            services.AddScoped(provider =>
            {
                var connection = provider.GetService<IVssConnection>();
                return connection?.GetClient<GitHttpClient>();
            });

            // Register ProjectHttpClient
            services.AddScoped(provider =>
            {
                var connection = provider.GetService<IVssConnection>();
                return connection?.GetClient<ProjectHttpClient>();
            });

            services.AddScoped<IBuildHttpClient, BuildHttpClientFacade>();
            services.AddScoped<IProjectHttpClient, ProjectHttpClientFacade>();
            services.AddScoped<IGitHttpClient, GitHttpClientFacade>();

            services.AddScoped<IAutoUpdateService, AutoUpdateService>();

            services.AddScoped<ICommitExportService, CommitExportService>();
            services.AddLogging();
            services.AddScoped<IOracleSchemaService, OracleSchemaService>();
            services.AddScoped<IRepositoryService, RepositoryService>();
            services.AddScoped<IConsulService, ConsulService>();
            services.AddBlazorBootstrap();
            services.AddBlazorContextMenu();
            services.AddScoped<IEsbService, EsbService>();
            services.AddScoped<ISggService, SggService>();
            services.AddScoped<IOracleMessageService, OracleMessageService>();
            services.AddScoped<ICadastroService, CadastroService>();
            services.AddScoped<UrlPinger>();

            services.AddScoped<TextFileProcessor>();
            services.AddScoped<CodeSearchService>();
            services.AddScoped<IOracleConnectionFactory, OracleConnectionFactory>();
            services.AddScoped<IOracleRepository, Common.Repositories.TCP.OracleRepository>();

            services.AddScoped<IMongoMessageService, MongoMessageService>();

            services.AddScoped<LogViewerService>();
            services.AddScoped<WindowsRoutesManager>();
            services.AddScoped<ThemeService>();

            // Register Tempo services
            services.AddScoped<ITempoService, TempoService>();
            services.AddScoped<IWorklogService, WorklogService>();
            services.AddScoped<IJiraService, JiraService>();
            services.AddHttpClient<IJiraService, JiraService>();

            services.AddAutoMapper(typeof(RepositoryDbContext));

            return services;
        }
    }
}
