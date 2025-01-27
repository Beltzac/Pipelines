using System.Collections.Generic;
using Common.Models;
using TugboatCaptainsPlayground.Services.Interfaces;

namespace TugboatCaptainsPlayground.Services
{
    public class MessageDiffState : ITracksLoading, IPaginates
    {
        public string SelectedSourceEnv { get; set; }
        public string SelectedTargetEnv { get; set; }

        public Dictionary<string, MessageDefinition> SourceMessages { get; set; } = new();
        public Dictionary<string, MessageDefinition> TargetMessages { get; set; } = new();
        public HashSet<string> AllMessageKeys { get; set; } = new();
        public Dictionary<string, MessageDiffResult> Differences { get; set; } = new();

        public string SearchKey { get; set; }
        public string SearchDescription { get; set; }

        public bool ShowOnlyChanged { get; set; }

        public int PageSize { get; set; } = 50;
        public int CurrentPage { get; set; } = 1;
        public int TotalCount { get; set; }

        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
    }
}