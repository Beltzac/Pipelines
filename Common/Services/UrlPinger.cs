using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Common.Services
{
    public class UrlPinger
    {
        public static async Task<Dictionary<string, bool>> PingUrlsInTextAsync(string text)
        {
            var urls = ExtractUrls(text).Distinct().ToList();
            var result = new Dictionary<string, bool>();

            // Configure HTTP client with certificate validation disabled and timeout
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };

            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            // Process all URLs concurrently while limiting maximum concurrency
            const int maxConcurrentRequests = 10;
            using var throttler = new SemaphoreSlim(maxConcurrentRequests);

            var tasks = urls.Select(async url =>
            {
                await throttler.WaitAsync();
                try
                {
                    var response = await client.GetAsync(url);
                    return (url, response.IsSuccessStatusCode);
                }
                catch (HttpRequestException) // Network errors
                {
                    return (url, false);
                }
                catch (TaskCanceledException) // Timeouts
                {
                    return (url, false);
                }
                finally
                {
                    throttler.Release();
                }
            });

            foreach (var task in await Task.WhenAll(tasks))
                result.Add(task.Item1, task.Item2);

            return result;
        }

        private static IEnumerable<string> ExtractUrls(string text)
        {
            // Regex pattern matches both HTTP and HTTPS URLs including ports and paths
            const string pattern = @"(?i)\bhttps?:\/\/(?:www\.)?(?:[a-z0-9\-]+\.)+[a-z]{2,}(?:/[^/\s]*)*(?:\.\w+)?(?::\d+)?(?:\?\S*)?(?:#\S*)?";

            return Regex.Matches(text, pattern)
                        .Select(m => m.Value)
                        .Where(url => Uri.TryCreate(url, UriKind.Absolute, out _));

        }
    }
}
