using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Common.Models;

namespace Common.Services.Interfaces
{
    public interface IOracleOpsService
    {
        Task<Dictionary<DateTime, VesselPlan>> FetchVesselPlansWithNamesAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default);
        Task<Dictionary<DateTime, RailPlan>> FetchRailPlansWithNamesAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default);
        Task<int> GetCurrentYardTeuAsync(string envName, CancellationToken cancellationToken = default);
        Task<Dictionary<DateTime, InOut>> FetchGateTrucksAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default);
        Task<Dictionary<DateTime, int>> FetchYardMovesAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default);
        Task<LoadUnloadRate> GetVesselLoadUnloadRatesAsync(string envName, CancellationToken cancellationToken = default);
        Task<LoadUnloadRate> GetTrainLoadUnloadRatesAsync(string envName, CancellationToken cancellationToken = default);
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