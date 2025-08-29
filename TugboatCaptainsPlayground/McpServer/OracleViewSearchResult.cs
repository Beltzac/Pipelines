using Common.Services.Interfaces;

namespace TugboatCaptainsPlayground.McpServer
{
    public class OracleViewSearchResult
    {
        public string ViewName { get; set; } = string.Empty;
        public List<string> MatchingLines { get; set; } = new();
        public float Similarity { get; set; }
    }
}