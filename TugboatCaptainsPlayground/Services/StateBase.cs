namespace TugboatCaptainsPlayground.Services
{

    public class StateBase : ITracksLoading
    {
        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
    }
}
