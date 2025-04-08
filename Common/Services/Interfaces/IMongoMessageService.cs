using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Models;

namespace Common.Services.Interfaces
{
    public interface IMongoMessageService
    {
        Task<Dictionary<string, MongoMessage>> GetMessagesAsync(string connectionString);
        Task<string> GenerateInsertStatementAsync(string environment, MongoMessage message);
        MongoMessageDiffResult GetMessageDiff(string id, MongoMessage source, MongoMessage target);
    }
}