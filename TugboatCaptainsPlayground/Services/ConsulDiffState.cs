using Common.Models;
using Common.Services;
using TugboatCaptainsPlayground.Services.Interfaces;

namespace TugboatCaptainsPlayground.Services
{
    public class ConsulDiffState : ITracksLoading, IComparesItems<string, ConsulKeyValue, ConsulDiffResult>
    {
        public string SelectedSourceEnv { get; set; }
        public string SelectedTargetEnv { get; set; }

        public List<ConsulDiffResult> PageItems { get; set; } = new();
        public HashSet<string> AllKeys { get; set; } = new();
        public Dictionary<string, ConsulKeyValue> SourceValues { get; set; } = new();
        public Dictionary<string, ConsulKeyValue> TargetValues { get; set; } = new();

        public bool UseRecursive { get; set; } = true;
        public bool ShowOnlyChanged { get; set; } = true;
        public string SearchKey { get; set; }

        public int PageSize { get; set; } = 10;
        public int CurrentPage { get; set; } = 1;
        public int TotalCount { get; set; }

        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
    }
}