using Common.Models;

namespace Common.Services.Interfaces
{
    public interface IMongoMessageService
    {
        Task<Dictionary<string, MongoMessage>> GetMessagesAsync(string connectionString);
        Task<string> GenerateInsertStatementAsync(MongoMessage message);
        Task<MongoMessageDiffResult> GetMessageDiffAsync(string id, MongoMessage source, MongoMessage target);
    }
}