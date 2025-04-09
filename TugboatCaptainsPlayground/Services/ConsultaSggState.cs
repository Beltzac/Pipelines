using Common.Models;
using Common.Services.Interfaces;

namespace TugboatCaptainsPlayground.Services
{
    public class ConsultaSggState : ITracksLoading, IPaginates
    {
        public string SelectedEnvironment { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public string Placa { get; set; }
        public string RequestId { get; set; }
        public string GenericText { get; set; }
        public long? IdAgendamento { get; set; }
        public string MoveType { get; set; }
        public string Status { get; set; }
        public double? MinDelay { get; set; }
        public List<LtdbLtvcRecord> Results { get; set; } = new();

        public int TotalCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public LtdbLtvcRecord SelectedItem { get; set; }
        public string FormattedLtdbXml { get; set; }
        public string FormattedLtvcXml { get; set; }
        public List<DelayMetric> DelayData { get; set; } = new();

        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
    }
}
