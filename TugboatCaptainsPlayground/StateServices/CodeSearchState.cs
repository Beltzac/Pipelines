using Common.Services.Interfaces;

namespace TugboatCaptainsPlayground.Services
{
    public class CodeSearchState : ITracksLoading
    {
        public List<Repository> Repositories { get; set; } = new();
        public string Filter { get; set; } = string.Empty;
        public string SearchQuery { get; set; } = string.Empty;
        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; }
        public string SelectedRepositoryId { get; set; }
    }
}