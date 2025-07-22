namespace Common.Models
{
    public class JiraConfiguration
    {
        public string ApiToken { get; set; }
        public string BaseUrl { get; set; } = "https://your-domain.atlassian.net";
        public string Email { get; set; }
    }
}