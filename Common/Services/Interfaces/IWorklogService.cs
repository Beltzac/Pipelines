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
        Task<WorklogCreationResult> CreateWorklogFromCommitAsync(Commit commit, int timeSpentMinutes = 60);
    }
}