namespace Common.Models
{
    public class ConfigModel
    {
        /// <summary>
        /// Minimum time (in seconds) between repository updates.
        /// </summary>
        public int MinUpdateTime { get; set; } = 180; // 3 minutes

        /// <summary>
        /// Maximum time (in seconds) between repository updates.
        /// </summary>
        public int MaxUpdateTime { get; set; } = 14400; // 4 hours
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
        public string VisualStudioPath { get; set; } = "devenv.exe";
        public string VSCodePath { get; set; } = "code-insiders.cmd";
        public string TcpUserName { get; set; }
        public HashSet<Guid> PinnedRepositories { get; set; } = new HashSet<Guid>();
        public List<string> RouteDomains { get; set; } = new List<string>();
        public List<MongoEnvironment> MongoEnvironments { get; set; } = new List<MongoEnvironment>();
        public List<SavedQuery> SavedQueries { get; set; } = new List<SavedQuery>();
    }

    public class OracleEnvironment
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
        public string Schema { get; set; }
    }
    public class MongoEnvironment
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }
    }
}
