using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Models;

namespace Common.Services.Interfaces
{
    public interface IWorklogService
    {
        Task<List<WorklogCreationResult>> CreateWorklogsFromCommitsAsync(List<Commit> commits, int defaultTimeSpentMinutes = 60);
        Task<bool> ValidateTempoConfigurationAsync();
        Task<List<TempoWorklog>> GetExistingWorklogsForDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<List<TempoWorklog>> GetWorklogsByUserAsync(string accountId, DateTime? from = null, DateTime? to = null);
        Task<WorklogCreationResult> CreateWorklogFromCommitAsync(Commit commit, int timeSpentMinutes = 60);
        Task<WorklogCreationResult> CreateGMWorklogFromCommitAsync(Commit commit);
        Task<WorklogCreationResult> CreateWorklogFromPullRequestAsync(PullRequest pullRequest, string jiraCardID, int timeSpentMinutes = 60);
        Task<WorklogCreationResult> CreateGMPullRequestWorklogAsync(PullRequest pullRequest, string jiraCardID);
        Task DeleteWorklogAsync(string worklogId);
        Task<List<TempoWorklog>> GetWorklogsForCommitsAsync(List<Commit> commits, string accountId = null, DateTime? endDate = null);
        Task<WorklogCreationResult> CreateDailyWorklogForCommitsAsync(List<Commit> commits, string accountId);
    }
}
