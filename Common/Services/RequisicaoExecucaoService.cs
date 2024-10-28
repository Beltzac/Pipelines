using Common.Models;
using Common.Services;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace Common.Services
{
    public class RequisicaoExecucaoService : IRequisicaoExecucaoService
    {
        private readonly IConfigurationService _configService;

        public RequisicaoExecucaoService(IConfigurationService configService)
        {
            _configService = configService;
        }

        public async Task<List<RequisicaoExecucao>> ExecuteQueryAsync(
            string environment,
            DateTime? startDate = null,
            string? urlFilter = null,
            string? httpMethod = null,
            string[]? containerNumbers = null,
            string? nomeFluxo = null,
            int? userId = null,
            int? execucaoId = null,
            int maxRows = 10)
        {
            var config = _configService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(x => x.Name == environment)
                ?? throw new ArgumentException($"Environment {environment} not found");

            using var connection = new OracleConnection(oracleEnv.ConnectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = BuildQuery(startDate, urlFilter, httpMethod, containerNumbers, nomeFluxo, userId, execucaoId, maxRows);

            var result = new List<RequisicaoExecucao>();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new RequisicaoExecucao
                {
                    Source = reader.IsDBNull("SOURCE") ? null : reader.GetString("SOURCE"),
                    IdExecucao = reader.GetInt32("ID_EXECUCAO"),
                    HttpMethod = reader.IsDBNull("HTTP_METHOD") ? null : reader.GetString("HTTP_METHOD"),
                    HttpStatusCode = reader.IsDBNull("HTTP_STATUS_CODE") ? null : reader.GetString("HTTP_STATUS_CODE"),
                    Requisicao = reader.IsDBNull("REQUISICAO") ? null : reader.GetString("REQUISICAO"),
                    Resposta = reader.IsDBNull("RESPOSTA") ? null : reader.GetString("RESPOSTA"),
                    Erro = reader.IsDBNull("ERRO") ? null : reader.GetString("ERRO"),
                    NomeFluxo = reader.IsDBNull("NOME_FLUXO") ? null : reader.GetString("NOME_FLUXO"),
                    EndPoint = reader.IsDBNull("END_POINT") ? null : reader.GetString("END_POINT"),
                    Url = reader.IsDBNull("URL") ? null : reader.GetString("URL"),
                    Duration = reader.IsDBNull(10) ? null : reader.GetValue(10) as TimeSpan?,
                    DataInicio = reader.IsDBNull("DATA_INICIO") ? DateTime.MinValue : reader.GetDateTime("DATA_INICIO"),
                    IdUsuarioInclusao = reader.IsDBNull("ID_USUARIO_INCLUSAO") ? 0 : reader.GetInt32("ID_USUARIO_INCLUSAO")
                });
            }

            return result;
        }

        private string BuildQuery(DateTime? startDate, string? urlFilter, string? httpMethod,
            string[]? containerNumbers, string? nomeFluxo, int? userId, int? execucaoId, int maxRows)
        {
            var conditions = new List<string>();

            if (startDate.HasValue)
                conditions.Add($"RE.DATA_INICIO > TO_DATE('{startDate:yy-MM-dd HH:mm:ss}', 'YY-MM-DD HH24:MI:SS')");

            if (!string.IsNullOrEmpty(urlFilter))
                conditions.Add($"RE.URL LIKE '%{urlFilter}%'");

            if (!string.IsNullOrEmpty(httpMethod))
                conditions.Add($"RE.HTTP_METHOD = '{httpMethod}'");

            if (containerNumbers?.Any() == true)
            {
                var containerConditions = containerNumbers.Select(c => $"RE.REQUISICAO LIKE '%{c}%'");
                conditions.Add($"({string.Join(" OR ", containerConditions)})");
            }

            if (!string.IsNullOrEmpty(nomeFluxo))
                conditions.Add($"RE.NOME_FLUXO = '{nomeFluxo}'");

            if (userId.HasValue)
                conditions.Add($"RE.ID_USUARIO_INCLUSAO = {userId}");

            if (execucaoId.HasValue)
                conditions.Add($"RE.ID_EXECUCAO = {execucaoId}");

            var whereClause = conditions.Any()
                ? $"WHERE {string.Join(" AND ", conditions)}"
                : "WHERE 1=1";

            return @$"
WITH RequisicaoExecucao AS (
    SELECT 'Requisição' AS SOURCE, E.ID_EXECUCAO, e.HTTP_METHOD, e.HTTP_STATUS_CODE,
           REQ.CONTEUDO AS REQUISICAO, RESP.CONTEUDO AS RESPOSTA, NULL as ERRO,
           E.NOME_FLUXO, E.END_POINT, E.URL, e.data_fim - e.data_inicio,
           E.DATA_INICIO, E.ID_USUARIO_INCLUSAO
    FROM TCPESB.REQUISICAO E
    LEFT JOIN TCPESB.MENSAGEM REQ ON E.ID_MSG_ENTRADA = REQ.ID_MENSAGEM
    LEFT JOIN TCPESB.MENSAGEM RESP ON E.ID_MSG_SAIDA = RESP.ID_MENSAGEM

    UNION ALL

    SELECT 'Execução' AS SOURCE, E.ID_EXECUCAO, null as HTTP_METHOD,
           null as HTTP_STATUS_CODE, REQ.CONTEUDO AS REQUISICAO,
           RESP.CONTEUDO AS RESPOSTA, ERRO.CONTEUDO AS ERRO,
           E.NOME_FLUXO, null as END_POINT, E.URL, e.data_fim - e.data_inicio,
           E.DATA_INICIO, E.ID_USUARIO_INCLUSAO
    FROM TCPESB.EXECUCAO E
    LEFT JOIN TCPESB.MENSAGEM REQ ON E.ID_MSG_ENTRADA = REQ.ID_MENSAGEM
    LEFT JOIN TCPESB.MENSAGEM RESP ON E.ID_MSG_SAIDA = RESP.ID_MENSAGEM
    LEFT JOIN TCPESB.MENSAGEM ERRO ON E.ID_MSG_ERRO = ERRO.ID_MENSAGEM
)
SELECT RE.* 
FROM RequisicaoExecucao RE
{whereClause}
ORDER BY RE.DATA_INICIO DESC
FETCH FIRST {maxRows} ROWS ONLY";
        }

        public async Task<Dictionary<int, string>> GetUsersAsync(string environment)
        {
            var config = _configService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(x => x.Name == environment)
                ?? throw new ArgumentException($"Environment {environment} not found");

            using var connection = new OracleConnection(oracleEnv.ConnectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT id_usuario, login FROM TCPCAD.login FETCH FIRST 10 ROWS ONLY";

            var users = new Dictionary<int, string>();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                users[reader.GetInt32(0)] = reader.GetString(1);
            }

            return users;
        }
    }
}
