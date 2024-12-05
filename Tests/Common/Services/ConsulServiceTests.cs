using Common.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

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
        [Arguments("key", "{ \"teste\": \"{{ key '_infra/common/logging/default-config' }}\"}", false)]
        [Arguments("key", "{ \"teste\": \"BlaBlaBla\"}", true)]
        public void IsValidFormated_ShouldValidateCorrectly(string key, string value, bool expected)
        {
            // Act
            var result = _consulService.IsValidFormated(key, value);

            // Assert
            result.Should().Be(expected);
        }
    }
}
