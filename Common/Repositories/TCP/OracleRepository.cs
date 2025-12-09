using Common.Repositories.TCP.Interfaces;
using Common.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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

        public async Task<List<Dictionary<string, object>>> GetFromSqlDynamicAsync(string connectionString, FormattableString sql, CancellationToken cancellationToken)
        {
            using var context = _connectionFactory.CreateContext(connectionString);

            // Use raw SQL query and map to dictionary
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            var result = new List<Dictionary<string, object>>();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql.Format;

                // Add parameters if any
                for (int i = 0; i < sql.ArgumentCount; i++)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = $"p{i}";
                    parameter.Value = sql.GetArgument(i);
                    command.Parameters.Add(parameter);
                }

                using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            object value;
                            Type fieldType = reader.GetFieldType(i);

                            if (fieldType == typeof(decimal) || fieldType == typeof(double) || fieldType == typeof(float) ||
                                fieldType == typeof(int) || fieldType == typeof(long) || fieldType == typeof(short))
                            {
                                value = reader.GetDecimal(i);
                            }
                            else
                            {
                                value = reader.GetValue(i);
                            }
                            row[reader.GetName(i)] = value == DBNull.Value ? null : value;
                        }
                        result.Add(row);
                    }
                }
            }

            await connection.CloseAsync();

            return result;
        }

        public async Task<int> ExecuteAsync(string connectionString, FormattableString sql, CancellationToken cancellationToken)
        {
            using var context = _connectionFactory.CreateContext(connectionString);

            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql.Format;

                // Add parameters if any
                for (int i = 0; i < sql.ArgumentCount; i++)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = $"p{i}";
                    parameter.Value = sql.GetArgument(i) ?? DBNull.Value;
                    command.Parameters.Add(parameter);
                }

                return await command.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
    }
}
