namespace Common.Models
{
    public class OracleEnvironment : IEnvironment
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
        public string Schema { get; set; }
        public bool IsProduction { get; set; }
        public string ServiceType => "Oracle";
    }
}
