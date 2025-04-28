using Common.Repositories.TCP.Interfaces;
using Common.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common.Services
{
    public class DatabaseAssertsService
    {
        private readonly IOracleRepository _oracleRepo;
        private readonly IMongoRepository _mongoRepo;

        public DatabaseAssertsService(IOracleRepository oracleRepo, IMongoRepository mongoRepo)
        {
            _oracleRepo = oracleRepo;
            _mongoRepo = mongoRepo;
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
                // For now, we'll use object and try to convert. This might not be robust.
                try
                {
                    // Using FormattableStringFactory to create FormattableString from raw string
                    var sqlFormattable = System.Runtime.CompilerServices.FormattableStringFactory.Create(query);
                    var sqlResults = await _oracleRepo.GetFromSqlAsync<object>(connectionString, sqlFormattable, cancellationToken);

                    // Attempt to convert object results to List<Dictionary<string, object>>
                    // This conversion might need refinement based on how EF Core returns object results
                    foreach (var item in sqlResults)
                    {
                        // Simple conversion - might need more complex logic based on actual result structure
                        var dictionary = new Dictionary<string, object>();
                        var properties = item.GetType().GetProperties();
                        foreach (var prop in properties)
                        {
                            dictionary[prop.Name] = prop.GetValue(item);
                        }
                        results.Add(dictionary);
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