using Markdig;
using System.Text.RegularExpressions;

namespace Common
{
    public static class StringExtensions
    {
        public static string ToHtml(this string commit)
        {
            if (string.IsNullOrEmpty(commit))
            {
                return null;
            };

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
                commit = Regex.Replace(commit, replacement.Key, replacement.Value, RegexOptions.IgnoreCase);
            }

            // Highlight timestamps
            var timestampPattern = @"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z)";
            commit = Regex.Replace(commit, timestampPattern, match =>
            {
                return $"<span class='log-timestamp'>{match.Value}</span>";
            });

            // Highlight docker commands
            var dockerCommandPattern = @"(##\[\w+\]/usr/bin/docker [^\n]+)";
            commit = Regex.Replace(commit, dockerCommandPattern, match =>
            {
                return $"<span class='log-docker-command'>{match.Value}</span>";
            });

            // Adjust Jira links
            var pattern = @"((?<!([A-Z]{1,10})-?)[A-Z]+-\d+[:,-])";
            commit = Regex.Replace(commit, pattern, match =>
            {
                return $"{match.Value.TrimEnd(':').TrimEnd('-')} -";
            });

            var pipeline = new MarkdownPipelineBuilder()
              //.UseAdvancedExtensions()
              .UseAutoLinks()
              .UseMediaLinks()
              .UseFigures()
              //.UsePipeTables()
              //.UsePreciseSourceLocation()
              //.UseAutoIdentifiers()
              //.UseSmartyPants()
              .UseBootstrap()
              //.UseSoftlineBreakAsHardlineBreak()
              .UseJiraLinks(new Markdig.Extensions.JiraLinks.JiraLinkOptions("https://terminalcp.atlassian.net/")
              {
                  OpenInNewWindow = true,
              })
              .Build();

            return Markdown.ToHtml(commit, pipeline);

            //return logs;
        }
    }
}
