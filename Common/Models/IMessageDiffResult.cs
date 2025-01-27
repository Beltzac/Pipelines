namespace Common.Models
{
    public interface IDiffResult
    {
        string Key { get; set; }
        string FormattedDiff { get; set; }
        bool HasDifferences { get; set; }
    }
}