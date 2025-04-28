using Common.Repositories.TCP.Interfaces;
using MongoDB.Driver;
using MongoDB.Bson; // Added using directive for BsonDocument
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq; // Added using directive for LINQ

namespace Common.Repositories.TCP
{
    public class MongoRepository : IMongoRepository
    {
        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string connectionString, string databaseName, string collectionName, string query, CancellationToken cancellationToken)
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            // Assuming the query is a JSON filter document for a Find operation
            BsonDocument filterDocument = null;
            if (!string.IsNullOrWhiteSpace(query))
            {
                try
                {
                    filterDocument = BsonDocument.Parse(query);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Invalid MongoDB query format: {ex.Message}", ex);
                }
            }

            var findOptions = new FindOptions<BsonDocument, BsonDocument>
            {
                // Add options if needed, e.g., Sort, Limit, Projection
            };

            using (var cursor = await collection.FindAsync(filterDocument ?? new BsonDocument(), findOptions, cancellationToken))
            {
                var results = new List<Dictionary<string, object>>();
                while (await cursor.MoveNextAsync(cancellationToken))
                {
                    foreach (var document in cursor.Current)
                    {
                        // Convert BsonDocument to Dictionary<string, object>
                        var dictionary = new Dictionary<string, object>();
                        foreach (var element in document)
                        {
                            // Attempt to convert BsonValue to .NET object
                            object dotNetValue;
                            if (element.Value.IsBsonDateTime)
                            {
                                dotNetValue = element.Value.ToUniversalTime();
                            }
                            else if (element.Value.IsBsonNull)
                            {
                                dotNetValue = null;
                            }
                            else if (element.Value.IsBsonDocument)
                            {
                                // Recursively convert nested documents if needed, or store as BsonDocument
                                dotNetValue = element.Value.ToBsonDocument().ToDictionary(); // Simple conversion to Dictionary
                            }
                            else if (element.Value.IsBsonArray)
                            {
                                // Convert BsonArray to List<object>
                                // Let's try a simpler conversion for array elements for now
                                dotNetValue = element.Value.AsBsonArray.Select(v => v.ToString()).ToList();
                            }
                            else
                            {
                                // Use ToString() for other basic types
                                dotNetValue = element.Value.ToString();
                            }
                            dictionary[element.Name] = dotNetValue;
                        }
                        results.Add(dictionary);
                    }
                }
                return results;
            }
        }
    }
}