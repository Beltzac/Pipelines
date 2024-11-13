using Common.Models;

namespace Front2.Services
{
    public class ConsulState
    {
        public string SelectedConsulEnv { get; set; }
        public Dictionary<string, ConsulKeyValue> ConsulKeyValues { get; set; } = new();
        public List<string> VisibleKeys { get; set; } = new();
        public string SearchTerm { get; set; } = string.Empty;
        public bool IsRecursive { get; set; }
        public bool ShowInvalidOnly { get; set; }
    }
}
