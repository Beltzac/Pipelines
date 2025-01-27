namespace TugboatCaptainsPlayground.Services.Interfaces
{
    public interface IComparesItems<TKey, T1, T> : IPaginates<T> where T : class
    {
        HashSet<TKey> AllKeys { get; set; }
        Dictionary<TKey, T1> SourceValues { get; set; }
        Dictionary<TKey, T1> TargetValues { get; set; }
    }

    public interface IPaginates<T> : IPaginates where T : class
    {
        List<T> PageItems { get; set; }
    }

    public interface IPaginates
    {
        int CurrentPage { get; set; }
        int PageSize { get; set; }
        int TotalCount { get; set; }
    }
}