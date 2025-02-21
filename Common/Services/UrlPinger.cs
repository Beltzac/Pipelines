using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Services
{
    public class UrlPinger
    {
        private readonly HttpClient _client;

        public UrlPinger()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };

            _client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        public async Task<bool> PingUrlAsync(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _client.GetAsync(url, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<Dictionary<string, bool>> PingUrlsInTextAsync(string text)
        {
            var urls = ExtractUrls(text).Distinct().ToList();
            var result = new Dictionary<string, bool>();

            using var client = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            })
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            var tasks = urls.Select(async url =>
            {
                try
                {
                    var response = await client.GetAsync(url);
                    return (url, response.IsSuccessStatusCode);
                }
                catch
                {
                    return (url, false);
                }
            });

            foreach (var task in await Task.WhenAll(tasks))
            {
                result[task.url] = task.Item2;
            }

            return result;
        }

        private static IEnumerable<string> ExtractUrls(string text)
        {
            const string pattern = @"(https?://[^\s]+)";
            return Regex.Matches(text, pattern)
                        .Select(m => m.Value)
                        .Where(url => Uri.TryCreate(url, UriKind.Absolute, out _));
        }
    }
}
