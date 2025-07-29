using Common.Models;
using Markdig;
using Microsoft.AspNetCore.Components;
using System.Text.RegularExpressions;

namespace Common.Utils
{
    public static class StringExtensions
    {
        public static string ToHtml(this Commit commit, ConfigModel config)
        {
            if (string.IsNullOrEmpty(commit?.CommitMessage))
            {
                return null;
            };

            var message = commit.CommitMessage;

            // // Define the patterns and replacements for different log levels using capture groups
            // var replacements = new Dictionary<string, string>
            // {
            //     { "(ERROR)", "<span class='log-error'>$1</span>" },
            //     { "(exception)", "<span class='log-error'>$1</span>" },
            //     { "(WARNING)", "<span class='log-warning'>$1</span>" },
            //     { "(INFO)", "<span class='log-info'>$1</span>" }
            // };

            // // Apply replacements using Regex with IgnoreCase option and capture groups
            // foreach (var replacement in replacements)
            // {
            //     message = Regex.Replace(message, replacement.Key, replacement.Value, RegexOptions.IgnoreCase);
            // }

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
                return $"<a href='https://terminalcp.atlassian.net/browse/{match.Groups[1].Value.Trim()}' target='_blank'>{match.Groups[1].Value.Trim()}</a>:";
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

        public static List<string> ExtractJiraCards(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var pattern = @"([A-Z, \d]{1,10}-\d+)";
            return Regex.Matches(text, pattern)
                .Select(m => m.Groups[1].Value.Trim())
                .Distinct()
                .ToList();
        }

        public static List<string> ExtractPrNumbers(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var pattern = @"(?:Merge[d]? pull request|Merged PR) #?(\d+)";
            return Regex.Matches(text, pattern)
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();
        }

        public static string ReplaceInvalidChars(this string str)
        {
            // Define invalid characters for Excel sheet names
            char[] invalidChars = { '\\', '/', '*', '[', ']', ':', '?' };
            string cleanedString = str;

            foreach (char invalidChar in invalidChars)
            {
                cleanedString = cleanedString.Replace(invalidChar, '_'); // Replace with underscore or another suitable character
            }

            return cleanedString;
        }

        public static MarkupString GetHighlightedText(this string text, string searchTerm)
        {
            if (string.IsNullOrEmpty(text))
                return new MarkupString(string.Empty); // Return empty if text is null/empty

            if (string.IsNullOrEmpty(searchTerm))
                return new MarkupString(text); // Return plain text if no search term

            try
            {
                // Use Regex.Escape for safety
                var regex = new Regex(Regex.Escape(searchTerm), RegexOptions.IgnoreCase);
                return new MarkupString(regex.Replace(text, match => $"<mark>{match.Value}</mark>"));
            }
            catch (RegexMatchTimeoutException)
            {
                // Handle potential regex timeouts on very large text/complex patterns
                return new MarkupString($"[Error highlighting text: Timeout] {text}");
            }
            catch (ArgumentException)
            {
                // Handle invalid regex patterns if Escape wasn't perfect
                return new MarkupString($"[Error highlighting text: Invalid Pattern] {text}");
            }
        }
    }
}
