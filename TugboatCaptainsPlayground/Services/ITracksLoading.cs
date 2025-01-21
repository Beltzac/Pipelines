namespace TugboatCaptainsPlayground.Services
{
    public interface ITracksLoading
    {
        bool IsLoading { get; set; }
        int? ProgressValue { get; set; }
        string ProgressLabel { get; set; }
    }
}
