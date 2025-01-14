using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Common.Models;
using Oracle.ManagedDataAccess.Client;
using SQL.Formatter;
using SQL.Formatter.Language;

namespace Common.Services
{
    public class OracleMessageService : IOracleMessageService
    {
        public async Task<Dictionary<string, MessageDefinition>> GetMessagesAsync(string connectionString)
        {
            var messages = new Dictionary<string, MessageDefinition>();

            using (var conn = new OracleConnection(connectionString))
            {
                await conn.OpenAsync();

                // Fetch base message data
                var messageQuery = $@"
                    SELECT m.ID_MENSAGEM, m.ID_SISTEMA_MENSAGEM, m.ID_DESTINO_MENSAGEM,
                           m.VERIFICADO, m.MODULO, m.CODIGO, m.PREFIXO, m.ELEMENTO, m.OBSERVACAO
                    FROM TCPCONF.MENSAGEM m
                    WHERE m.EXCLUIDO = 0";

                using (var cmd = new OracleCommand(messageQuery, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var message = new MessageDefinition
                        {
                            IdMensagem = reader.GetInt64(0),
                            IdSistemaMensagem = reader.GetInt64(1),
                            IdDestinoMensagem = reader.GetInt64(2),
                            Verificado = reader.GetInt32(3) == 1,
                            Modulo = reader.GetString(4),
                            Codigo = reader.GetString(5),
                            Prefixo = reader.GetString(6),
                            Elemento = reader.IsDBNull(7) ? null : reader.GetString(7),
                            Observacao = reader.IsDBNull(8) ? null : reader.GetString(8)
                        };

                        message.Key = $"{message.Prefixo}-{message.Codigo}";

                        messages[message.Key] = message;
                    }
                }

                // Fetch language-specific data
                var languageQuery = $@"
                    SELECT mi.ID_MENSAGEM, mi.IDIOMA, mi.TITULO, mi.DESCRICAO, mi.AJUDA
                    FROM TCPCONF.MENSAGEM_IDIOMA mi
                    WHERE mi.EXCLUIDO = 0";

                using (var cmd = new OracleCommand(languageQuery, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var messageId = reader.GetInt64(0);
                        var message = messages.Values.FirstOrDefault(x => x.IdMensagem == messageId);
                        if (message != null)
                        {
                            message.Languages[reader.GetInt32(1)] = new MessageLanguageDefinition
                            {
                                Idioma = reader.GetInt32(1),
                                Titulo = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Descricao = reader.GetString(3),
                                Ajuda = reader.IsDBNull(4) ? null : reader.GetString(4)
                            };
                        }
                    }
                }
            }

            return messages;
        }

        public async Task<MessageDiffResult> GetMessageDiff(string key, MessageDefinition source, MessageDefinition target)
        {
            var diff = new MessageDiffResult
            {
                HasDifferences = false,
                FormattedDiff = string.Empty,
                Source = source,
                Target = target
            };

            if (source == null && target == null)
                return diff;

            var diffs = new List<string>();

            // Compare base message properties
            CompareProperty("Modulo", source?.Modulo, target?.Modulo, diffs);
            CompareProperty("Codigo", source?.Codigo, target?.Codigo, diffs);
            CompareProperty("Prefixo", source?.Prefixo, target?.Prefixo, diffs);
            CompareProperty("Elemento", source?.Elemento, target?.Elemento, diffs);
            CompareProperty("Observacao", source?.Observacao, target?.Observacao, diffs);

            // Compare language-specific content
            var allLanguages = new HashSet<int>(
                (source?.Languages.Keys ?? Enumerable.Empty<int>())
                .Union(target?.Languages.Keys ?? Enumerable.Empty<int>())
            );

            foreach (var language in allLanguages)
            {
                var sourceLang = source?.Languages.GetValueOrDefault(language);
                var targetLang = target?.Languages.GetValueOrDefault(language);

                CompareProperty($"Titulo ({language})", sourceLang?.Titulo, targetLang?.Titulo, diffs);
                CompareProperty($"Descricao ({language})", sourceLang?.Descricao, targetLang?.Descricao, diffs);
                CompareProperty($"Ajuda ({language})", sourceLang?.Ajuda, targetLang?.Ajuda, diffs);
            }

            if (diffs.Count > 0)
            {
                diff.HasDifferences = true;
                diff.FormattedDiff = string.Join("\n", diffs);
            }

            return diff;
        }

