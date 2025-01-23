using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Models;

namespace Common.Services.Interfaces
{
    public interface IOracleMessageService
    {
        Task<Dictionary<string, MessageDefinition>> GetMessagesAsync(string connectionString);
        Task<MessageDiffResult> GetMessageDiff(string key, MessageDefinition source, MessageDefinition target);
        string GenerateUpsertStatement(string environment, MessageDefinition message);
    }
}