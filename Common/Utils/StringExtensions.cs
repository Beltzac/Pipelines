using Common.Models;
using Markdig;
using System.Text.RegularExpressions;

namespace Common.Utils
{
    public static class StringExtensions
    {
        public static string ToHtml(this Commit commit, ConfigModel config)
        {
            if (string.IsNullOrEmpty(commit.CommitMessage))
            {
                return null;
            };

            var message = commit.CommitMessage;

            // Define the patterns and replacements for different log levels using capture groups
            var replacements = new Dictionary<string, string>
            {
                { "(ERROR)", "<span class='log-error'>$1</span>" },
                { "(exception)", "<span class='log-error'>$1</span>" },
                { "(WARNING)", "<span class='log-warning'>$1</span>" },
                { "(INFO)", "<span class='log-info'>$1</span>" }
            };

            // Apply replacements using Regex with IgnoreCase option and capture groups
            foreach (var replacement in replacements)
            {
                message = Regex.Replace(message, replacement.Key, replacement.Value, RegexOptions.IgnoreCase);
            }

            // Highlight timestamps
            var timestampPattern = @"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z)";
            message = Regex.Replace(message, timestampPattern, match =>
            {
                return $"<span class='log-timestamp'>{match.Value}</span>";
            });

            // Highlight docker commands
            var dockerCommandPattern = @"(##\[\w+\]/usr/bin/docker [^\n]+)";
            message = Regex.Replace(message, dockerCommandPattern, match =>
            {
                return $"<span class='log-docker-command'>{match.Value}</span>";
            });

            // Identify and link PRs
            var prPattern = @"(?:Merge[d]? pull request|Merged PR) #?(\d+)";
            message = Regex.Replace(message, prPattern, match =>
            {
                var prNumber = match.Groups[1].Value;
                if (string.IsNullOrEmpty(commit.ProjectName))
                {
                    return match.Value; // Return original text if no project context
                }
                return $"<a href='{config.OrganizationUrl}/{commit.ProjectName}/_git/{commit.RepoName}/pullrequest/{prNumber}' target='_blank'>{match.Value}</a>";
            });

            // Create Jira links https://terminalcp.atlassian.net/ and open in new window
            var pattern = @"([A-Z, \d]{1,10}-\d+)(\s?:)";
            message = Regex.Replace(message, pattern, match =>
            {
                return $"<a href='https://terminalcp.atlassian.net/browse/{match.Groups[1].Value}' target='_blank'>{match.Groups[1].Value.Trim()}</a>:";
            });

            var pipeline = new MarkdownPipelineBuilder()
              .UseAutoLinks()
              .UseMediaLinks()
              .UseFigures()
              .UseBootstrap()
              .Build();

            return Markdown.ToHtml(message, pipeline);

            //return logs;
        }
    }
}
