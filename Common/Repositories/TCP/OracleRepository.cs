using Common.Repositories.TCP.Interfaces;
using Common.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace Common.Repositories.TCP
{
    public class OracleRepository : IOracleRepository
    {
        private readonly IOracleConnectionFactory _connectionFactory;

        public OracleRepository(IOracleConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<List<T>> GetFromSqlAsync<T>(string connectionString, FormattableString sql, CancellationToken cancellationToken)
        {
            using var context = _connectionFactory.CreateContext(connectionString);

            return await context.Database
                .SqlQuery<T>(sql)
                .ToListAsync(cancellationToken);
        }

        public async Task<T> GetSingleFromSqlAsync<T>(string connectionString, FormattableString sql, CancellationToken cancellationToken)
        {
            using var context = _connectionFactory.CreateContext(connectionString);

            return await context.Database
                .SqlQuery<T>(sql)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
