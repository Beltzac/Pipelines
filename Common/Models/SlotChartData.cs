using System;
using System.Collections.Generic;

namespace Common.Models
{
    public class SlotChartData
    {
        public string Timestamp { get; set; } = string.Empty;
        public int TotalSlots { get; set; }
        public int SlotsIn { get; set; }
        public int SlotsOut { get; set; }
        public int YardTEU { get; set; }
        public int MinYardTeu { get; set; }
        public int TargetYardTeu { get; set; }
        public int MaxYardTeu { get; set; }
        public int YardTeuProjection { get; set; }
        public int YardTeuNoGate { get; set; }
        public int YardTeuRealGate { get; set; }
        public int TruckIn { get; set; }
        public int TruckOut { get; set; }
        public int VesselIn { get; set; }
        public int VesselOut { get; set; }
        public int RailIn { get; set; }
        public int RailOut { get; set; }

        public int SimVesselDiff { get; set; }
        public int SimRailDiff { get; set; }
        public int SimTruckDiff { get; set; }
        public int RealVesselDiff { get; set; }
        public int RealRailDiff { get; set; }
        public int RealTruckDiff { get; set; }
    }
}