namespace Common.Models
{
    public class LtdbLtvcRecord
    {
        public DateTime? DataLtdb { get; set; }
        public DateTime? DataLtvc { get; set; }
        public string RequestId { get; set; }
        public long? IdAgendamento { get; set; }
        public string MoveType { get; set; }
        public string Placa { get; set; }
        public string Motorista { get; set; }
        public string LtdbXml { get; set; }
        public string LtvcXml { get; set; }
        public double? Delay { get; set; }
        public string Status { get; set; }
        public string MessageText { get; set; }
        public string ContainerNumbers { get; set; }
        public string CodigoBarras { get; set; }
        public int TotalCount { get; set; }
    }
}
