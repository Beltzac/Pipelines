namespace Common.Models
{
    public class WorklogCreationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public Commit Commit { get; set; }
        public string WorklogId { get; set; }
    }
}