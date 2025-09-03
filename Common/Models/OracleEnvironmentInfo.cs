namespace Common.Models
{
    public class OracleEnvironmentInfo
    {
        public string Name { get; set; }
        public bool IsConnected { get; set; }
        public bool IsProduction { get; set; }
        public string ConnectionError { get; set; }
    }
}