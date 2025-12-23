using System;

namespace Common.Models
{
    public class VesselSchedule
    {
        public string VesselName { get; set; } = string.Empty;
        public DateTime? StartWork { get; set; }
        public DateTime? EndWork { get; set; }
    }

    public class RailSchedule
    {
        public string TrainName { get; set; } = string.Empty;
        public DateTime? StartWork { get; set; }
        public DateTime? EndWork { get; set; }
    }
}
