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

        public async Task<(List<RequisicaoExecucao> Results, int TotalCount)> ExecuteQueryAsync(
            string environment,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? urlFilter = null,
            string? httpMethod = null,
            string[]? containerNumbers = null,
            int? userId = null,
            int? execucaoId = null,
            int pageSize = 10,
            int pageNumber = 1,
            string? httpStatusRange = null,
            string? responseStatus = null,
            CancellationToken cancellationToken = default)
        {
            var config = _configService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(x => x.Name == environment)
                ?? throw new ArgumentException($"Environment {environment} not found");

            using var connection = new OracleConnection(oracleEnv.ConnectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = BuildQuery(startDate, endDate, urlFilter, httpMethod, containerNumbers, nomeFluxo, userId, execucaoId, pageSize, pageNumber, httpStatusRange, responseStatus);

            var result = new List<RequisicaoExecucao>();
            int totalCount = 0;
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
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
                    Duration = reader.IsDBNull(reader.GetOrdinal("DELAY")) ? null : reader.GetTimeSpan(reader.GetOrdinal("DELAY")),
                    DataInicio = reader.IsDBNull("DATA_INICIO") ? DateTime.MinValue : reader.GetDateTime("DATA_INICIO"),
                    IdUsuarioInclusao = reader.IsDBNull("ID_USUARIO_INCLUSAO") ? 0 : reader.GetInt32("ID_USUARIO_INCLUSAO")
                });
                totalCount = reader.GetInt32("TotalCount");
            }

            return (Results: result, TotalCount: totalCount);
        }

        private string BuildQuery(DateTime? startDate, DateTime? endDate, string? urlFilter, string? httpMethod,
            string[]? containerNumbers, string? nomeFluxo, int? userId, int? execucaoId, int pageSize, int pageNumber,
            string? httpStatusRange, string? responseStatus)
        {
            var conditions = new List<string>();

            if (startDate.HasValue)
                conditions.Add($"RE.DATA_INICIO >= TO_DATE('{startDate:yy-MM-dd HH:mm:ss}', 'YY-MM-DD HH24:MI:SS')");

            if (endDate.HasValue)
                conditions.Add($"RE.DATA_INICIO <= TO_DATE('{endDate:yy-MM-dd HH:mm:ss}', 'YY-MM-DD HH24:MI:SS')");

            if (!string.IsNullOrEmpty(urlFilter))
                conditions.Add($"(RE.URL LIKE '%{urlFilter}%' OR RE.NOME_FLUXO LIKE '%{urlFilter}%')");

            if (!string.IsNullOrEmpty(httpMethod))
                conditions.Add($"RE.HTTP_METHOD = '{httpMethod}'");

            if (containerNumbers?.Any() == true)
            {
                var containerConditions = containerNumbers.Select(c => $"RE.REQUISICAO LIKE '%{c}%'");
                conditions.Add($"({string.Join(" OR ", containerConditions)})");
            }

            if (userId.HasValue)
                conditions.Add($"RE.ID_USUARIO_INCLUSAO = {userId}");

            if (execucaoId.HasValue)
                conditions.Add($"RE.ID_EXECUCAO = {execucaoId}");

            if (!string.IsNullOrEmpty(httpStatusRange))
            {
                var range = httpStatusRange.Replace("xx", "");
                conditions.Add($"RE.HTTP_STATUS_CODE LIKE '{range}%'");
            }

            if (!string.IsNullOrEmpty(responseStatus))
            {
                conditions.Add(@"
                    (
                        (REGEXP_LIKE(RE.RESPOSTA, '<Status>\s*' || '" + responseStatus + @"' || '\s*</Status>') AND REGEXP_LIKE(RE.RESPOSTA, '(?s)^.*<.*>.*$'))
                        OR
                        (REGEXP_LIKE(RE.RESPOSTA, '""Status""\s*:\s*' || '" + responseStatus + @"' || '\s*[,}]') AND REGEXP_LIKE(RE.RESPOSTA, '(?s)^.*{.*}.*$'))
                    )");
            }

            var whereClause = conditions.Any()
                ? $"WHERE {string.Join(" AND ", conditions)}"
                : "WHERE 1=1";

            return @$"
WITH RequisicaoExecucao AS (
    SELECT 'Requisição' AS SOURCE, E.ID_EXECUCAO, e.HTTP_METHOD, e.HTTP_STATUS_CODE,
           REQ.CONTEUDO AS REQUISICAO, RESP.CONTEUDO AS RESPOSTA, NULL as ERRO,
           E.NOME_FLUXO, E.END_POINT, E.URL, (e.data_fim - e.data_inicio) as DELAY,
           E.DATA_INICIO, E.ID_USUARIO_INCLUSAO
    FROM TCPESB.REQUISICAO E
    LEFT JOIN TCPESB.MENSAGEM REQ ON E.ID_MSG_ENTRADA = REQ.ID_MENSAGEM
    LEFT JOIN TCPESB.MENSAGEM RESP ON E.ID_MSG_SAIDA = RESP.ID_MENSAGEM

    UNION ALL

    SELECT 'Execução' AS SOURCE, E.ID_EXECUCAO, null as HTTP_METHOD,
           null as HTTP_STATUS_CODE, REQ.CONTEUDO AS REQUISICAO,
           RESP.CONTEUDO AS RESPOSTA, ERRO.CONTEUDO AS ERRO,
           E.NOME_FLUXO, null as END_POINT, E.URL, (e.data_fim - e.data_inicio) as DELAY,
           E.DATA_INICIO, E.ID_USUARIO_INCLUSAO
    FROM TCPESB.EXECUCAO E
    LEFT JOIN TCPESB.MENSAGEM REQ ON E.ID_MSG_ENTRADA = REQ.ID_MENSAGEM
    LEFT JOIN TCPESB.MENSAGEM RESP ON E.ID_MSG_SAIDA = RESP.ID_MENSAGEM
    LEFT JOIN TCPESB.MENSAGEM ERRO ON E.ID_MSG_ERRO = ERRO.ID_MENSAGEM
),
CountQuery AS (
    SELECT COUNT(*) as TotalCount
    FROM RequisicaoExecucao RE
    {whereClause}
),
PagedQuery AS (
    SELECT RE.* 
    FROM RequisicaoExecucao RE
    {whereClause}
    ORDER BY RE.DATA_INICIO DESC
    OFFSET {(pageNumber - 1) * pageSize} ROWS FETCH NEXT {pageSize} ROWS ONLY
)
SELECT q.*, c.TotalCount
FROM PagedQuery q
CROSS JOIN CountQuery c";
        }

        public async Task<Dictionary<int, string>> GetUsersAsync(string environment, string? searchText = null)
        {
            var config = _configService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(x => x.Name == environment)
                ?? throw new ArgumentException($"Environment {environment} not found");

            using var connection = new OracleConnection(oracleEnv.ConnectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id_usuario, login 
                FROM TCPCAD.login 
                WHERE UPPER(login) LIKE '%' || UPPER(:searchText) || '%'
                FETCH FIRST 10 ROWS ONLY";
            
            cmd.Parameters.Add(new OracleParameter("searchText", searchText ?? string.Empty));

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
