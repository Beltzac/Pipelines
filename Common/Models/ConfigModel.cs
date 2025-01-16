namespace Common.Models
{
    public class ConfigModel
    {
        public bool IsDarkMode { get; set; }
        public string PAT { get; set; }
        public string OrganizationUrl { get; set; }
        public string LocalCloneFolder { get; set; }
        public List<ConsulEnvironment> ConsulEnvironments { get; set; } = new List<ConsulEnvironment>();
        public List<OracleEnvironment> OracleEnvironments { get; set; } = new List<OracleEnvironment>();
        public string RepositoryOwner { get; set; } = "beltzac";
        public string Username { get; set; } = "Beltzac";
        public string RepositoryName { get; set; } = "pipelines";
        public string UserAgent { get; set; } = "TcpDash";
        public string AccessToken { get; set; }
        public List<string> IgnoreRepositoriesRegex { get; set; } = new List<string>();
        public string OracleViewsBackupRepo { get; set; } = "C:\\OracleViewsBackup";
        public string ConsulBackupRepo { get; set; } = "C:\\ConsulBackup";
        public bool EnableOracleBackupJob { get; set; } = true;
        public bool EnableConsulBackupJob { get; set; } = true;
        public string AndroidStudioPath { get; set; } = "C:\\Program Files\\Android\\Android Studio\\bin\\studio64.exe";
    }

    public class OracleEnvironment
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
        public string Schema { get; set; }
    }
}
