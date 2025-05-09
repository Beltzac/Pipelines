using Common.Models;

namespace Common.Services.Interfaces
{
    public interface IOracleMessageService
    {
        Task<Dictionary<string, MessageDefinition>> GetMessagesAsync(string connectionString);
        Task<MessageDiffResult> GetMessageDiffAsync(string key, MessageDefinition source, MessageDefinition target);
        Task<string> GenerateUpsertStatementAsync(string environment, MessageDefinition message);
    }
}