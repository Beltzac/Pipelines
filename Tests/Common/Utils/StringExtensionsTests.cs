using Common.Models;
using Common.Utils;
using FluentAssertions;

namespace Common.Tests.Utils
{
    public class StringExtensionsTests
    {
        private readonly ConfigModel _config;

        public StringExtensionsTests()
        {
            _config = new ConfigModel
            {
                OrganizationUrl = "https://dev.azure.com/organization"
            };
        }

        [Test]
        public void ToHtml_WithLogLevels_HighlightsLogLevels()
        {
            // Arrange
            var commit = new Commit
            {
                CommitMessage = "ERROR: This is an error\nWARNING: This is a warning\nINFO: This is info"
            };

            // Act
            var result = commit.ToHtml(_config);

            // Assert
            result.Should().Contain("<span class='log-error'>ERROR</span>");
            result.Should().Contain("<span class='log-warning'>WARNING</span>");
            result.Should().Contain("<span class='log-info'>INFO</span>");
        }

        [Test]
        public void ToHtml_WithTimestamp_HighlightsTimestamp()
        {
            // Arrange
            var commit = new Commit
            {
                CommitMessage = "2023-01-01T12:00:00.0000000Z - Test message"
            };

            // Act
            var result = commit.ToHtml(_config);

            // Assert
            result.Should().Contain("<span class='log-timestamp'>2023-01-01T12:00:00.0000000Z</span>");
        }

        [Test]
        public void ToHtml_WithPullRequest_CreatesPRLink()
        {
            // Arrange
            var commit = new Commit
            {
                CommitMessage = "Merged PR #123",
                ProjectName = "project",
                RepoName = "repo"
            };

            // Act
            var result = commit.ToHtml(_config);

            // Assert
            result.Should().Contain($"<a href='{_config.OrganizationUrl}/project/_git/repo/pullrequest/123'");
        }

        [Test]
        public void ToHtml_WithDockerCommand_HighlightsCommand()
        {
            // Arrange
            var commit = new Commit
            {
                CommitMessage = "##[command]/usr/bin/docker build -t test ."
            };

            // Act
            var result = commit.ToHtml(_config);

            // Assert
            result.Should().Contain("<span class='log-docker-command'>##[command]/usr/bin/docker build -t test .</span>");
        }

        [Test]
        public void ToHtml_WithJiraLink_HasJiraLink()
        {
            // Arrange
            var commit = new Commit
            {
                CommitMessage = "JIRA-123: Test message"
            };
            // Act
            var result = commit.ToHtml(_config);
            // Assert
            result.Should().Contain("<a href=");
        }

    
        public void ToHtml_WithJiraLink_HasJiraLink2()
        {
            // Arrange
            var commit = new Commit
            {
                CommitMessage = "RSPN2023-385: Não precisamos da chave mais"
            };
            // Act
            var result = commit.ToHtml(_config);
            // Assert
            result.Should().Contain("<a href=");
        }
    }
}
