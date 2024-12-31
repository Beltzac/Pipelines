using Common.Models;

namespace Front2.Services
{
    public class ConsultaSggState
    {
        public string SelectedEnvironment { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public string Placa { get; set; }
        public string RequestId { get; set; }
        public string ContainerNumbers { get; set; }
        public long? IdAgendamento { get; set; }
        public string MoveType { get; set; }
        public string Status { get; set; }
        public double? MinDelay { get; set; }
        public bool IsLoading { get; set; }
        public List<LtdbLtvcRecord> Results { get; set; } = new();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public LtdbLtvcRecord SelectedItem { get; set; }
        public string FormattedLtdbXml { get; set; }
        public string FormattedLtvcXml { get; set; }
        public List<(DateTime Timestamp, double AvgDelaySeconds, double MaxDelaySeconds, int RequestCount)> DelayData { get; set; } = new();
    }
}
