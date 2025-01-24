using Common.Models;
using Common.Services;
using FluentAssertions;
using Flurl.Http.Testing;
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

        public ConsulServiceTests()
        {
            _loggerMock = new Mock<ILogger<ConsulService>>();
            _configServiceMock = new Mock<IConfigurationService>();
            _consulService = new ConsulService(_loggerMock.Object, _configServiceMock.Object);
        }

        [Test]
        public async Task GetConsulKeyValues_ShouldFallbackToSequential_WhenBatchFails()
        {
            using var _httpTest = new HttpTest();
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://localhost:8500",
                ConsulToken = "test-token"
            };

            // Mock batch failure
            _httpTest
                .ForCallsTo("*/v1/kv/")
                .WithQueryParam("recurse")
                .SimulateException(new Exception("Batch failed"));

            // Mock sequential success
            _httpTest
                .ForCallsTo("*/v1/kv/")
                .WithQueryParam("keys")
                .RespondWithJson(new[] { "key1", "key2" });

            _httpTest
                .ForCallsTo("*/v1/kv/key1")
                .RespondWithJson(new object[] { new {
                    Key = "key1",
                    Value = Convert.ToBase64String(Encoding.UTF8.GetBytes("value1"))
                } });

            _httpTest
                .ForCallsTo("*/v1/kv/key2")
                .RespondWithJson(new object[] { new {
                    Key = "key2",
                    Value = Convert.ToBase64String(Encoding.UTF8.GetBytes("value2"))
                } });

            // Act
            var result = await _consulService.GetConsulKeyValues(consulEnv);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result["key1"].Value.Should().Be("value1");
            result["key2"].Value.Should().Be("value2");
        }

        [Test]
        public async Task GetConsulKeyValues_ShouldHandleEmptyValues()
        {
            using var _httpTest = new HttpTest();
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://localhost:8500",
                ConsulToken = "test-token"
            };

            // Mock sequential success with empty values
            _httpTest
                .ForCallsTo("*/v1/kv/")
                .WithQueryParam("keys")
                .RespondWithJson(new[] { "key1", "key2" });

            _httpTest
                .ForCallsTo("*/v1/kv/key1")
                .RespondWithJson(new object[] { new {
                    Key = "key1",
                    Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(""))
                } });

            _httpTest
                .ForCallsTo("*/v1/kv/key2")
                .RespondWithJson(new object[] { new {
                    Key = "key2",
                    Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(""))
                } });

            // Act
            var result = await _consulService.GetConsulKeyValues(consulEnv);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result["key1"].Value.Should().Be("");
            result["key2"].Value.Should().Be("");
        }

        [Test]
        public async Task GetConsulKeyValues_ShouldIncludeUrlInResult()
        {
            using var _httpTest = new HttpTest();
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://localhost:8500",
                ConsulToken = "test-token"
            };

            // Mock sequential success with URL
            _httpTest
                .ForCallsTo("*/v1/kv/")
                .WithQueryParam("keys")
                .RespondWithJson(new[] { "key1", "key2" });

            _httpTest
                .ForCallsTo("*/v1/kv/key1")
                .RespondWithJson(new object[] { new {
                    Key = "key1",
                    Value = Convert.ToBase64String(Encoding.UTF8.GetBytes("value1"))
                } });

            _httpTest
                .ForCallsTo("*/v1/kv/key2")
                .RespondWithJson(new object[] { new {
                    Key = "key2",
                    Value = Convert.ToBase64String(Encoding.UTF8.GetBytes("value2"))
                } });

            // Act
            var result = await _consulService.GetConsulKeyValues(consulEnv);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result["key1"].Url.Should().Be("http://localhost:8500/ui/kv/key1/edit");
            result["key2"].Url.Should().Be("http://localhost:8500/ui/kv/key2/edit");
        }

        [Test]
        public async Task GetConsulKeyValues_ShouldHandleInvalidJson()
        {
            using var _httpTest = new HttpTest();
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://localhost:8500",
                ConsulToken = "test-token"
            };

            // Mock sequential success with invalid JSON
            _httpTest
                .ForCallsTo("*/v1/kv/")
                .WithQueryParam("keys")
                .RespondWithJson(new[] { "key1", "key2" });

            _httpTest
                .ForCallsTo("*/v1/kv/key1")
                .RespondWithJson(new object[] { new {
                    Key = "key1",
                    Value = Convert.ToBase64String(Encoding.UTF8.GetBytes("{invalid json}"))
                } });

            _httpTest
                .ForCallsTo("*/v1/kv/key2")
                .RespondWithJson(new object[] { new {
                    Key = "key2",
                    Value = Convert.ToBase64String(Encoding.UTF8.GetBytes("{invalid json}"))
                } });

            // Act
            var result = await _consulService.GetConsulKeyValues(consulEnv);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result["key1"].Value.Should().Be("{invalid json}");
            result["key2"].Value.Should().Be("{invalid json}");
        }

        [Test]
        public async Task GetConsulKeyValues_ShouldResolveRecursiveValues()
        {
            using var _httpTest = new HttpTest();
            // Arrange
            var consulEnv = new ConsulEnvironment
            {
                ConsulUrl = "http://localhost:8500",
                ConsulToken = "test-token"
            };

            // Mock sequential success with recursive values
            _httpTest
                .ForCallsTo("*/v1/kv/")
                .WithQueryParam("keys")
                .RespondWithJson(new[] { "config/base", "config/override" });

            _httpTest
                .ForCallsTo("*/v1/kv/config/base")
                .RespondWithJson(new object[] { new {
                    Key = "config/base",
                    Value = Convert.ToBase64String(Encoding.UTF8.GetBytes("{ \"setting\": \"base-value\" }"))
                } });

            _httpTest
                .ForCallsTo("*/v1/kv/config/override")
                .RespondWithJson(new object[] { new {
                    Key = "config/override",
                    Value = Convert.ToBase64String(Encoding.UTF8.GetBytes("{ \"setting\": \"{{ key 'config/base' }}\" }"))
                } });

            // Act
            var result = await _consulService.GetConsulKeyValues(consulEnv);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result["config/override"].ValueRecursive.Should().Contain("base-value");
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
            using var _httpTest = new HttpTest();
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
                Times.AtLeastOnce
            );
        }
    }
}
