namespace TugboatCaptainsPlayground.Services
{
    public class BuildInfoState : StateBase
    {
        public List<Repository> BuildInfos { get; set; } = new();
        public string Filter { get; set; } = string.Empty;
    }
}
