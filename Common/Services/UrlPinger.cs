using Common.Repositories.Interno.Interfaces;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using urldetector;
using urldetector.detection;

namespace Common.Services
{
    public class UrlPinger
    {
        private readonly HttpClient _client;

        private readonly IRepositoryDatabase _repositoryDatabase;

        public UrlPinger(IRepositoryDatabase repositoryDatabase)
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

            _repositoryDatabase = repositoryDatabase;
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

        public async Task<List<string>> ExtractUrls(string text)
        {
            if (text == null)
            {
                return [];
            }

            var assemblies = await _repositoryDatabase.GetAllAssemblies();

            var assembliesClean = assemblies
                .Select(x => x.Replace(".Consumer", "").Replace(".Api", "").Replace(".Domain", "").Replace(".Tests", "").Replace(".Test", "").Replace(".Events", "").Replace(".Infrastructure", "").Replace(".Infraestrutura", ""))
                .Distinct()
                .ToList();

            UrlDetector parser = new UrlDetector(text, UrlDetectorOptions.JSON, new HashSet<string>
            {
                "http", "https", "ftp", "ftps", "sftp", "ws", "wss", "telnet"
            });

            List<Url> found = parser.Detect();

            return found
                .Where(x => !assembliesClean.Any(y => x.GetHost().Contains(y)))
                .Select(x => x.GetFullUrl().TrimEnd('.'))
                .Where(x => !x.Contains('@'))
                .ToList();
        }
    }
}
