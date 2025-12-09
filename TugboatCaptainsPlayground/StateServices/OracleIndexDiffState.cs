using Common.Models;
using Common.Services.Interfaces;
using System.Collections.Concurrent;

namespace TugboatCaptainsPlayground.Services
{
    public class OracleIndexDiffState : ITracksLoading, IComparesItems<string, OracleIndexDefinition, OracleIndexDiffResult>
    {
        public string SelectedSourceEnv { get; set; }
        public string SelectedTargetEnv { get; set; }

        public Dictionary<string, OracleIndexDefinition> SourceValues { get; set; } = new();
        public Dictionary<string, OracleIndexDefinition> TargetValues { get; set; } = new();
        public HashSet<string> AllKeys { get; set; } = new();
        public ConcurrentDictionary<string, OracleIndexDiffResult> DiffCache { get; set; } = new();

        public bool ShowOnlyMissing { get; set; }
        public string SearchKey { get; set; }

        public List<OracleIndexDiffResult> PageItems { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }

        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
    }
}
