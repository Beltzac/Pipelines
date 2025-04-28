using Common.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common.Repositories.TCP.Interfaces
{
    public interface IMongoRepository
    {
        Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string connectionString, SavedQuery query, CancellationToken cancellationToken);
    }
}