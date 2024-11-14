using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Common.Utils
{
    public static class DataFormatUtils
    {
        public static string FormatJson(string json)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var element = JsonSerializer.Deserialize<JsonElement>(json);
                return JsonSerializer.Serialize(element, options);
            }
            catch
            {
                return json;
            }
        }

        public static string FormatXml(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                return doc.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            }
            catch
            {
                return xml;
            }
        }

        public static bool IsJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            try
            {
                JsonSerializer.Deserialize<JsonElement>(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsXml(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            try
            {
                XDocument.Parse(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string JsonToXml(string json)
        {
            try
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
                var doc = new XDocument();
                var root = new XElement("root");
                ConvertJsonElementToXml(jsonElement, root);
                doc.Add(root);
                var settings = new XmlWriterSettings
                {
                    OmitXmlDeclaration = true,
                    Indent = true,
                    NewLineOnAttributes = false
                };
                using var stringWriter = new StringWriter();
                using var xmlWriter = XmlWriter.Create(stringWriter, settings);
                doc.Save(xmlWriter);
                return stringWriter.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            }
            catch
            {
                return json;
            }
        }

        public static string XmlToJson(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var jsonObject = ConvertXElementToJson(doc.Root);
                return JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return xml;
            }
        }

        private static void ConvertJsonElementToXml(JsonElement element, XElement parent)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var child = new XElement(property.Name);
                        ConvertJsonElementToXml(property.Value, child);
                        parent.Add(child);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        var child = new XElement("item");
                        ConvertJsonElementToXml(item, child);
                        parent.Add(child);
                    }
                    break;

                default:
                    parent.Value = element.ToString();
                    break;
            }
        }

        private static object ConvertXElementToJson(XElement element)
        {
            if (!element.HasElements)
            {
                return element.Value;
            }

            if (element.Elements().All(e => e.Name == "item"))
            {
                return element.Elements().Select(e => ConvertXElementToJson(e)).ToList();
            }

            return element.Elements().ToDictionary(
                e => e.Name.LocalName,
                e => ConvertXElementToJson(e));
        }
    }
}
