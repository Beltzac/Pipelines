namespace Common.Models
{
    public class DelayMetric
    {
        public string Timestamp { get; set; }
        public double AvgDelaySeconds { get; set; }
        public double MaxDelaySeconds { get; set; }
        public int RequestCount { get; set; }
    }
}
