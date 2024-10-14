namespace Common.Models
{
    public class ConfigModel
    {
        public string PAT { get; set; }
        public string OrganizationUrl { get; set; }
        public string LocalCloneFolder { get; set; }
        public List<ConsulEnvironment> ConsulEnvironments { get; set; } = new List<ConsulEnvironment>();
        public string RepositoryOwner { get; set; } = "beltzac";
        public string Username { get; set; } = "Beltzac";
        public string RepositoryName { get; set; } = "pipelines";
        public string UserAgent { get; set; } = "TcpDash";
        public string AccessToken { get; set; }
    }
    public class ConsulEnvironment
    {
        public string Name { get; set; }
        public string ConsulUrl { get; set; }
        public string ConsulFolder { get; set; }
        public string ConsulToken { get; set; }
    }
}
