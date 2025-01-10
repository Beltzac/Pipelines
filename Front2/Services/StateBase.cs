namespace Front2.Services
{
    public class StateBase
    {
        public int ProgressValue { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
        public bool IsIndeterminate { get; set; }
        public bool IsLoading { get; set; }
    }
}
