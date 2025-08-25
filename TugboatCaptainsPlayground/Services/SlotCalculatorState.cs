using Common.Models;
using Common.Services.Interfaces;
using static Common.Services.OracleOpsService;

namespace TugboatCaptainsPlayground.Services
{
    public class SlotCalculatorState : ITracksLoading
    {
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-7);
        public DateTime EndDate { get; set; } = DateTime.Today;
        public int InitialYardTeu { get; set; } = 500;
        public int VesselPlanCount { get; set; } = 1;
        public int RailPlanCount { get; set; } = 1;
        public int OpsCaps { get; set; } = 1;
        public int YardBandCount { get; set; } = 1;
        public List<HourWindow> HourWindows { get; set; } = new List<HourWindow>();
        public List<SlotChartData> ChartData { get; set; } = new List<SlotChartData>();
        public List<CapacityChartData> CapacityData { get; set; } = new List<CapacityChartData>();
        public bool IsLoading { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
        public double ReserveRho { get; set; } = 0.1;
        public double AvgTeuPerTruck { get; set; } = 2.5;
        public int MinYardTeu { get; set; } = 0;
        public int TargetYardTeu { get; set; } = 1000;
        public int MaxYardTeu { get; set; } = 2000;

        public int GateTrucksPerHour { get; set; } = 100;
        public int YardMovesPerHour { get; set; } = 300;
        public double EasingStrength { get; set; } = 0.1;

        // Cached data
        public Dictionary<DateTime, InOut> GateTrucks { get; set; } = new();
        public Dictionary<DateTime, int> YardMoves { get; set; } = new();

        public DateTime? CachedStartDate { get; set; }
        public DateTime? CachedEndDate { get; set; }
    }
}