namespace Common.Models
{
    public class Commit
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public string Url { get; set; }
        public string AuthorName { get; set; }
        public string AuthorEmail { get; set; }
        public string ProjectName { get; set; }
        public string RepoName { get; set; }
        public string BranchName { get; set; }
        public DateTime CommitDate { get; set; }
        public string JiraCardID { get; set; }
    }
}

