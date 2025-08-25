using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Models;

namespace Common.Services.Interfaces
{
    public interface IOracleOpsService
    {
        Task<Dictionary<DateTime, VesselPlan>> FetchVesselPlansWithNamesAsync(DateTime startDate, DateTime endDate);
        Task<Dictionary<DateTime, RailPlan>> FetchRailPlansWithNamesAsync(DateTime startDate, DateTime endDate);
        Task<int> GetCurrentYardTeuAsync();
        Task<Dictionary<DateTime, InOut>> FetchGateTrucksAsync(DateTime startDate, DateTime endDate);
        Task<Dictionary<DateTime, int>> FetchYardMovesAsync(DateTime startDate, DateTime endDate);
        Task<LoadUnloadRate> GetVesselLoadUnloadRatesAsync();
        Task<LoadUnloadRate> GetTrainLoadUnloadRatesAsync();
    }

    public class LoadUnloadRate
    {
        public string Name { get; set; } = string.Empty;
        public double LoadRateTeusPerHour { get; set; }
        public double UnloadRateTeusPerHour { get; set; }
        public int TotalLoadTeus { get; set; }
        public int TotalUnloadTeus { get; set; }
        public double TotalDurationHours { get; set; }
    }

    public class InOut
    {
        public int In { get; set; }
        public int Out { get; set; }
    }
}