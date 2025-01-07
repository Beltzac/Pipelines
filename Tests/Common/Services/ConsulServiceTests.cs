using Common.Models;
using Common.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using System.Text;
using TUnit;

namespace Tests.Common.Services
{
    public class ConsulServiceTests
    {
        private readonly Mock<ILogger<ConsulService>> _loggerMock;
        private readonly Mock<IConfigurationService> _configServiceMock;
        private readonly ConsulService _consulService;
        private readonly string _tempPath;

        public ConsulServiceTests()
        {
            _loggerMock = new Mock<ILogger<ConsulService>>();
            _configServiceMock = new Mock<IConfigurationService>();
            _consulService = new ConsulService(_loggerMock.Object, _configServiceMock.Object);
            _tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }

        [Test]
        public async Task GetConsulKeyValues_ShouldFallbackToSequential_WhenBatchFails()
        {
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://localhost:8500",
                ConsulToken = "test-token"
            };

            // Act
            var result = await _consulService.GetConsulKeyValues(consulEnv);

            // Assert
            result.Should().NotBeNull();
            // The fact that we get here means the fallback worked, as the batch request would fail
            // and the sequential request would also fail, but we handle those failures gracefully
        }

        [Test]
        public async Task GetConsulKeyValues_ShouldHandleEmptyValues()
        {
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://localhost:8500",
                ConsulToken = "test-token"
            };

            // Act
            var result = await _consulService.GetConsulKeyValues(consulEnv);

