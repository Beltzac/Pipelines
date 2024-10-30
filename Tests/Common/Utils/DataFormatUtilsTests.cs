using Common.Utils;
using FluentAssertions;

namespace Common.Tests.Utils
{
    public class DataFormatUtilsTests
    {
        [Test]
        public void FormatJson_ValidJson_ReturnsFormattedJson()
        {
            // Arrange
            var input = "{\"name\":\"test\",\"value\":123}";
            var expected = "{\r\n  \"name\": \"test\",\r\n  \"value\": 123\r\n}";

            // Act
            var result = DataFormatUtils.FormatJson(input);

            // Assert
            result.Replace("\n", "\r\n").Should().Be(expected);
        }

        [Test]
        public void FormatJson_InvalidJson_ReturnsOriginalString()
        {
            // Arrange
            var input = "invalid json";

            // Act
            var result = DataFormatUtils.FormatJson(input);

            // Assert
            result.Should().Be(input);
        }

        [Test]
        public void IsJson_ValidJson_ReturnsTrue()
        {
            // Arrange
            var input = "{\"name\":\"test\"}";

            // Act
            var result = DataFormatUtils.IsJson(input);

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public void IsJson_InvalidJson_ReturnsFalse()
        {
            // Arrange
            var input = "invalid json";

            // Act
            var result = DataFormatUtils.IsJson(input);

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void JsonToXml_ValidJson_ReturnsXml()
        {
            // Arrange
            var input = "{\"name\":\"test\",\"value\":123}";

            // Act
            var result = DataFormatUtils.JsonToXml(input);

            // Assert
            result.Should().Contain("<name>test</name>");
            result.Should().Contain("<value>123</value>");
        }

        [Test]
        public void XmlToJson_ValidXml_ReturnsJson()
        {
            // Arrange
            var input = "<root><name>test</name><value>123</value></root>";

            // Act
            var result = DataFormatUtils.XmlToJson(input);

            // Assert
            result.Should().Contain("\"name\": \"test\"");
            result.Should().Contain("\"value\": \"123\"");
        }
    }
}
