using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common.Repositories.TCP.Interfaces
{
    public interface IMongoRepository
    {
        Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string connectionString, string databaseName, string collectionName, string query, CancellationToken cancellationToken);
    }
}