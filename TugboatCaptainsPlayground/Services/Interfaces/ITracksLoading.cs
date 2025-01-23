namespace TugboatCaptainsPlayground.Services.Interfaces
{
    public interface ITracksLoading
    {
        bool IsLoading { get; set; }
        int? ProgressValue { get; set; }
        string ProgressLabel { get; set; }
    }
}
