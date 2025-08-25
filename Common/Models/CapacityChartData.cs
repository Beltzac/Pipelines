using System;
using System.Collections.Generic;

namespace Common.Models
{
    public class CapacityChartData
    {
        public string Timestamp { get; set; } = string.Empty;
        public int GateTrucksIn { get; set; }
        public int GateTrucksOut { get; set; }
        public int YardMoves { get; set; }
    }
}