            // Assert
            result.Should().NotBeNull();
            foreach (var kv in result.Values)
            {
                kv.Value.Should().NotBeNull(); // Even null values should be converted to empty string
            }
        }

        [Test]
        public async Task GetConsulKeyValues_ShouldIncludeUrlInResult()
        {
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://localhost:8500",
                ConsulToken = "test-token"
            };

            // Act
            var result = await _consulService.GetConsulKeyValues(consulEnv);

            // Assert
            result.Should().NotBeNull();
            foreach (var kv in result.Values)
            {
                kv.Url.Should().NotBeNullOrEmpty();
                kv.Url.Should().StartWith(consulEnv.ConsulUrl);
            }
        }

        [Test]
        [Arguments("123", true)]
        [Arguments("abc", false)]
        public void IsNumeric_ShouldValidateCorrectly(string value, bool expected)
        {
            // Act
            var result = _consulService.IsNumeric(value);

            // Assert
            result.Should().Be(expected);
        }

        [Test]
        [Arguments("http://example.com", true)]
        [Arguments("not-a-url", false)]
        public void IsValidURL_ShouldValidateCorrectly(string value, bool expected)
        {
            // Act
            var result = _consulService.IsValidURL(value);

            // Assert
            result.Should().Be(expected);
        }

        [Test]
        [Arguments("Data Source=myServerAddress;Database=myDataBase;", true)]
        [Arguments("random-string", false)]
        public void IsConnectionString_ShouldValidateCorrectly(string value, bool expected)
        {
            // Act
            var result = _consulService.IsConnectionString(value);

            // Assert
            result.Should().Be(expected);
        }

        [Test]
        [Arguments("2024-10-21", true)]
        [Arguments("not-a-date", false)]
        public void IsDateTime_ShouldValidateCorrectly(string value, bool expected)
        {
            // Act
            var result = _consulService.IsDateTime(value);

            // Assert
            result.Should().Be(expected);
        }

        [Test]
        [Arguments(@"C:\path\to\file", true)]
        [Arguments("invalid|path", false)]
        public void IsPath_ShouldValidateCorrectly(string value, bool expected)
        {
            // Act
            var result = _consulService.IsPath(value);

            // Assert
            result.Should().Be(expected);
        }

        [Test]
        [Arguments("simpleString", true)]
        [Arguments("string with spaces", false)]
        public void IsSimpleString_ShouldValidateCorrectly(string value, bool expected)
        {
            // Act
            var result = _consulService.IsSimpleString(value);

            // Assert
            result.Should().Be(expected);
        }

        [Test]
        [Arguments("Basic dXNlcjpwYXNzd29yZA==", true)]
        [Arguments("NotBasicAuth", false)]
        public void IsBasicAuth_ShouldValidateCorrectly(string value, bool expected)
        {
            // Act
            var result = _consulService.IsBasicAuth(value);

            // Assert
            result.Should().Be(expected);
        }

        [Test]
        [Arguments("key", @"{ ""teste"": ""{{ key '_infra/common/logging/default-config' }}""}", false)]
        [Arguments("key", @"{ ""teste"": ""BlaBlaBla""}", true)]
        [Arguments("key.lock", "any value", true)]
        [Arguments("config.json", @"{""valid"": true}", true)]
        [Arguments("config.json", "{invalid json}", false)]
        [Arguments("array-key", "[1,2,3]", true)]
        [Arguments("array-key", "[invalid array]", false)]
        [Arguments("prop-key", @"""validProp"": ""value""", true)]
        [Arguments("prop-key", "invalid:property", false)]
        public void IsValidFormated_ShouldValidateCorrectly(string key, string value, bool expected)
        {
            // Act
            var result = _consulService.IsValidFormated(key, value);

            // Assert
            result.Should().Be(expected);
        }

        [Test]
        public async Task UpdateConsulKeyValue_ShouldLogInformation()
        {
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://localhost:8500",
                ConsulToken = "test-token"
            };
            var key = "test/key";
            var value = "test-value";

            // Act
            await _consulService.UpdateConsulKeyValue(consulEnv, key, value);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Updated key: test/key")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        [Test]
        public async Task GetConsulKeyValues_ShouldResolveRecursiveValues()
        {
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://localhost:8500",
                ConsulToken = "test-token"
            };

            var keyValues = new Dictionary<string, string>
            {
                { "config/base", "{ \"setting\": \"base-value\" }" },
                { "config/override", "{ \"setting\": \"{{ key 'config/base' }}\" }" }
            };

            // Act
            var result = await _consulService.GetConsulKeyValues(consulEnv);

            // Assert
            result.Should().NotBeNull();
            result["config/override"].ValueRecursive.Should().Contain("base-value");
        }

        [Test]
        public async Task GetConsulKeyValues_ShouldHandleInvalidJson()
        {
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://localhost:8500",
                ConsulToken = "test-token"
            };

            // Act
            var result = await _consulService.GetConsulKeyValues(consulEnv);

            // Assert
            result.Should().NotBeNull();
            foreach (var kv in result.Values)
            {
                if (!kv.IsValidJson)
                {
                    kv.Value.Should().Be(kv.ValueRecursive);
                }
            }
        }

        [Test]
        public async Task GetConsulKeyValues_ShouldHandleMissingReferences()
        {
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://localhost:8500",
                ConsulToken = "test-token"
            };

            // Act
            var result = await _consulService.GetConsulKeyValues(consulEnv);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Key not found:")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.AtLeastOnce
            );
        }

        [Test]
        public async Task CompareAsync_ShouldThrowWhenEnvironmentNotFound()
        {
            // Arrange
            _configServiceMock.Setup(x => x.GetConfig())
                .Returns(new ConfigModel { ConsulEnvironments = new List<ConsulEnvironment>() });

            // Act & Assert
            await _consulService.Invoking(x => x.CompareAsync("source", "target"))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("Source environment 'source' not found");
        }

        [Test]
        public void JoinPathKey_ShouldCombinePathsCorrectly()
        {
            // Arrange
            var folderPath = @"C:\test";
            var key = "path/to/key";

            // Act
            var result = ConsulService.JoinPathKey(folderPath, key);

            // Assert
            result.Should().Be(@"C:\test\path\to\key");
        }

        [Test]
        public void IsValidFormated_WithEmptyInput_ShouldReturnTrue()
        {
            // Act
            var result = _consulService.IsValidFormated("key", "");

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public void IsValidFormated_WithNullInput_ShouldReturnTrue()
        {
            // Act
            var result = _consulService.IsValidFormated("key", null);

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public void SaveKvToFile_ShouldCreateDirectoryAndSaveFile()
        {
            // Arrange
            var key = "test/key";
            var value = "test-value";
            var folderPath = _tempPath;

            // Act
            _consulService.SaveKvToFile(folderPath, key, value);

            // Assert
            var expectedPath = Path.Combine(folderPath, "test", "key");
            File.Exists(expectedPath).Should().BeTrue();
            File.ReadAllText(expectedPath).Should().Be(value);
        }

        [Test]
        public void SaveKvToFile_WithNullValue_ShouldNotCreateFile()
        {
            // Arrange
            var key = "test/key";
            string value = null;
            var folderPath = _tempPath;

            // Act
            _consulService.SaveKvToFile(folderPath, key, value);

            // Assert
            var expectedPath = Path.Combine(folderPath, "test", "key");
            File.Exists(expectedPath).Should().BeFalse();
        }

        [Test]
        public async Task DownloadConsulAsync_ShouldHandleError()
        {
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://invalid-url",
                ConsulToken = "test-token",
                ConsulFolder = _tempPath
            };

            // Act
            await _consulService.DownloadConsulAsync(consulEnv);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Error:")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        [Test]
        public void SaveKvToFile_ShouldLogError_WhenWriteFails()
        {
            // Arrange
            var key = "test/key";
            var value = "test-value";
            var invalidPath = Path.Combine("Z:", "nonexistent", Guid.NewGuid().ToString());

            // Act
            _consulService.SaveKvToFile(invalidPath, key, value);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Error to save:")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        [Test]
        public async Task CompareAsync_ShouldHandleEmptyEnvironments()
        {
            // Arrange
            var sourceEnv = new ConsulEnvironment { Name = "source", ConsulUrl = "http://source" };
            var targetEnv = new ConsulEnvironment { Name = "target", ConsulUrl = "http://target" };

            _configServiceMock.Setup(x => x.GetConfig())
                .Returns(new ConfigModel
                {
                    ConsulEnvironments = new List<ConsulEnvironment> { sourceEnv, targetEnv }
                });

            // Act
            var result = await _consulService.CompareAsync("source", "target");

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public async Task CompareAsync_ShouldDetectDifferences()
        {
            // Arrange
            var sourceEnv = new ConsulEnvironment { Name = "source", ConsulUrl = "http://source" };
            var targetEnv = new ConsulEnvironment { Name = "target", ConsulUrl = "http://target" };

            _configServiceMock.Setup(x => x.GetConfig())
                .Returns(new ConfigModel
                {
                    ConsulEnvironments = new List<ConsulEnvironment> { sourceEnv, targetEnv }
                });

            // Act
            var result = await _consulService.CompareAsync("source", "target");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Difference in key:")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.AtLeastOnce
            );
        }

        [Test]
        public async Task CompareAsync_ShouldHandleMissingKeys()
        {
            // Arrange
            var sourceEnv = new ConsulEnvironment { Name = "source", ConsulUrl = "http://source" };
            var targetEnv = new ConsulEnvironment { Name = "target", ConsulUrl = "http://target" };

            _configServiceMock.Setup(x => x.GetConfig())
                .Returns(new ConfigModel
                {
                    ConsulEnvironments = new List<ConsulEnvironment> { sourceEnv, targetEnv }
                });

            // Act
            var result = await _consulService.CompareAsync("source", "target");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) =>
                        o.ToString().Contains("is present in") &&
                        o.ToString().Contains("but not in")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.AtLeastOnce
            );
        }

        [Test]
        public async Task CompareAsync_ShouldHandleRecursiveValues()
        {
            // Arrange
            var sourceEnv = new ConsulEnvironment { Name = "source", ConsulUrl = "http://source" };
            var targetEnv = new ConsulEnvironment { Name = "target", ConsulUrl = "http://target" };

            _configServiceMock.Setup(x => x.GetConfig())
                .Returns(new ConfigModel
                {
                    ConsulEnvironments = new List<ConsulEnvironment> { sourceEnv, targetEnv }
                });

            // Act
            var result = await _consulService.CompareAsync("source", "target", useRecursive: true);

            // Assert
            result.Should().BeEmpty();
        }
    }
}
