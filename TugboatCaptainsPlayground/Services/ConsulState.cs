using Common.Models;

namespace TugboatCaptainsPlayground.Services
{
    public class ConsulState : StateBase
    {
        public string SelectedConsulEnv { get; set; }
        public Dictionary<string, ConsulKeyValue> ConsulKeyValues { get; set; } = new();
        public List<string> VisibleKeys { get; set; } = new();
        public string SearchKey { get; set; } = string.Empty;
        public string SearchValue { get; set; } = string.Empty;
        public bool IsRecursive { get; set; }
        public bool ShowInvalidOnly { get; set; }
    }
}
