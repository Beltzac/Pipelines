using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Repositories.TCP.Interfaces
{
    public interface IOracleRepository
    {
        Task<List<T>> GetFromSqlAsync<T>(string connectionString, FormattableString sql, CancellationToken cancellationToken);
        Task<T> GetSingleFromSqlAsync<T>(string connectionString, FormattableString sql, CancellationToken cancellationToken);
    }
}
