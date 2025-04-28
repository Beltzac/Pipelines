namespace Common.Models
{
    public enum AssertStatus
    {
        NotRun,
        OK,
        Problems,
        Error
    }

    public class SavedQuery
    {
        public string Name { get; set; }
        public string QueryString { get; set; }
        public string QueryType { get; set; } // "SQL" or "MongoDB"
        public string Description { get; set; }
        public AssertStatus LastRunStatus { get; set; }
        public List<Dictionary<string, object>> LastRunResults { get; set; }
    }
}