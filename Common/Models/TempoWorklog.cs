using System;
using System.Collections.Generic;

namespace Common.Models
{
    public class TempoWorklog
    {
        public string Self { get; set; }
        public int TempoWorklogId { get; set; }
        public TempoIssue Issue { get; set; }
        public int TimeSpentSeconds { get; set; }
        public int BillableSeconds { get; set; }
        public DateTime StartDate { get; set; }
        public string StartTime { get; set; }
        public DateTime StartDateTimeUtc { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public TempoAuthor Author { get; set; }
        public TempoAttributes Attributes { get; set; }

        // Backward compatibility properties
        public string Id => TempoWorklogId.ToString();
        public string Comment => Description;
        public string OriginTaskId => Issue?.Id.ToString();
        public string JiraWorklogId => TempoWorklogId.ToString();
        public string IssueKey => Issue?.Key;
    }

    public class TempoIssue
    {
        public string Self { get; set; }
        public int Id { get; set; }
        public string Key { get; set; }
    }

    public class TempoAuthor
    {
        public string Self { get; set; }
        public string AccountId { get; set; }
        public string DisplayName { get; set; }
    }

    public class TempoAttributes
    {
        public string Self { get; set; }
        public List<TempoAttribute> Values { get; set; } = new List<TempoAttribute>();
    }

    public class TempoAttribute
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class TempoWorklogResponse
    {
        public string Self { get; set; }
        public TempoMetadata Metadata { get; set; }
        public List<TempoWorklog> Results { get; set; } = new List<TempoWorklog>();
    }

    public class TempoMetadata
    {
        public int Count { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
    }

    public class CreateWorklogRequest
    {
        public int IssueId { get; set; }
        public int TimeSpentSeconds { get; set; }
        public string StartDate { get; set; }
        public string StartTime { get; set; }
        public string Description { get; set; }
        public string AuthorAccountId { get; set; }
        public List<TempoAttribute> Attributes { get; set; } = new List<TempoAttribute>();
    }

    public class TempoWorklogCreationResponse
    {
        public string Id { get; set; }
        public string Self { get; set; }
    }

    public class TempoConfiguration
    {
        public string ApiToken { get; set; }
        public string BaseUrl { get; set; } = "https://api.tempo.io/4";
        public string AccountId { get; set; }
        public string JiraInstanceUrl { get; set; }
    }
}