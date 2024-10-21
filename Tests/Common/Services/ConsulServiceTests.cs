using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using System.Net;
using FluentAssertions;
using Moq.Protected;

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
        [Arguments("C:\\path\\to\\file", true)]
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
        public async Task UpdateConsulKeyValue_ShouldLogInformation_WhenSuccessful()
        {
            // Arrange
            var consulEnv = new ConsulEnvironment { ConsulUrl = "http://localhost:8500", ConsulToken = "token" };
            var key = "test/key";
            var value = "testValue";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                });

            var httpClient = new HttpClient(handlerMock.Object);

            // Act
            await _consulService.UpdateConsulKeyValue(consulEnv, key, value);

            // Assert
            _loggerMock.Verify(
                x => x.LogInformation(It.IsAny<string>(), It.IsAny<object[]>()),
                Times.Once
            );
        }

        [Test]
        public async Task GetConsulKeyValues_ShouldReturnKeyValueDictionary()
        {
            // Arrange
            var consulEnv = new ConsulEnvironment { ConsulUrl = "http://localhost:8500", ConsulToken = "token" };
            var kvData = new JArray
            {
                new JObject
                {
                    { "Key", "test/key" },
                    { "Value", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("testValue")) }
                }
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(kvData.ToString())
                });

            var httpClient = new HttpClient(handlerMock.Object);

            // Act
            var result = await _consulService.GetConsulKeyValues(consulEnv);

            // Assert
            result.Should().NotBeNull();
            result.Keys.Should().Contain("test/key");
            result["test/key"].Value.Should().Be("testValue");
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
    }
}
