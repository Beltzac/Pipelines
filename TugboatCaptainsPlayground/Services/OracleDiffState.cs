using Common.Models;
using System.Collections.Generic;
using TugboatCaptainsPlayground.Services.Interfaces;

namespace TugboatCaptainsPlayground.Services
{
    public class OracleDiffState : ITracksLoading, IPaginates
    {
        public string SelectedSourceEnv { get; set; }
        public string SelectedTargetEnv { get; set; }

        public Dictionary<string, OracleViewDefinition> SourceViews { get; set; } = new();
        public Dictionary<string, OracleViewDefinition> TargetViews { get; set; } = new();
        public HashSet<string> AllViewNames { get; set; } = new();
        public Dictionary<string, OracleDiffResult> Differences { get; set; } = new();

        public bool ShowOnlyChanged { get; set; }
        public string SearchKey { get; set; }

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }

        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
    }
}
