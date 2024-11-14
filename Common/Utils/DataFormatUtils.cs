
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                if (IsJson(json))
                {
                    var element = JsonConvert.DeserializeObject<JToken>(json);
                    return JsonConvert.SerializeObject(element, Newtonsoft.Json.Formatting.Indented);
                }

                return json;
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
                JsonConvert.DeserializeObject<JToken>(text);
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
                XmlDocument doc = JsonConvert.DeserializeXmlNode(json, "jsonObject");
                return doc.OuterXml;
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
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                return JsonConvert.SerializeXmlNode(doc);
            }
            catch
            {
                return xml;
            }
        }
    }
}
