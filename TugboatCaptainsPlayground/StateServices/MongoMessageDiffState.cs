using Common.Models;
using Common.Services.Interfaces;
using System.Collections.Concurrent;

namespace TugboatCaptainsPlayground.Services
{
    public class MongoMessageDiffState : ITracksLoading, IComparesItems<string, MongoMessage, MongoMessageDiffResult>
    {
        public string SelectedSourceEnv { get; set; }
        public string SelectedTargetEnv { get; set; }
        public string SearchKey { get; set; }
        public string SearchText { get; set; }
        public bool ShowOnlyChanged { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalItems { get; set; }
        public ConcurrentDictionary<string, MongoMessageDiffResult> DiffCache { get; set; } = new();
        public List<MongoMessageDiffResult> PageItems { get; set; } = new();
        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; }
        public HashSet<string> AllKeys { get; set; } = new();
        public Dictionary<string, MongoMessage> SourceValues { get; set; } = new();
        public Dictionary<string, MongoMessage> TargetValues { get; set; } = new();
        public int TotalCount { get; set; } = new();
    }
}