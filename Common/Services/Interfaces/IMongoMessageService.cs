using Common.Models;

namespace Common.Services.Interfaces
{
    public interface IMongoMessageService
    {
        Task<Dictionary<string, MongoMessage>> GetMessagesAsync(string connectionString);
        Task<PaginatedResult<MongoMessage>> GetMessagesPaginatedAsync(string connectionString, int pageNumber, int pageSize);
        Task<string> GenerateInsertStatementAsync(MongoMessage message);
        Task<MongoMessageDiffResult> GetMessageDiffAsync(string id, MongoMessage source, MongoMessage target);
    }
}