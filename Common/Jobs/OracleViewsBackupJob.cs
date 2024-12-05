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
            var isManualRun = context.MergedJobDataMap.GetBooleanValue("IsManualRun");
    
            if (!isManualRun && !config.EnableOracleBackupJob)
            {
                _logger.LogInformation("Oracle backup job is disabled, skipping execution");
                return;
            }

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
                        
                        // Track existing files to detect deletions
                        var existingFiles = new HashSet<string>();
                        foreach (var view in views)
                        {
                            var filePath = Path.Combine(envPath, $"{view.Key}.sql");
                            existingFiles.Add(filePath);
                            await File.WriteAllTextAsync(filePath, view.Value);
                        }

                        // Remove files that no longer exist in Oracle
                        if (Directory.Exists(envPath))
                        {
                            foreach (var file in Directory.GetFiles(envPath, "*.sql", SearchOption.TopDirectoryOnly))
                            {
                                if (!existingFiles.Contains(file))
                                {
                                    File.Delete(file);
                                    _logger.LogInformation($"Deleted file that no longer exists in Oracle: {file}");
                                }
                            }
                        }

                        // Stage all changes including deletions
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
