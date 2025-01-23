using Common.Models;
using Dapper;
using Oracle.ManagedDataAccess.Client;
using SQL.Formatter;
using SQL.Formatter.Language;
using System.Data;

namespace Common.Services
{
    public class EsbService : IEsbService
    {
        private readonly IConfigurationService _configService;

        public EsbService(IConfigurationService configService)
        {
            _configService = configService;
        }

        public async Task<(List<RequisicaoExecucao> Results, int TotalCount)> ExecuteQueryAsync(
            string environment,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null,
            string? urlFilter = null,
            string? httpMethod = null,
            string? genericText = null,
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

            var sql = BuildQuery(startDate, endDate, urlFilter, httpMethod, genericText, userId, execucaoId, pageSize, pageNumber, httpStatusRange, responseStatus);
            
            var results = await connection.QueryAsync<RequisicaoExecucao, decimal, (RequisicaoExecucao Result, int TotalCount)>(
                sql,
                (requisicao, totalCount) => (requisicao, Convert.ToInt32(totalCount)),
                splitOn: "TotalCount",
                commandTimeout: 120);

            var processed = results.ToList();
            return (
                Results: processed.Select(x => x.Result).ToList(),
                TotalCount: processed.FirstOrDefault().TotalCount
            );
        }

        public string BuildQuery(
            DateTimeOffset? startDate,
            DateTimeOffset? endDate,
            string? urlFilter,
            string? httpMethod,
            string? genericText,
            int? userId,
            int? execucaoId,
            int pageSize,
            int pageNumber,
            string? httpStatusRange,
            string? responseStatus)
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

            if (!string.IsNullOrEmpty(genericText))
                conditions.Add($@"
                    (
                        REGEXP_LIKE(RE.REQUISICAO, '{genericText}')
                        OR
                        REGEXP_LIKE(RE.RESPOSTA, '{genericText}')
                        OR
                        REGEXP_LIKE(RE.ERRO, '{genericText}')
                    )");

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
                        (REGEXP_LIKE(RE.RESPOSTA, '<Status>' || '" + responseStatus + @"'  || '</Status>'))
                        OR
                        (REGEXP_LIKE(RE.RESPOSTA, '""Status""\s*:\s*""?' || '" + responseStatus + @"' || '""?[\s,}]'))
                    )");
            }

            var whereClause = conditions.Any()
                ? $"WHERE {string.Join(" AND ", conditions)}"
                : "WHERE 1=1";

            var sql = @$"
WITH RequisicaoExecucao AS (
    SELECT 'Requisição' AS SOURCE, E.ID_EXECUCAO, e.HTTP_METHOD, e.HTTP_STATUS_CODE,
           REQ.CONTEUDO AS REQUISICAO, RESP.CONTEUDO AS RESPOSTA, NULL as ERRO,
           E.NOME_FLUXO, E.END_POINT, E.URL, (e.data_fim - e.data_inicio) as DELAY,
           E.DATA_INICIO, E.ID_USUARIO_INCLUSAO, L.LOGIN as USER_LOGIN
    FROM TCPESB.REQUISICAO E
    LEFT JOIN TCPCAD.LOGIN L ON E.ID_USUARIO_INCLUSAO = L.ID_USUARIO
    LEFT JOIN TCPESB.MENSAGEM REQ ON E.ID_MSG_ENTRADA = REQ.ID_MENSAGEM
    LEFT JOIN TCPESB.MENSAGEM RESP ON E.ID_MSG_SAIDA = RESP.ID_MENSAGEM

    UNION ALL

    SELECT 'Execução' AS SOURCE, E.ID_EXECUCAO, null as HTTP_METHOD,
           null as HTTP_STATUS_CODE, REQ.CONTEUDO AS REQUISICAO,
           RESP.CONTEUDO AS RESPOSTA, ERRO.CONTEUDO AS ERRO,
           E.NOME_FLUXO, null as END_POINT, E.URL, (e.data_fim - e.data_inicio) as DELAY,
           E.DATA_INICIO, E.ID_USUARIO_INCLUSAO, L.LOGIN as USER_LOGIN
    FROM TCPESB.EXECUCAO E
    LEFT JOIN TCPCAD.LOGIN L ON E.ID_USUARIO_INCLUSAO = L.ID_USUARIO
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

            return SqlFormatter.Of(Dialect.PlSql).Format(sql);
        }

        public async Task<Dictionary<int, string>> GetUsersAsync(string environment, string? searchText = null)
        {
            var config = _configService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(x => x.Name == environment)
                ?? throw new ArgumentException($"Environment {environment} not found");

            using var connection = new OracleConnection(oracleEnv.ConnectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT id_usuario, login
                FROM TCPCAD.login
                WHERE UPPER(login) LIKE '%' || UPPER(:searchText) || '%'
                FETCH FIRST 10 ROWS ONLY";

            var results = await connection.QueryAsync<(int id_usuario, string login)>(
                sql,
                new { searchText = searchText ?? string.Empty });

            return results.ToDictionary(x => x.id_usuario, x => x.login);
        }
    }
}
