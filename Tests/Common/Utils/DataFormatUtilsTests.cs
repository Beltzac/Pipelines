using Common.Utils;
using FluentAssertions;

namespace Tests.Common.Utils
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
            result.Should().Be(expected);
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
            result.Should().Contain("\"name\":\"test\"");
            result.Should().Contain("\"value\":\"123\"");
        }

        [Test]
        public void JsonToXml_ArrayJson_ReturnsCorrectXml()
        {
            // Arrange
            var input = "{\"items\":[1,2,3],\"names\":[\"a\",\"b\"]}";

            // Act
            var result = DataFormatUtils.JsonToXml(input);

            // Assert
            result.Should().Contain("<items>1</items><items>2</items><items>3</items>");
            result.Should().Contain("<names>a</names><names>b</names>");
        }

        [Test]
        public void XmlToJson_ArrayXml_ReturnsCorrectJson()
        {
            // Arrange
            var input = "<root><items>1</items><items>2</items><items>3</items></root>";

            // Act
            var result = DataFormatUtils.XmlToJson(input);

            // Assert
            result.Should().Contain("\"items\":[");
            result.Should().Contain("\"1\"");
            result.Should().Contain("\"2\"");
            result.Should().Contain("\"3\"");
        }

        [Test]
        public void JsonToXml_ComplexArrayJson_ReturnsCorrectXml()
        {
            // Arrange
            var input = "{\"users\":[{\"name\":\"John\",\"age\":30},{\"name\":\"Jane\",\"age\":25}]}";

            // Act
            var result = DataFormatUtils.JsonToXml(input);

            // Assert
            result.Should().Contain("<users>");
            result.Should().Contain("<name>John</name><age>30</age>");
            result.Should().Contain("<name>Jane</name><age>25</age>");
            result.Should().Contain("</users>");
        }

        [Test]
        public void JsonToXml_SingleElementArrayJson_ReturnsCorrectXml()
        {
            // Arrange
            var input = "{\"items\":[42],\"names\":[\"single\"]}";

            // Act
            var result = DataFormatUtils.JsonToXml(input);

            // Assert
            result.Should().Contain("<items>42</items>");
            result.Should().Contain("<names>single</names>");
        }

        [Test]
        public void XmlToJson_SingleElementArrayXml_ReturnsCorrectJson()
        {
            // Arrange
            var input = "<root><items>42</items><names><item>single</item></names></root>";

            // Act
            var result = DataFormatUtils.XmlToJson(input);

            // Assert
            result.Should().NotContain("\"items\":[");
            result.Should().Contain("\"42\"");
            result.Should().Contain("\"single\"");
            result.Should().Contain("\"item\":");
        }
    }
}
