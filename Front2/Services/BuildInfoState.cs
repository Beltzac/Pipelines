namespace Front2.Services
{
    public class BuildInfoState
    {
        public bool IsLoading { get; set; }
        public List<Repository> BuildInfos { get; set; } = new();
        public string Filter { get; set; } = string.Empty;
    }
}
