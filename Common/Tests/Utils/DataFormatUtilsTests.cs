using Xunit;
using Common.Utils;

namespace Common.Tests.Utils
{
    public class DataFormatUtilsTests
    {
        [Fact]
        public void FormatJson_ValidJson_ReturnsFormattedJson()
        {
            // Arrange
            var input = "{\"name\":\"test\",\"value\":123}";
            var expected = "{\r\n  \"name\": \"test\",\r\n  \"value\": 123\r\n}";

            // Act
            var result = DataFormatUtils.FormatJson(input);

            // Assert
            Assert.Equal(expected, result.Replace("\n", "\r\n"));
        }

        [Fact]
        public void FormatJson_InvalidJson_ReturnsOriginalString()
        {
            // Arrange
            var input = "invalid json";

            // Act
            var result = DataFormatUtils.FormatJson(input);

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void IsJson_ValidJson_ReturnsTrue()
        {
            // Arrange
            var input = "{\"name\":\"test\"}";

            // Act
            var result = DataFormatUtils.IsJson(input);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsJson_InvalidJson_ReturnsFalse()
        {
            // Arrange
            var input = "invalid json";

            // Act
            var result = DataFormatUtils.IsJson(input);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void JsonToXml_ValidJson_ReturnsXml()
        {
            // Arrange
            var input = "{\"name\":\"test\",\"value\":123}";

            // Act
            var result = DataFormatUtils.JsonToXml(input);

            // Assert
            Assert.Contains("<name>test</name>", result);
            Assert.Contains("<value>123</value>", result);
        }

        [Fact]
        public void XmlToJson_ValidXml_ReturnsJson()
        {
            // Arrange
            var input = "<root><name>test</name><value>123</value></root>";

            // Act
            var result = DataFormatUtils.XmlToJson(input);

            // Assert
            Assert.Contains("\"name\": \"test\"", result);
            Assert.Contains("\"value\": \"123\"", result);
        }
    }
}
