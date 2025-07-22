using System;
using System.Collections.Generic;

namespace Common.Models
{
    public class TempoWorklog
    {
        public string Id { get; set; }
        public string Self { get; set; }
        public TempoAuthor Author { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime Started { get; set; }
        public int TimeSpentSeconds { get; set; }
        public string OriginTaskId { get; set; }
        public string JiraWorklogId { get; set; }
        public string Issue { get; set; }
        public List<TempoAttribute> Attributes { get; set; } = new List<TempoAttribute>();
    }

    public class TempoAuthor
    {
        public string Self { get; set; }
        public string AccountId { get; set; }
        public string DisplayName { get; set; }
    }

    public class TempoAttribute
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class CreateWorklogRequest
    {
        public string IssueKey { get; set; }
        public int TimeSpentSeconds { get; set; }
        public DateTime Started { get; set; }
        public string Comment { get; set; }
        public List<TempoAttribute> Attributes { get; set; } = new List<TempoAttribute>();
    }

    public class TempoWorklogResponse
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