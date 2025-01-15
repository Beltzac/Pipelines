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
                var messageQuery = @"
                    SELECT m.ID_MENSAGEM, m.ID_SISTEMA_MENSAGEM, m.ID_DESTINO_MENSAGEM, m.ID_GRUPO_MENSAGEM,
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
                            IdGrupoMensagem = reader.GetInt64(3),
                            Verificado = reader.GetInt32(4) == 1,
                            Modulo = reader.GetString(5),
                            Codigo = reader.GetString(6),
                            Prefixo = reader.GetString(7),
                            Elemento = reader.IsDBNull(8) ? null : reader.GetString(8),
                            Observacao = reader.IsDBNull(9) ? null : reader.GetString(9)
                        };

                        message.Key = $"{message.Prefixo}-{message.Codigo}";

                        messages[message.Key] = message;
                    }
                }

                // Fetch language-specific data
                var languageQuery = @"
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
            CompareProperty("Modulo", source?.Modulo, target?.Modulo, diffs, diff.ChangedFields);
            CompareProperty("Codigo", source?.Codigo, target?.Codigo, diffs, diff.ChangedFields);
            CompareProperty("Prefixo", source?.Prefixo, target?.Prefixo, diffs, diff.ChangedFields);
            CompareProperty("Elemento", source?.Elemento, target?.Elemento, diffs, diff.ChangedFields);
            CompareProperty("Observacao", source?.Observacao, target?.Observacao, diffs, diff.ChangedFields);
            CompareProperty("IdGrupoMensagem", source?.IdGrupoMensagem.ToString(), target?.IdGrupoMensagem.ToString(), diffs, diff.ChangedFields);

            // Compare language-specific content
            var allLanguages = new HashSet<int>(
                (source?.Languages.Keys ?? Enumerable.Empty<int>())
                .Union(target?.Languages.Keys ?? Enumerable.Empty<int>())
            );

            foreach (var language in allLanguages)
            {
                var sourceLang = source?.Languages.GetValueOrDefault(language);
                var targetLang = target?.Languages.GetValueOrDefault(language);

                CompareProperty($"Titulo ({language})", sourceLang?.Titulo, targetLang?.Titulo, diffs, diff.ChangedFields);
                CompareProperty($"Descricao ({language})", sourceLang?.Descricao, targetLang?.Descricao, diffs, diff.ChangedFields);
                CompareProperty($"Ajuda ({language})", sourceLang?.Ajuda, targetLang?.Ajuda, diffs, diff.ChangedFields);
            }

            if (diffs.Count > 0)
            {
                diff.HasDifferences = true;
                diff.FormattedDiff = string.Join("\n", diffs);
            }

            return diff;
        }

        private void CompareProperty(string propertyName, string sourceValue, string targetValue, List<string> diffs, List<string> changedFields)
        {
            if (sourceValue?.Trim() != targetValue?.Trim())
            {
                diffs.Add($"{propertyName}:\n- {sourceValue}\n+ {targetValue}");
                changedFields.Add(propertyName);
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
        m.ID_SISTEMA_MENSAGEM = {message.IdSistemaMensagem},
        m.ID_DESTINO_MENSAGEM = {message.IdDestinoMensagem},
        m.ID_GRUPO_MENSAGEM = {message.IdGrupoMensagem},
        m.MODULO = '{message.Modulo}',
        m.ELEMENTO = {(message.Elemento == null ? "NULL" : $"'{message.Elemento}'")},
        m.OBSERVACAO = {(message.Observacao == null ? "NULL" : $"'{message.Observacao}'")},
        m.VERIFICADO = {(message.Verificado ? "1" : "0")},
        m.DATA_ALTERACAO = SYSDATE,
        m.ID_USUARIO_ALTERACAO = 30120
WHEN NOT MATCHED THEN
    INSERT (ID_SISTEMA_MENSAGEM, ID_DESTINO_MENSAGEM, ID_GRUPO_MENSAGEM, PREFIXO, CODIGO, MODULO, ELEMENTO, OBSERVACAO, VERIFICADO, EXCLUIDO,
            DATA_INCLUSAO, DATA_ALTERACAO, ID_USUARIO_INCLUSAO, ID_USUARIO_ALTERACAO)
    VALUES ({message.IdSistemaMensagem}, {message.IdDestinoMensagem}, {message.IdGrupoMensagem},
            '{message.Prefixo}', '{message.Codigo}', '{message.Modulo}',
            {(message.Elemento == null ? "NULL" : $"'{message.Elemento}'")},
            {(message.Observacao == null ? "NULL" : $"'{message.Observacao}'")},
            {(message.Verificado ? "1" : "0")}, 0,
            SYSDATE, SYSDATE, 30120, 30120)";

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
        mi.EXCLUIDO = 0,
        mi.DATA_ALTERACAO = SYSDATE,
        mi.ID_USUARIO_ALTERACAO = 30120
WHEN NOT MATCHED THEN
    INSERT (ID_MENSAGEM, IDIOMA, TITULO, DESCRICAO, AJUDA, EXCLUIDO,
            DATA_INCLUSAO, DATA_ALTERACAO, ID_USUARIO_INCLUSAO, ID_USUARIO_ALTERACAO)
    VALUES (m.ID_MENSAGEM, {lang.Idioma},
            {(lang.Titulo == null ? "NULL" : $"'{lang.Titulo?.Replace("'", "''")}'")},
            '{lang.Descricao?.Replace("'", "''")}',
            {(lang.Ajuda == null ? "NULL" : $"'{lang.Ajuda?.Replace("'", "''")}'")}, 0,
            SYSDATE, SYSDATE, 30120, 30120)");

            var fullSql = string.Join(";\n\n", new[] { baseUpsert }.Concat(languageUpserts)) + ";";
            return SqlFormatter.Of(Dialect.PlSql).Format(fullSql);
        }
    }
}