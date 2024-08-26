using System;
using System.IO;
using Xunit;

namespace BuildInfoBlazorApp.Tests
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

        [Fact]
        public void GetConfig_ReturnsDefaultValues_WhenConfigFileDoesNotExist()
        {
            var config = _configService.GetConfig();

            Assert.Equal("https://dev.azure.com/terminal-cp", config.OrganizationUrl);
            Assert.Equal(@"C:\repos", config.LocalCloneFolder);
            Assert.Empty(config.PAT);
        }

        [Fact]
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
            Assert.Equal(testConfig.PAT, loadedConfig.PAT);
            Assert.Equal(testConfig.OrganizationUrl, loadedConfig.OrganizationUrl);
            Assert.Equal(testConfig.LocalCloneFolder, loadedConfig.LocalCloneFolder);
        }

        public void Dispose()
        {
            // Clean up the temporary directory after tests
            Directory.Delete(Path.GetDirectoryName(_testConfigPath), true);
        }
    }
}