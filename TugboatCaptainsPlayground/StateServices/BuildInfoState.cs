using Common.Services.Interfaces;

namespace TugboatCaptainsPlayground.Services
{
    public class BuildInfoState : ITracksLoading
    {
        public List<Repository> BuildInfos { get; set; } = new();
        public string Filter { get; set; } = string.Empty;
        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; }
    }
}
