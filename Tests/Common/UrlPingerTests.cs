﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System.Collections.Generic;
using System.Linq;
using Common.Services;
using FluentAssertions;

namespace Tests.Common
{
    public class UrlPingerTests
    {
        [Test]
        [Arguments("Visit https://example.com", new[] { "https://example.com/" })]
        [Arguments("Check http://test.com and https://api.test.com/v1",
    new[] { "http://test.com", "https://api.test.com/v1" })]
        [Arguments("No URLs here", new string[0])]
        [Arguments("Invalid URL test.com", new[] { "http://test.com" })]
        [Arguments("Multiple spaces https://example.com  https://test.com/",
    new[] { "https://example.com/", "https://test.com/" })]
        [Arguments("IP address http://192.168.1.1:8080", new[] { "http://192.168.1.1:8080" })]
        [Arguments("Mixed case HTTPS://Example.COM", new[] { "https://example.com/" })]
        [Arguments("URL with path https://example.com/path/to/resource",
    new[] { "https://example.com/path/to/resource" })]
        [Arguments("URL with query https://example.com?param=value",
    new[] { "https://example.com/?param=value" })]
        [Arguments("Multiple lines\nhttps://line1.com\nhttps://line2.com",
    new[] { "https://line1.com", "https://line2.com" })]
        [Arguments("FTP protocol ftp://ftp.example.com", new[] { "ftp://ftp.example.com/" })]
        [Arguments("WebSocket link ws://chat.example.com", new[] { "ws://chat.example.com/" })]
        [Arguments("Secure WebSocket wss://secure-chat.example.com", new[] { "wss://secure-chat.example.com/" })]
        //[Arguments("Email link mailto:contact@example.com", new[] { "mailto:contact@example.com" })]
        [Arguments("Authentication URL http://user:pass@auth.example.com",
    new[] { "http://user:pass@auth.example.com/" })]
        [Arguments("IPv6 address http://[2001:db8::1]:8080/path",
    new[] { "http://[2001:db8::1]:8080/path" })]
        [Arguments("URL with fragment https://example.com/#anchor",
    new[] { "https://example.com/#anchor/" })]
        [Arguments("Encoded space in path https://example.com/file%20name.txt",
    new[] { "https://example.com/file%20name.txt" })]
        [Arguments("Hyphenated domain https://my-site.example.org",
    new[] { "https://my-site.example.org/" })]
        [Arguments("Long TLD https://example.travel", new[] { "https://example.travel/" })]
        [Arguments("Parentheses around (https://parenthesis-example.org)",
    new[] { "https://parenthesis-example.org/" })]
        [Arguments("Markdown link [text](http://markdown-example.org)",
    new[] { "http://markdown-example.org/" })]
        [Arguments("HTML anchor <a href='http://html-example.net'>link</a>",
    new[] { "http://html-example.net/" })]
        [Arguments("Multiple semicolons https://ex.co/;param?q=1;r=2",
    new[] { "https://ex.co/;param?q=1;r=2" })]
        [Arguments("Comma in path https://ex.co/a,b,c.html",
    new[] { "https://ex.co/a,b,c.html" })]
        [Arguments("URL with fragment https://example.com#section1", new[] { "https://example.com/#section1" })]
        [Arguments("URL with URL-encoded characters https://example.com?q=%20", new[] { "https://example.com/?q=%20/" })]
        //[Arguments("ftps://ftp.example.com", new[] { "ftps://ftp.example.com/" })]
        [Arguments("sftp://sftp.example.com", new[] { "sftp://sftp.example.com/" })]
        [Arguments("ws://example.com/socket", new[] { "ws://example.com/socket/" })]
        [Arguments("wss://example.com/socket", new[] { "wss://example.com/socket/" })]
        [Arguments("telnet://telnet.example.com", new[] { "telnet://telnet.example.com/" })]
        [Arguments("Internal IP address http://10.0.0.1", new[] { "http://10.0.0.1/" })]
        [Arguments("Private IP address http://172.16.0.1", new[] { "http://172.16.0.1/" })]
        [Arguments("Localhost http://127.0.0.1", new[] { "http://127.0.0.1/" })]
        [Arguments("IPv6 address http://[2001:db8::1]", new[] { "http://[2001:db8::1]/" })]
        [Arguments("IPv6 address with port http://[2001:db8::1]:8080", new[] { "http://[2001:db8::1]:8080/" })]
        [Arguments("IP address without protocol 192.168.1.1", new [] { "http://192.168.1.1" })]
        [Arguments("IP address with port without protocol 192.168.1.1:8080", new[] { "http://192.168.1.1:8080" })]
        //[Arguments("IP address surrounded by text something192.168.1.1something", new string[0])]
        [Arguments("Url at the end of the line. https://endofline.com", new[] { "https://endofline.com/" })]
        //[Arguments("Url followed by punctuation. https://punctuation.com\"", new[] { "https://punctuation.com/" })]
        //[Arguments("Url followed by punctuation. https://punctuation.com,", new[] { "https://punctuation.com/" })]
        //[Arguments("Url followed by punctuation. https://punctuation.com!", new[] { "https://punctuation.com/" })]
        //[Arguments("Url followed by punctuation. https://punctuation.com?", new[] { "https://punctuation.com/" })]
        //[Arguments("Url followed by punctuation. https://punctuation.com;", new[] { "https://punctuation.com/" })]
        //[Arguments("Url followed by punctuation. https://punctuation.com)", new[] { "https://punctuation.com/" })]
        [Arguments("Nested URL-like strings inside a URL https://example.com/path?q=http://nested.com", new[] { "https://example.com/path?q=http://nested.com/" })]
        [Arguments("Mixed case and weird characters HttPs://ExAmPlE-.com", new[] { "https://example-.com" })]
        [Arguments("Multiple URLs, some invalid, some with extra characters around them  invalidurl https://valid.com invalidurl. https://anothervalid.com, invalidurl https://yetvalid.com; invalidurl", new[] { "https://valid.com/", "https://anothervalid.com/", "https://yetvalid.com/" })]
        [Arguments("Complex URL: https://user:password@sub.example.com:8080/path/to/resource?param1=value1&param2=value2#fragment ", new[] { "https://user:password@sub.example.com:8080/path/to/resource?param1=value1&param2=value2#fragment/" })]
        [Arguments("URL with encoded space https://example.com/path%20with%20space", new[] { "https://example.com/path%20with%20space/" })]
        [Arguments("URL with plus sign https://example.com?param=a+b", new[] { "https://example.com/?param=a+b/" })]
        [Arguments("URL with ampersand https://example.com?param1=a&param2=b", new[] { "https://example.com/?param1=a&param2=b/" })]
        [Arguments("URL with equals sign https://example.com?param=a=b", new[] { "https://example.com/?param=a=b/" })]
        [Arguments("Edge case: Empty input", new string[0])]
        [Arguments("Edge case: Only whitespace", new string[0])]
        public void ExtractUrls_ReturnsCorrectUrls(string input, string[] expected)
        {
            // Arrange
            var expectedUrls = expected.ToList();

            // Act
            var result = UrlPinger.ExtractUrls(input).ToList();

            // Assert
            result.Select(url => url.TrimEnd('/').TrimEnd('?').ToLowerInvariant())
                .Should()
                .BeEquivalentTo(expectedUrls.Select(url => url.TrimEnd('/').TrimEnd('?').ToLowerInvariant()));

            // Additional assertion to verify URLs can be parsed by Uri class
            foreach (var url in result)
            {
                Action act = () => new Uri(url);
                act.Should().NotThrow("because all extracted URLs should be valid URIs");
            }
        }

        [Test]
        public void ExtractUrls_EmptyString_ReturnsEmpty()
        {
            // Arrange
            var input = string.Empty;

            // Act
            var result = UrlPinger.ExtractUrls(input);

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void ExtractUrls_NullString_ReturnsEmpty()
        {
            // Arrange
            string input = null;

            // Act
            var result = UrlPinger.ExtractUrls(input);

            // Assert
            result.Should().BeEmpty();
        }
    }
}