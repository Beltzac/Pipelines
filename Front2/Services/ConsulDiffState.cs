using Common.Services;

namespace Front2.Services
{
    public class ConsulDiffState
    {
        public string SelectedSourceEnv { get; set; }
        public string SelectedTargetEnv { get; set; }
        public bool UseRecursive { get; set; } = true;
        public Dictionary<string, string> Differences { get; set; } = new();
    }
}