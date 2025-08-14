using System;

namespace Common.Models
{
    public class SggQueryFilter
    {
        public string Environment { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public string GenericText { get; set; }
        public string Placa { get; set; }
        public string Motorista { get; set; }
        public string MoveType { get; set; }
        public long? IdAgendamento { get; set; }
        public string Status { get; set; }
        public double? MinDelay { get; set; }
        public string CodigoBarras { get; set; }
        public string RequestId { get; set; }
        public int PageSize { get; set; } = 10;
        public int PageNumber { get; set; } = 1;
    }
}