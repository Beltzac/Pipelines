using Common.Repositories.TCP.Interfaces;
using Common.Services.Interfaces;
using Microsoft.EntityFrameworkCore; // Added for DatabaseFacade

namespace Common.Services
{
    public class DatabaseAssertsService
    {
        private readonly IOracleRepository _oracleRepo;
        private readonly IMongoRepository _mongoRepo;
        private readonly IOracleConnectionFactory _connectionFactory;

        public DatabaseAssertsService(IOracleRepository oracleRepo, IMongoRepository mongoRepo, IOracleConnectionFactory connectionFactory)
        {
            _oracleRepo = oracleRepo;
            _mongoRepo = mongoRepo;
            _connectionFactory = connectionFactory;
        }

        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string query, string queryType, string connectionString, string mongoDatabaseName = null, string mongoCollectionName = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<Dictionary<string, object>>();
            }

            List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();

            if (queryType.Equals("SQL", StringComparison.OrdinalIgnoreCase))
            {
                // Execute SQL query using OracleRepository
                // Need to find a way to get dynamic results (List<Dictionary<string, object>>)
                // as GetFromSqlAsync<T> requires a specific type T.
                try
                {
                    using var context = _connectionFactory.CreateContext(connectionString);
                    using var connection = context.Database.GetDbConnection();
                    using var command = connection.CreateCommand();
                    command.CommandText = query;

                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        await connection.OpenAsync(cancellationToken);
                    }

                    using var reader = await command.ExecuteReaderAsync(cancellationToken);

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        results.Add(row);
                    }
                }
                catch (Exception ex)
                {
                    // Handle SQL execution errors
                    results.Add(new Dictionary<string, object> { { "Error", $"SQL Execution Error: {ex.Message}" } });
                }
            }
            else if (queryType.Equals("MongoDB", StringComparison.OrdinalIgnoreCase))
            {
                // Execute MongoDB query using MongoRepository
                if (string.IsNullOrWhiteSpace(mongoDatabaseName) || string.IsNullOrWhiteSpace(mongoCollectionName))
                {
                     results.Add(new Dictionary<string, object> { { "Error", "MongoDB database and collection names are required." } });
                     return results;
                }
                try
                {
                    results = await _mongoRepo.ExecuteQueryAsync(connectionString, mongoDatabaseName, mongoCollectionName, query, cancellationToken);
                }
                 catch (Exception ex)
                {
                    // Handle MongoDB execution errors
                    results.Add(new Dictionary<string, object> { { "Error", $"MongoDB Execution Error: {ex.Message}" } });
                }
            }
            else
            {
                // Handle unknown query type
                 results.Add(new Dictionary<string, object> { { "Error", $"Unknown query type: {queryType}" } });
            }

            // TODO: Apply date filtering if not done in the query

            return results;
        }
    }
}