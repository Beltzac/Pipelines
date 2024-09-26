namespace Common.Models
{
    public class CommitData
    {
        public string ProjectName { get; set; }
        public string RepoName { get; set; }
        public string BranchName { get; set; }
        public DateTime CommitDate { get; set; }
        public string CommitMessage { get; set; }
        public string JiraCardID { get; set; }
    }
}

