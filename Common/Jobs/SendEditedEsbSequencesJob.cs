using Common.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Common.Models;
using System.Text;

namespace Common.Jobs
{
    public class SendEditedEsbSequencesJob : IJob
    {
        private readonly IEsbService _esbService;
        private readonly IConfigurationService _configService;
        private readonly ILogger<SendEditedEsbSequencesJob> _logger;

        public SendEditedEsbSequencesJob(
            IEsbService esbService,
            IConfigurationService configService,
            ILogger<SendEditedEsbSequencesJob> logger)
        {
            _esbService = esbService;
            _configService = configService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("Starting SendEditedEsbSequencesJob.");

            var config = _configService.GetConfig();
            if (string.IsNullOrEmpty(config.TeamsWebhookUrl))
            {
                _logger.LogWarning("Teams webhook URL is not configured. Skipping sending edited ESB sequences.");
                return;
            }

            var editedSequences = new List<SequenceInfo>();

            foreach (var esbServer in config.EsbServers)
            {
                try
                {
                    var parsedSequences = await _esbService.GetSequencesAsync(esbServer);
                    editedSequences.AddRange(parsedSequences.Where(s => s.IsEdited));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error loading ESB sequences for server {esbServer.Name}: {ex.Message}");
                }
            }

            if (editedSequences.Any())
            {
                var message = FormatTeamsMessage(editedSequences);
                _logger.LogInformation($"Sent notification for {editedSequences.Count} edited ESB sequences. {message}");
            }
            else
            {
                _logger.LogInformation("No edited ESB sequences found.");
            }

            _logger.LogInformation("Finished SendEditedEsbSequencesJob.");
        }

        private string FormatTeamsMessage(List<SequenceInfo> sequences)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Edited ESB Sequences Report");
            sb.AppendLine($"**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Total Edited Sequences:** {sequences.Count}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var sequence in sequences)
            {
                sb.AppendLine($"### Sequence: {sequence.Name}");
                sb.AppendLine($"- **Artifact Container:** {sequence.ArtifactContainerName}");
                sb.AppendLine($"- **Description:** {sequence.Description ?? "N/A"}");
                sb.AppendLine($"- **Enable Statistics:** {(sequence.EnableStatistics ? "Yes" : "No")}");
                sb.AppendLine($"- **Enable Tracing:** {(sequence.EnableTracing ? "Yes" : "No")}");
                sb.AppendLine($"- **Is Edited:** {(sequence.IsEdited ? "Yes" : "No")}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}