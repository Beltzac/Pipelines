using Common.Models;
using System.Collections.Generic;

namespace Front2.Services
{
    public class OracleDiffState : StateBase
    {
        public string SelectedSourceEnv { get; set; }
        public string SelectedTargetEnv { get; set; }
        public Dictionary<string, OracleViewDefinition> SourceViews { get; set; } = new();
        public Dictionary<string, OracleViewDefinition> TargetViews { get; set; } = new();
        public HashSet<string> AllViewNames { get; set; } = new();
        public Dictionary<string, OracleDiffResult> Differences { get; set; } = new();
        public string SearchKey { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
    }
}
