namespace Common.Models
{
    public class OracleColumn
    {
        public string COLUMN_NAME { get; set; }
        public string DATA_TYPE { get; set; }
        public int? DATA_LENGTH { get; set; }
        public int? DATA_PRECISION { get; set; }
        public int? DATA_SCALE { get; set; }
        public string NULLABLE { get; set; }
    }
}