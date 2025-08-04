using Common.Models;
using SmartComponents.LocalEmbeddings;
using System.Text.RegularExpressions;

public class Build
{
    public int Id { get; set; }
    public string Status { get; set; }
    public string Result { get; set; }
    public string Url { get; set; }

    public string ErrorLogs { get; set; }

    public DateTime? Queued { get; set; }
    public DateTime Changed { get; set; }

    public Commit Commit { get; set; }
}

public class Pipeline
{
    public int Id { get; set; }
    public Build Last { get; set; }
    public Build LastSuccessful { get; set; }
}

public class Repository
{
    public const string PROJECT_TYPE_ANDROID = "ANDROID";
    public const string PROJECT_TYPE_VISUAL_STUDIO = "VISUAL_STUDIO";
    public const string PROJECT_TYPE_VISUAL_STUDIO_CODE = "VISUAL_STUDIO_CODE";
    public const string PROJECT_TYPE_FOLDER = "FOLDER";

    public Guid Id { get; set; }
    public string Project { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public string CloneUrl { get; set; }
    public bool MasterClonned { get; set; }
    public string CurrentBranch { get; set; }

    public Pipeline Pipeline { get; set; }

    public string Path => $"{Project}/{Name}";

    public EmbeddingF32? Embedding { get; set; }

    public string ProjectType { get; set; }

    public List<string> ProjectNames { get; set; } = new List<string>();

    public List<PullRequest> ActivePullRequests { get; set; } = new List<PullRequest>();
}

public class PullRequest
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Url { get; set; }
    public string SourceBranch { get; set; }
    public string TargetBranch { get; set; }
    public int ChangedFileCount { get; set; }
    public DateTime CreatedDate { get; set; }
    public List<string> JiraCardIDs { get; set; } = new List<string>();
    public Guid ProjectId { get; set; }
    public Guid RepositoryId { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public DateTime? LastPipelineRunDate { get; set; }

    public static List<string> ExtractJiraCardIDs(params string[] texts)
    {
        var allJiraCardIDs = new List<string>();
        foreach (var text in texts)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var matches = Regex.Matches(text, @"([A-Z]+-\d+)");
                allJiraCardIDs.AddRange(matches.Cast<Match>().Select(m => m.Groups[1].Value));
            }
        }
        return allJiraCardIDs.Distinct().ToList(); // Ensure unique IDs
    }
}
