namespace TugboatCaptainsPlayground.Services.Interfaces
{
    public interface IPaginates
    {
        int CurrentPage { get; set; }
        int PageSize { get; set; }
        int TotalCount { get; set; }
    }
}