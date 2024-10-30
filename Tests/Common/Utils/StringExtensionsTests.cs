using Common.Models;
using Common.Utils;

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
            Assert.Contains("<span class='log-error'>ERROR</span>", result);
            Assert.Contains("<span class='log-warning'>WARNING</span>", result);
            Assert.Contains("<span class='log-info'>INFO</span>", result);
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
            Assert.Contains("<span class='log-timestamp'>2023-01-01T12:00:00.0000000Z</span>", result);
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
            Assert.Contains($"<a href='{_config.OrganizationUrl}/project/_git/repo/pullrequest/123'", result);
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
            Assert.Contains("<span class='log-docker-command'>##[command]/usr/bin/docker build -t test .</span>", result);
        }
    }
}
