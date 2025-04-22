using CSharpDiff.Patches.Models;

namespace Common.Models
{
    public class ConsulDiffResult : IDiffResult
    {
        public ConsulDiffResult(string key, string formattedDiff, bool hasDifferences)
        {
            Key = key;
            FormattedDiff = formattedDiff;
            HasDifferences = hasDifferences;
        }

        public string Key { get; set; }
        public string FormattedDiff { get; set; }
        public bool HasDifferences { get; set; }
        public PatchResult Patch { get; set; }
    };
}
