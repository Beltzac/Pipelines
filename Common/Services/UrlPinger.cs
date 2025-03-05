using System.Text.RegularExpressions;
using urldetector;
using urldetector.detection;

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

            UrlDetector parser = new UrlDetector(text, UrlDetectorOptions.JSON, new HashSet<string>
            {
                "http", "https", "ftp", "ftps", "sftp", "ws", "wss", "telnet"
            });
            List<Url> found = parser.Detect();
            return found.Select(x => x.GetFullUrl().TrimEnd('.'));
        }
    }
}
