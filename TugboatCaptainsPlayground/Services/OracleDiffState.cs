using Common.Models;
using Common.Services.Interfaces;
using System.Collections.Concurrent;

namespace TugboatCaptainsPlayground.Services
{
    public class OracleDiffState : ITracksLoading, IComparesItems<string, OracleViewDefinition, OracleDiffResult>
    {
        public string SelectedSourceEnv { get; set; }
        public string SelectedTargetEnv { get; set; }

        public Dictionary<string, OracleViewDefinition> SourceValues { get; set; } = new();
        public Dictionary<string, OracleViewDefinition> TargetValues { get; set; } = new();
        public HashSet<string> AllKeys { get; set; } = new();
        public ConcurrentDictionary<string, OracleDiffResult> DiffCache { get; set; } = new();

        public bool ShowOnlyChanged { get; set; }
        public string SearchKey { get; set; }

        public List<OracleDiffResult> PageItems { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }

        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
    }
}
