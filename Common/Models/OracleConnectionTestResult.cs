namespace Common.Models
{
    public class OracleConnectionTestResult
    {
        public bool IsConnected { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ConnectionDetails { get; set; }
    }
}