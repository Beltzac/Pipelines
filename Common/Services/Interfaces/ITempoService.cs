using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Models;

namespace Common.Services.Interfaces
{
    public interface ITempoService
    {
        Task<List<TempoWorklog>> GetWorklogsAsync(DateTime? from = null, DateTime? to = null);
        Task<TempoWorklog> CreateWorklogAsync(CreateWorklogRequest request);
        Task<TempoWorklog> UpdateWorklogAsync(string worklogId, CreateWorklogRequest request);
        Task<bool> DeleteWorklogAsync(string worklogId);
        Task<List<TempoWorklog>> GetWorklogsByIssueAsync(string issueId);
        Task<List<TempoWorklog>> GetWorklogsByUserAsync(string accountId, DateTime? from = null, DateTime? to = null);
        Task<bool> TestConnectionAsync();
    }
}