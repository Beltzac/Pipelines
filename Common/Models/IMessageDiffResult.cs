using CSharpDiff.Patches.Models;

namespace Common.Models
{
    public interface IDiffResult
    {
        string Key { get; set; }
        string FormattedDiff { get; set; }
        PatchResult Patch { get; set; }
        bool HasDifferences { get; set; }
    }
}