        private void CompareProperty(string propertyName, string sourceValue, string targetValue, List<string> diffs)
        {
            if (sourceValue != targetValue)
            {
                diffs.Add($"{propertyName}:\n- {sourceValue}\n+ {targetValue}");
            }
        }

        public string GenerateUpsertStatement(MessageDefinition message)
        {
            if (message == null)
                return string.Empty;

            var baseUpsert = $@"MERGE INTO TCPCONF.MENSAGEM m
USING DUAL
ON (m.PREFIXO = '{message.Prefixo}' AND m.CODIGO = '{message.Codigo}')
WHEN MATCHED THEN
    UPDATE SET
        m.MODULO = '{message.Modulo}',
        m.ELEMENTO = {(message.Elemento == null ? "NULL" : $"'{message.Elemento}'")},
        m.OBSERVACAO = {(message.Observacao == null ? "NULL" : $"'{message.Observacao}'")},
        m.VERIFICADO = {(message.Verificado ? "1" : "0")}
WHEN NOT MATCHED THEN
    INSERT (ID_SISTEMA_MENSAGEM, ID_DESTINO_MENSAGEM, PREFIXO, CODIGO, MODULO, ELEMENTO, OBSERVACAO, VERIFICADO, EXCLUIDO)
    VALUES ({message.IdSistemaMensagem}, {message.IdDestinoMensagem},
            '{message.Prefixo}', '{message.Codigo}', '{message.Modulo}',
            {(message.Elemento == null ? "NULL" : $"'{message.Elemento}'")},
            {(message.Observacao == null ? "NULL" : $"'{message.Observacao}'")},
            {(message.Verificado ? "1" : "0")}, 0)";

            var languageUpserts = message.Languages.Values.Select(lang => $@"MERGE INTO TCPCONF.MENSAGEM_IDIOMA mi
USING (
    SELECT m.ID_MENSAGEM
    FROM TCPCONF.MENSAGEM m
    WHERE m.PREFIXO = '{message.Prefixo}' AND m.CODIGO = '{message.Codigo}'
) m
ON (mi.ID_MENSAGEM = m.ID_MENSAGEM AND mi.IDIOMA = {lang.Idioma})
WHEN MATCHED THEN
    UPDATE SET
        mi.TITULO = {(lang.Titulo == null ? "NULL" : $"'{lang.Titulo?.Replace("'", "''")}'")},
        mi.DESCRICAO = '{lang.Descricao?.Replace("'", "''")}',
        mi.AJUDA = {(lang.Ajuda == null ? "NULL" : $"'{lang.Ajuda?.Replace("'", "''")}'")},
        mi.EXCLUIDO = 0
WHEN NOT MATCHED THEN
    INSERT (ID_MENSAGEM, IDIOMA, TITULO, DESCRICAO, AJUDA, EXCLUIDO)
    VALUES (m.ID_MENSAGEM, {lang.Idioma},
            {(lang.Titulo == null ? "NULL" : $"'{lang.Titulo?.Replace("'", "''")}'")},
            '{lang.Descricao?.Replace("'", "''")}',
            {(lang.Ajuda == null ? "NULL" : $"'{lang.Ajuda?.Replace("'", "''")}'")}, 0)");

            var fullSql = string.Join(";\n\n", new[] { baseUpsert }.Concat(languageUpserts));
            return SqlFormatter.Of(Dialect.PlSql).Format(fullSql);
        }
    }
}