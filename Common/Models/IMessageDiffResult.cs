namespace Common.Models
{
    public interface IDiffResult
    {
        string FormattedDiff { get; set; }
        bool HasDifferences { get; set; }
    }
}