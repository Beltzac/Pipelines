namespace Common.Models
{
    public class EsbServerConfig : IEnvironment
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsProduction { get; set; }
        public string ServiceType => "ESB";
    }
}