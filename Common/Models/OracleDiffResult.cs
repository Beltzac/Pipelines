using CSharpDiff.Patches.Models;

namespace Common.Models
{
    public class OracleDiffResult : IDiffResult
    {
        public string Key { get; set; }
        public string FormattedDiff { get; set; }
        public bool HasDifferences { get; set; }
        public PatchResult Patch { get; set; }
        public OracleDiffResult(string key, string formattedDiff, bool hasDifferences)
        {
            Key = key;
            FormattedDiff = formattedDiff;
            HasDifferences = hasDifferences;
        }
    }
}