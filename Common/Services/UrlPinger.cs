using System.Text.RegularExpressions;

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

        public static IEnumerable<string> ExtractUrls(string text)
        {
            if (text == null)
            {
                return [];
            }

            const string pattern = @"\b(?:https?://)(?:(?:[0-9]{1,3}\.){3}[0-9]{1,3}|(?:[a-z0-9-]+(?:\.[a-z0-9-]+)*\.[a-z]{2,}))(?::\d+)?(?:/\S*)?\b";
            return Regex.Matches(text, pattern)
                        .Select(m => m.Value)
                        .Where(url => Uri.TryCreate(url, UriKind.Absolute, out _));
        }
    }
}
