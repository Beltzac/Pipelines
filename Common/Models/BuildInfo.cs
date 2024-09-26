public class Commit
{
    public string Id { get; set; }
    public string Message { get; set; }
    public string Url { get; set; }
    public string AuthorName { get; set; }
    public string AuthorEmail { get; set; }
}

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
    public Guid Id { get; set; }
    public string Project { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public string CloneUrl { get; set; }
    public bool MasterClonned { get; set; }

    public Pipeline Pipeline { get; set; }

    public string Path => $"{Project}/{Name}";
}