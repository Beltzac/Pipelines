namespace Common.Models
{
    public class ConsulEnvironment
    {
        public string Name { get; set; }
        public string ConsulUrl { get; set; }
        public string ConsulFolder { get; set; }
        public string ConsulToken { get; set; }
        public bool IsProduction { get; set; }
    }
}
