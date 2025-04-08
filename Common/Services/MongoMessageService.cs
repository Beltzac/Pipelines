using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Models;
using Common.Services.Interfaces;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TugboatCaptainsPlayground.Services
{
    public class MongoMessageService : IMongoMessageService
    {
        public async Task<Dictionary<string, MongoMessage>> GetMessagesAsync(string connectionString)
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase("Core_Mensagens");
            var collection = database.GetCollection<BsonDocument>("Mensagem");

            var messages = new Dictionary<string, MongoMessage>();
            var cursor = await collection.FindAsync(new BsonDocument());

            await cursor.ForEachAsync(doc =>
            {
                var message = new MongoMessage
                {
                    Id = doc.TryGetValue("_id", out var id) ? id.AsString : null,
                    Path = doc.TryGetValue("Path", out var path) ? path.AsString : null,
                    Key = doc.TryGetValue("Key", out var key) ? key.AsString : null,
                    Idioma = doc.TryGetValue("Idioma", out var idioma) ? idioma.AsString : null,
                    Nivel = doc.TryGetValue("Nivel", out var nivel) ?
                        nivel.BsonType == BsonType.Int32 ? nivel.AsInt32 :
                        nivel.BsonType == BsonType.Double ? (int)nivel.AsDouble :
                        throw new InvalidOperationException("Nivel must be a number") :
                        null,
                    Titulo = doc.TryGetValue("Titulo", out var titulo) ? titulo.AsString : null,
                    Texto = doc.TryGetValue("Texto", out var texto) ? texto.AsString : null,
                    Tags = doc.TryGetValue("Tags", out var tags) ? tags.AsBsonArray.Select(x => x.AsString).ToList() : new List<string>(),
                    RevisaoPendente = doc.TryGetValue("RevisaoPendente", out var revisaoPendente) && !revisaoPendente.IsBsonNull ? revisaoPendente.AsBoolean : false,
                    Inclusao = doc.TryGetValue("Inclusao", out var inclusao) ? ConvertMetadata(inclusao.AsBsonDocument) : new Metadata(),
                    Alteracao = doc.TryGetValue("Alteracao", out var alteracao) ? ConvertMetadata(alteracao.AsBsonDocument) : new Metadata(),
                    UltimaInteracao = doc.TryGetValue("UltimaInteracao", out var ultimaInteracao) ? ConvertMetadata(ultimaInteracao.AsBsonDocument) : new Metadata()
                };

                messages[message.Id] = message;
            });

            return messages;
        }

        public async Task<string> GenerateInsertStatementAsync(string environment, MongoMessage message)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"db.getCollection(\"Mensagem\").insert({{");
            sb.AppendLine($"    _id: \"{message.Id}\",");
            sb.AppendLine($"    Inclusao: {{");
            sb.AppendLine($"        Data: ISODate(\"{message.Inclusao.Data:yyyy-MM-ddTHH:mm:ss.fffZ}\"),");
            sb.AppendLine($"        UserId: \"{message.Inclusao.UserId}\",");
            sb.AppendLine($"        UserName: \"{message.Inclusao.UserName}\",");
            sb.AppendLine($"        UserLogin: {(message.Inclusao.UserLogin != null ? $"\"{message.Inclusao.UserLogin}\"" : "null")},");
            sb.AppendLine($"        ProcurationId: {(message.Inclusao.ProcurationId != null ? $"\"{message.Inclusao.ProcurationId}\"" : "null")},");
            sb.AppendLine($"        CompanyId: {(message.Inclusao.CompanyId != null ? $"\"{message.Inclusao.CompanyId}\"" : "null")},");
            sb.AppendLine($"        CompanyName: {(message.Inclusao.CompanyName != null ? $"\"{message.Inclusao.CompanyName}\"" : "null")},");
            sb.AppendLine($"        AggregationId: \"{message.Inclusao.AggregationId}\"");
            sb.AppendLine($"    }},");
            sb.AppendLine($"    Alteracao: {{");
            sb.AppendLine($"        Data: ISODate(\"{message.Alteracao.Data:yyyy-MM-ddTHH:mm:ss.fffZ}\"),");
            sb.AppendLine($"        UserId: \"{message.Alteracao.UserId}\",");
            sb.AppendLine($"        UserName: \"{message.Alteracao.UserName}\",");
            sb.AppendLine($"        UserLogin: {(message.Alteracao.UserLogin != null ? $"\"{message.Alteracao.UserLogin}\"" : "null")},");
            sb.AppendLine($"        ProcurationId: {(message.Alteracao.ProcurationId != null ? $"\"{message.Alteracao.ProcurationId}\"" : "null")},");
            sb.AppendLine($"        CompanyId: {(message.Alteracao.CompanyId != null ? $"\"{message.Alteracao.CompanyId}\"" : "null")},");
            sb.AppendLine($"        CompanyName: {(message.Alteracao.CompanyName != null ? $"\"{message.Alteracao.CompanyName}\"" : "null")},");
            sb.AppendLine($"        AggregationId: \"{message.Alteracao.AggregationId}\"");
            sb.AppendLine($"    }},");
            sb.AppendLine($"    UltimaInteracao: {{");
            sb.AppendLine($"        Data: ISODate(\"{message.UltimaInteracao.Data:yyyy-MM-ddTHH:mm:ss.fffZ}\"),");
            sb.AppendLine($"        UserId: \"{message.UltimaInteracao.UserId}\",");
            sb.AppendLine($"        UserName: \"{message.UltimaInteracao.UserName}\",");
            sb.AppendLine($"        UserLogin: {(message.UltimaInteracao.UserLogin != null ? $"\"{message.UltimaInteracao.UserLogin}\"" : "null")},");
            sb.AppendLine($"        ProcurationId: {(message.UltimaInteracao.ProcurationId != null ? $"\"{message.UltimaInteracao.ProcurationId}\"" : "null")},");
            sb.AppendLine($"        CompanyId: {(message.UltimaInteracao.CompanyId != null ? $"\"{message.UltimaInteracao.CompanyId}\"" : "null")},");
            sb.AppendLine($"        CompanyName: {(message.UltimaInteracao.CompanyName != null ? $"\"{message.UltimaInteracao.CompanyName}\"" : "null")},");
            sb.AppendLine($"        AggregationId: \"{message.UltimaInteracao.AggregationId}\"");
            sb.AppendLine($"    }},");
            sb.AppendLine($"    Path: \"{message.Path}\",");
            sb.AppendLine($"    Key: \"{message.Key}\",");
            sb.AppendLine($"    Idioma: \"{message.Idioma}\",");
            sb.AppendLine($"    Nivel: NumberInt(\"{message.Nivel}\"),");
            sb.AppendLine($"    Titulo: \"{message.Titulo}\",");
            sb.AppendLine($"    Texto: \"{message.Texto}\",");
            sb.AppendLine($"    Tags: [{(message.Tags.Any() ? string.Join(", ", message.Tags.Select(t => $"\"{t}\"")) : "")}],");
            sb.AppendLine($"    RevisaoPendente: {message.RevisaoPendente.ToString().ToLower()}");
            sb.AppendLine($"}});");

            return sb.ToString();
        }

        public MongoMessageDiffResult GetMessageDiff(string id, MongoMessage source, MongoMessage target)
        {
            var diff = new MongoMessageDiffResult
            {
                Id = id,
                Source = source,
                Target = target,
                Path = source?.Path ?? target?.Path,
                Key = source?.Key ?? target?.Key,
                Idioma = source?.Idioma ?? target?.Idioma
            };

            if (source == null || target == null)
            {
                diff.HasDifferences = source != target;
                diff.FormattedDiff = source == null ? "Source is null" : "Target is null";
                return diff;
            }

            var properties = typeof(MongoMessage).GetProperties();
            foreach (var prop in properties)
            {
                if (prop.Name == "Inclusao" || prop.Name == "Alteracao" || prop.Name == "UltimaInteracao")
                    continue;

                var sourceValue = prop.GetValue(source);
                var targetValue = prop.GetValue(target);

                if (prop.Name == "Tags")
                {
                    var sourceTags = sourceValue as List<string> ?? new List<string>();
                    var targetTags = targetValue as List<string> ?? new List<string>();
                    if (!sourceTags.SequenceEqual(targetTags))
                    {
                        diff.ChangedFields.Add(prop.Name);
                    }
                }
                else if (sourceValue == null && targetValue == null)
                {
                    continue;
                }
                else if (sourceValue == null || targetValue == null || !sourceValue.Equals(targetValue))
                {
                    diff.ChangedFields.Add(prop.Name);
                }
            }

            diff.HasDifferences = diff.ChangedFields.Any();
            diff.FormattedDiff = string.Join(Environment.NewLine, diff.ChangedFields
                .Select(f => $"{f}: '{GetPropertyValue(source, f)}' => '{GetPropertyValue(target, f)}'"));

            return diff;
        }

        private Metadata ConvertMetadata(BsonDocument doc)
        {
            if (doc == null) return new Metadata();

            return new Metadata
            {
                Data = doc.TryGetValue("Data", out var data) && data is BsonDateTime bsonDate ?
                    bsonDate.ToUniversalTime() : DateTime.UtcNow,
                UserId = doc.TryGetValue("UserId", out var userId) && !userId.IsBsonNull ? userId.AsString : string.Empty,
                UserName = doc.TryGetValue("UserName", out var userName) && !userName.IsBsonNull ? userName.AsString : string.Empty,
                UserLogin = doc.TryGetValue("UserLogin", out var userLogin) && !userLogin.IsBsonNull ? userLogin.AsString : null,
                ProcurationId = doc.TryGetValue("ProcurationId", out var procurationId) && !procurationId.IsBsonNull ? procurationId.AsString : null,
                CompanyId = doc.TryGetValue("CompanyId", out var companyId) && !companyId.IsBsonNull ? companyId.AsString : null,
                CompanyName = doc.TryGetValue("CompanyName", out var companyName) && !companyName.IsBsonNull ? companyName.AsString : null,
                AggregationId = doc.TryGetValue("AggregationId", out var aggregationId) && !aggregationId.IsBsonNull ? aggregationId.AsString : string.Empty
            };
        }

        private static string GetPropertyValue(MongoMessage message, string propertyName)
        {
            if (message == null) return "null";
            var prop = typeof(MongoMessage).GetProperty(propertyName);
            if (prop == null) return "[invalid property]";
            var value = prop.GetValue(message);
            return value?.ToString() ?? "null";
        }
    }
}