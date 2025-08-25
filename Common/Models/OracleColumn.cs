using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Models
{
    public class OracleColumn
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public int? DataLength { get; set; }
        public int? DataPrecision { get; set; }
        public int? DataScale { get; set; }
        public string Nullable { get; set; }
        public string? Comments { get; set; }
    }
}