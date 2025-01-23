using Common.Services;
using Microsoft.Extensions.Logging;
using Quartz;
using System.IO;
using LibGit2Sharp;
using Common.Services.Interfaces;

namespace Common.Jobs
{
    public class BackupConsulJob : IJob
    {
        private readonly IConsulService _consulService;
        private readonly IConfigurationService _configService;
        private readonly ILogger<BackupConsulJob> _logger;

        public BackupConsulJob(
            IConsulService consulService,
            IConfigurationService configService,
            ILogger<BackupConsulJob> logger)
        {
            _consulService = consulService;
            _configService = configService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var config = _configService.GetConfig();

            context.MergedJobDataMap.TryGetBooleanValue("IsManualRun", out bool isManualRun);

            if (!isManualRun && !config.EnableConsulBackupJob)
            {
                _logger.LogInformation("Consul backup job is disabled, skipping execution");
                return;
            }

            var backupPath = config.ConsulBackupRepo;

            // Ensure backup directory exists
            Directory.CreateDirectory(backupPath);

            // Initialize or open Git repository
            if (!LibGit2Sharp.Repository.IsValid(backupPath))
            {
                LibGit2Sharp.Repository.Init(backupPath);
            }

            using (var repo = new LibGit2Sharp.Repository(backupPath))
            {
                foreach (var env in config.ConsulEnvironments)
                {
                    var envPath = Path.Combine(backupPath, env.Name);
                    Directory.CreateDirectory(envPath);

                    try
                    {
                        var consulData = await _consulService.GetConsulKeyValues(env);

                        // Track existing files to detect deletions
                        var existingFiles = new HashSet<string>();
                        foreach (var kv in consulData)
                        {
                            var filePath = ConsulService.JoinPathKey(envPath, kv.Key);
                            existingFiles.Add(filePath);

                            _consulService.SaveKvToFile(envPath, kv.Key, kv.Value.Value);
                        }

                        // Remove files that no longer exist in Consul
                        if (Directory.Exists(envPath))
                        {
                            foreach (var file in Directory.GetFiles(envPath, "*", SearchOption.AllDirectories))
                            {
                                if (!existingFiles.Contains(file))
                                {
                                    File.Delete(file);
                                    _logger.LogInformation($"Deleted file that no longer exists in Consul: {file}");
                                }
                            }
                        }

                        // Stage all changes including deletions
                        Commands.Stage(repo, "*");

                        var status = repo.RetrieveStatus();
                        // Create commit if there are changes
                        if (status.IsDirty)
                        {
                            var signature = new LibGit2Sharp.Signature("ConsulBackup", "backup@local", DateTimeOffset.Now);
                            repo.Commit($"Backup Consul KVs from {env.Name} at {DateTime.Now}", signature, signature);
                            _logger.LogInformation($"Created backup commit for environment {env.Name}");
                        }
                        else
                        {
                            _logger.LogInformation($"No changes detected for environment {env.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error backing up Consul KVs for environment {env.Name}");
                    }
                }
            }
        }
    }
}
