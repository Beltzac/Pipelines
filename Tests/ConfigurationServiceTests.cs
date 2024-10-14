using Common.Models;
using FluentAssertions;

namespace Tests
{
    public class ConfigurationServiceTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly ConfigurationService _configService;

        public ConfigurationServiceTests()
        {
            // Use a temporary directory for testing
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            _testConfigPath = Path.Combine(tempPath, "config.json");

            // Create a test-specific ConfigurationService
            _configService = new ConfigurationService(_testConfigPath);
        }

        [Test]
        public void GetConfig_ReturnsDefaultValues_WhenConfigFileDoesNotExist()
        {
            var config = _configService.GetConfig();

            config.OrganizationUrl.Should().Be("https://dev.azure.com/terminal-cp");
            config.LocalCloneFolder.Should().Be(@"C:\repos");
            config.PAT.Should().BeEmpty();
            config.Should().NotBeNull();
            config.Should().BeOfType<ConfigModel>();
        }

        [Test]
        public async Task SaveConfigAsync_SavesConfigCorrectly()
        {
            var testConfig = new ConfigModel
            {
                PAT = "testPAT",
                OrganizationUrl = "https://test.com",
                LocalCloneFolder = @"D:\test"
            };

            await _configService.SaveConfigAsync(testConfig);

            var loadedConfig = _configService.GetConfig();
            loadedConfig.PAT.Should().Be(testConfig.PAT);
            loadedConfig.OrganizationUrl.Should().Be(testConfig.OrganizationUrl);
            loadedConfig.LocalCloneFolder.Should().Be(testConfig.LocalCloneFolder);
        }

        public void Dispose()
        {
            // Clean up the temporary directory after tests
            Directory.Delete(Path.GetDirectoryName(_testConfigPath), true);
        }
    }
}
