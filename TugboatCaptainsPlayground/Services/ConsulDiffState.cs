using Common.Models;
using Common.Services;

namespace TugboatCaptainsPlayground.Services
{
    public class ConsulDiffState : StateBase
    {
        public string SelectedSourceEnv { get; set; }
        public string SelectedTargetEnv { get; set; }
        public bool UseRecursive { get; set; } = true;
        public Dictionary<string, string> Differences { get; set; } = new();
        public HashSet<string> AllKeys { get; set; } = new();
        public Dictionary<string, ConsulKeyValue> SourceKeyValues { get; set; } = new();
        public Dictionary<string, ConsulKeyValue> TargetKeyValues { get; set; } = new();
        public string SearchKey { get; set; }
        public int PageSize { get; set; } = 10;
        public int CurrentPage { get; set; } = 1;
        public int TotalCount { get; set; }
    }
}