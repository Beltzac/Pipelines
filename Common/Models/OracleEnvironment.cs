namespace Common.Models
{
    public class OracleEnvironment
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
        public string Schema { get; set; }
        public bool IsProduction { get; set; }
    }
}
