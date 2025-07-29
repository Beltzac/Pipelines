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
            return await CreateWorklogInternalAsync(commit, timeSpentMinutes, FormatCommitMessageForWorklog(commit));
        }

        public async Task<WorklogCreationResult> CreateGMWorklogFromCommitAsync(Commit commit)
        {
            return await CreateWorklogInternalAsync(commit, 120, "Acompanhamento de GM");
        }

        private async Task<WorklogCreationResult> CreateWorklogInternalAsync(Commit commit, int timeSpentMinutes, string description)
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

                var config = _configService.GetConfig();
                var authorAccountId = config.TempoConfig?.AccountId;

                if (string.IsNullOrEmpty(authorAccountId))
                {
                    throw new InvalidOperationException("Tempo AccountId is required but not configured");
                }

                // Convert issue ID string to integer
                if (!int.TryParse(issueId, out var issueIdInt))
                {
                    throw new InvalidOperationException($"Invalid issue ID format: {issueId}");
                }

                var request = new CreateWorklogRequest
                {
                    IssueId = issueIdInt,
                    TimeSpentSeconds = timeSpentMinutes * 60,
                    StartDate = commit.CommitDate.Date.ToString("yyyy-MM-dd"),
                    StartTime = commit.CommitDate.ToString("HH:mm:ss"),
                    Description = description,
                    AuthorAccountId = authorAccountId,
                    Attributes = new List<TempoAttribute>()
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
            catch (System.Net.Http.HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to communicate with API for commit {CommitId}", commit.Id);
                return new WorklogCreationResult
                {
                    Success = false,
                    Commit = commit,
                    Message = "Failed to communicate with the API. Please check the connection and try again.",
                    Error = ex.Message
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

        public async Task<List<TempoWorklog>> GetWorklogsByUserAsync(string accountId, DateTime? from = null, DateTime? to = null)
        {
            try
            {
                return await _tempoService.GetWorklogsByUserAsync(accountId, from, to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get worklogs for user {AccountId}", accountId);
                return new List<TempoWorklog>();
            }
        }

        public async Task DeleteWorklogAsync(string worklogId)
        {
            var config = _configService.GetConfig();
            var accountId = config.TempoConfig?.AccountId;

            if (string.IsNullOrEmpty(accountId))
            {
                throw new InvalidOperationException("Account ID not configured");
            }

            try
            {
                await _tempoService.DeleteWorklogAsync(worklogId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete worklog: {ex.Message}", ex);
            }
        }

        public async Task<List<TempoWorklog>> GetWorklogsForCommitsAsync(List<Commit> commits, string accountId = null)
        {
            if (commits == null || !commits.Any())
            {
                return new List<TempoWorklog>();
            }

            try
            {
                var startDate = commits.Min(c => c.CommitDate.Date);
                var endDate = commits.Max(c => c.CommitDate.Date);

                // Use accountId if provided, otherwise use configured account
                if (string.IsNullOrEmpty(accountId))
                {
                    var config = _configService.GetConfig();
                    accountId = config.TempoConfig?.AccountId;
                }

                List<TempoWorklog> worklogs;
                if (!string.IsNullOrEmpty(accountId))
                {
                    // Use user-specific endpoint if account ID is available
                    worklogs = await GetWorklogsByUserAsync(accountId, startDate, endDate);
                }
                else
                {
                    // Fallback to date range endpoint
                    worklogs = await GetExistingWorklogsForDateRangeAsync(startDate, endDate);
                }

                return worklogs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get worklogs for commits");
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
