using Common.Repositories.TCP.Interfaces;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Common.Models;
using MongoDB.Driver.Linq;

namespace Common.Repositories.TCP
{
    public class MongoRepository : IMongoRepository
    {
        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string connectionString, SavedQuery query, CancellationToken cancellationToken)
        {
            var client = new MongoClient(connectionString);

            var database = client.GetDatabase(query.Database);
            var collection = database.GetCollection<BsonDocument>(query.Collection);

            BsonDocument filter;
            if (string.IsNullOrWhiteSpace(query.QueryString))
            {
                filter = new BsonDocument(); // No filter if query string is empty
            }
            else
            {
                try
                {
                    filter = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(query.QueryString);
                }
                catch (Exception ex)
                {
                    throw new FormatException($"Invalid MongoDB query string format: {ex.Message}", ex);
                }
            }

            var result = await (await collection.FindAsync(filter, cancellationToken: cancellationToken)).ToListAsync(cancellationToken);

            // Convert the result BsonDocument into a list of dictionaries
            var resultList = new List<Dictionary<string, object>>();

            foreach (var doc in result)
            {
                var resultDict = new Dictionary<string, object>();
                foreach (var element in doc.Elements)
                {
                    resultDict[element.Name] = ConvertBsonValue(element.Value);
                }
                resultList.Add(resultDict);
            }

            return resultList;
        }

        private object ConvertBsonValue(BsonValue value)
        {
            if (value.IsBsonDateTime)
                return value.ToUniversalTime();
            if (value.IsBsonNull)
                return null;
            if (value.IsBsonDocument)
            {
                var dict = value.AsBsonDocument.Elements.ToDictionary(e => e.Name, e => ConvertBsonValue(e.Value));
                return "{ " + string.Join(", ", dict.Select(kv => $"{kv.Key}: {kv.Value}")) + " }";
            }
            if (value.IsBsonArray)
            {
                var list = value.AsBsonArray.Select(ConvertBsonValue).ToList();
                return "[ " + string.Join(", ", list.Select(item => item?.ToString())) + " ]";
            }
            if (value.IsObjectId)
                return value.AsObjectId.ToString();
            if (value.IsString)
                return value.AsString;
            if (value.IsInt32)
                return value.AsInt32;
            if (value.IsInt64)
                return value.AsInt64;
            if (value.IsDouble)
                return value.AsDouble;
            if (value.IsBoolean)
                return value.AsBoolean;

            // Default fallback
            return value.ToString();
        }
    }
}
