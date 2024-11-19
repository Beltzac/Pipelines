namespace Front2.Services
{
    public class OracleDiffState
    {
        public string SelectedSourceEnv { get; set; }
        public string SelectedTargetEnv { get; set; }
        public Dictionary<string, string> Differences { get; set; }
    }
}
