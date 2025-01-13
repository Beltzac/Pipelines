namespace Front2.Services
{
    public interface ITracksLoading
    {
        bool IsLoading { get; set; }
        int? ProgressValue { get; set; }
        string ProgressLabel { get; set; }
    }

    public class LoadingScope : IDisposable
    {
        private readonly ITracksLoading _state;

        public LoadingScope(ITracksLoading state)
        {
            _state = state;
            _state.IsLoading = true;
        }

        public void Dispose()
        {
            _state.IsLoading = false;
        }
    }

    public class StateBase : ITracksLoading
    {
        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
    }
}
