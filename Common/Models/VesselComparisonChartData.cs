namespace Common.Models
{
    public class VesselComparisonChartData
    {
        public string Timestamp { get; set; } = string.Empty;
        public int CumulativeRealTeu { get; set; }
        public int CumulativeSimulatedTeu { get; set; }
        public int Difference { get; set; }
        public double RealDischargeRate { get; set; }
        public double RealLoadRate { get; set; }
        public double SimulatedDischargeRate { get; set; }
        public double SimulatedLoadRate { get; set; }
    }
}