using Common.Models;
using SmartComponents.LocalEmbeddings;

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

    public Pipeline Pipeline { get; set; }

    public string Path => $"{Project}/{Name}";

    public EmbeddingF32? Embedding { get; set; }

    public string ProjectType { get; set; }
}
