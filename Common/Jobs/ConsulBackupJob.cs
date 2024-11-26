using Common.Services;
using Microsoft.Extensions.Logging;
using Quartz;
using System.IO;
using LibGit2Sharp;

namespace Common.Jobs
{
    public class ConsulBackupJob : IJob
    {
        private readonly IConsulService _consulService;
        private readonly IConfigurationService _configService;
        private readonly ILogger<ConsulBackupJob> _logger;

        public ConsulBackupJob(
            IConsulService consulService,
            IConfigurationService configService,
            ILogger<ConsulBackupJob> logger)
        {
            _consulService = consulService;
            _configService = configService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var config = _configService.GetConfig();
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

                        foreach (var kv in consulData)
                        {
                            _consulService.SaveKvToFile(envPath, kv.Key, kv.Value.Value);
                        }

                        // Stage all changes
                        LibGit2Sharp.Commands.Stage(repo, "*");

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
