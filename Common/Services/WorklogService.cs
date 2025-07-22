using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Models;
using Common.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Common.Services
{
    public class WorklogService : IWorklogService
    {
        private readonly ITempoService _tempoService;
        private readonly IJiraService _jiraService;
        private readonly IConfigurationService _configService;
        private readonly ILogger<WorklogService> _logger;

        public WorklogService(
            ITempoService tempoService,
            IJiraService jiraService,
            IConfigurationService configService,
            ILogger<WorklogService> logger)
        {
            _tempoService = tempoService;
            _jiraService = jiraService;
            _configService = configService;
            _logger = logger;
        }

        public async Task<bool> ValidateTempoConfigurationAsync()
        {
            try
            {
                var config = _configService.GetConfig();
                return config.TempoConfig != null &&
                       !string.IsNullOrEmpty(config.TempoConfig.ApiToken) &&
                       !string.IsNullOrEmpty(config.TempoConfig.AccountId) &&
                       await _tempoService.TestConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate Tempo configuration");
                return false;
            }
        }

        public async Task<List<WorklogCreationResult>> CreateWorklogsFromCommitsAsync(List<Commit> commits, int defaultTimeSpentMinutes = 60)
        {
            var results = new List<WorklogCreationResult>();

            if (!await ValidateTempoConfigurationAsync())
            {
                results.Add(new WorklogCreationResult
                {
                    Success = false,
                    Message = "Tempo configuration is invalid",
                    Error = "Please configure Tempo API settings"
                });
                return results;
            }

            foreach (var commit in commits)
            {
                var result = await CreateWorklogFromCommitAsync(commit, defaultTimeSpentMinutes);
                results.Add(result);
            }

            return results;
        }

        public async Task<WorklogCreationResult> CreateWorklogFromCommitAsync(Commit commit, int timeSpentMinutes = 60)
        {
            try
            {
                if (string.IsNullOrEmpty(commit.JiraCardID))
                {
                    return new WorklogCreationResult
                    {
                        Success = false,
                        Commit = commit,
                        Message = "No JIRA card ID found in commit message",
                        Error = "Commit does not contain a valid JIRA card reference"
                    };
                }

                // Get the issue ID from Jira using the issue key
                var issueId = await _jiraService.GetIssueIdByKeyAsync(commit.JiraCardID);
                if (string.IsNullOrEmpty(issueId))
                {
                    return new WorklogCreationResult
                    {
                        Success = false,
                        Commit = commit,
                        Message = "Could not resolve JIRA issue key to ID",
                        Error = $"Failed to find issue with key: {commit.JiraCardID}"
                    };
                }

                var existingWorklogs = await _tempoService.GetWorklogsByIssueAsync(issueId);
                var existingWorklogForCommit = existingWorklogs.FirstOrDefault(w =>
                    w.Comment != null && w.Comment.Contains(commit.Id));

                if (existingWorklogForCommit != null)
                {
                    return new WorklogCreationResult
                    {
                        Success = false,
                        Commit = commit,
                        Message = "Worklog already exists for this commit",
                        WorklogId = existingWorklogForCommit.Id,
                        Error = "A worklog for this commit already exists"
                    };
                }

                var request = new CreateWorklogRequest
                {
                    IssueKey = issueId,
                    TimeSpentSeconds = timeSpentMinutes * 60,
                    Started = commit.CommitDate,
                    Comment = FormatCommitMessageForWorklog(commit),
                    Attributes = new List<TempoAttribute>
                    {
                        new TempoAttribute
                        {
                            Key = "Repository",
                            Value = $"{commit.ProjectName}/{commit.RepoName}"
                        },
                        new TempoAttribute
                        {
                            Key = "Branch",
                            Value = commit.BranchName
                        },
                        new TempoAttribute
                        {
                            Key = "CommitId",
                            Value = commit.Id
                        }
                    }
                };

                var worklog = await _tempoService.CreateWorklogAsync(request);

                return new WorklogCreationResult
                {
                    Success = true,
                    Commit = commit,
                    Message = "Worklog created successfully",
                    WorklogId = worklog.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create worklog for commit {CommitId}", commit.Id);
                return new WorklogCreationResult
                {
                    Success = false,
                    Commit = commit,
                    Message = "Failed to create worklog",
                    Error = ex.Message
                };
            }
        }

        public async Task<List<TempoWorklog>> GetExistingWorklogsForDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                return await _tempoService.GetWorklogsAsync(startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get existing worklogs for date range");
                return new List<TempoWorklog>();
            }
        }

        private string FormatCommitMessageForWorklog(Commit commit)
        {
            var cleanMessage = commit.CommitMessage.Split('\n')[0]; // Take only first line
            return $"{cleanMessage} (Commit: {commit.Id.Substring(0, 8)} - {commit.RepoName}/{commit.BranchName})";
        }
    }
}