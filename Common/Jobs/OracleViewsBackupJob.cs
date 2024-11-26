using Common.ExternalApis;
using Common.Services;
using Microsoft.Extensions.Logging;
using Quartz;
using System.IO;
using LibGit2Sharp;

namespace Common.Jobs
{
    public class OracleViewsBackupJob : IJob
    {
        private readonly IOracleSchemaService _oracleSchemaService;
        private readonly IConfigurationService _configService;
        private readonly ILogger<OracleViewsBackupJob> _logger;

        public OracleViewsBackupJob(
            IOracleSchemaService oracleSchemaService,
            IConfigurationService configService,
            ILogger<OracleViewsBackupJob> logger)
        {
            _oracleSchemaService = oracleSchemaService;
            _configService = configService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var config = _configService.GetConfig();
            var backupPath = config.OracleViewsBackupRepo;

            // Ensure backup directory exists
            Directory.CreateDirectory(backupPath);

            // Initialize or open Git repository
            if (!LibGit2Sharp.Repository.IsValid(backupPath))
            {
                LibGit2Sharp.Repository.Init(backupPath);
            }

            using (var repo = new LibGit2Sharp.Repository(backupPath))
            {
                foreach (var env in config.OracleEnvironments)
                {
                    var envPath = Path.Combine(backupPath, env.Name);
                    Directory.CreateDirectory(envPath);

                    try
                    {
                        var views = _oracleSchemaService.GetViewDefinitions(env.ConnectionString, env.Schema);
                        
                        foreach (var view in views)
                        {
                            var filePath = Path.Combine(envPath, $"{view.Key}.sql");
                            await File.WriteAllTextAsync(filePath, view.Value);
                        }

                        // Stage all changes
                        LibGit2Sharp.Commands.Stage(repo, "*");

                        var status = repo.RetrieveStatus();
                        // Create commit if there are changes
                        if (status.IsDirty)
                        {
                            var signature = new LibGit2Sharp.Signature("OracleBackup", "backup@local", DateTimeOffset.Now);
                            repo.Commit($"Backup views from {env.Name} at {DateTime.Now}", signature, signature);
                            _logger.LogInformation($"Created backup commit for environment {env.Name}");
                        }
                        else
                        {
                            _logger.LogInformation($"No changes detected for environment {env.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error backing up views for environment {env.Name}");
                    }
                }
            }
        }
    }
}
