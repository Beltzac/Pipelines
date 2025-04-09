using Common.Models;
using Common.Services.Interfaces;

namespace TugboatCaptainsPlayground.Services
{
    public class ConsulState : ITracksLoading
    {
        public string SelectedConsulEnv { get; set; }
        public Dictionary<string, ConsulKeyValue> ConsulKeyValues { get; set; } = new();
        public List<string> VisibleKeys { get; set; } = new();
        public string SearchKey { get; set; } = string.Empty;
        public string SearchValue { get; set; } = string.Empty;
        public bool IsRecursive { get; set; }
        public bool ShowInvalidOnly { get; set; }

        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
    }
}
