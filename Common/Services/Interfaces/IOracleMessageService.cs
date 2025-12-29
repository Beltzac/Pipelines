using Common.Models;

namespace Common.Services.Interfaces
{
    public interface IOracleMessageService
    {
        Task<Dictionary<string, MessageDefinition>> GetMessagesAsync(string connectionString);
        Task<PaginatedResult<MessageDefinition>> GetMessagesPaginatedAsync(string connectionString, int pageNumber, int pageSize);
        Task<MessageDiffResult> GetMessageDiffAsync(string key, MessageDefinition source, MessageDefinition target);
        Task<string> GenerateUpsertStatementAsync(string environment, MessageDefinition message);
    }
}