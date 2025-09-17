using Common.Models;
using Common.Repositories.TCP.Interfaces;
using Common.Services.Interfaces;
using SQL.Formatter;
using SQL.Formatter.Language;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Common.Services
{
    public class EsbService : IEsbService
    {
        private readonly IOracleRepository _repo;
        private readonly IConfigurationService _configService;

        public EsbService(IOracleRepository repo, IConfigurationService configService)
        {
            _repo = repo;
            _configService = configService;
        }

        public EsbService(IOracleRepository repo)
        {
            _repo = repo;
        }

        public async Task<(List<RequisicaoExecucao> Results, int TotalCount)> ExecuteQueryAsync(
            string environment,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null,
            string? urlFilter = null,
            string? httpMethod = null,
            string? genericText = null,
            long? userId = null,
            int? execucaoId = null,
            int pageSize = 10,
            int pageNumber = 1,
            string? httpStatusRange = null,
            string? responseStatus = null,
            int? minDelaySeconds = null,
            CancellationToken cancellationToken = default)
        {
            var sql = BuildQuery(startDate, endDate, urlFilter, httpMethod, genericText, userId,
                execucaoId, pageSize, pageNumber, httpStatusRange, responseStatus, minDelaySeconds);

            var results = await _repo.GetFromSqlAsync<RequisicaoExecucao>(environment, FormattableStringFactory.Create(sql), cancellationToken);

            return (
                Results: results,
                TotalCount: results.FirstOrDefault()?.TotalCount ?? 0
            );
        }

        public string BuildQuery(
            DateTimeOffset? startDate,
            DateTimeOffset? endDate,
            string? urlFilter,
            string? httpMethod,
            string? genericText,
            long? userId,
            int? execucaoId,
            int pageSize,
            int pageNumber,
            string? httpStatusRange,
            string? responseStatus,
            int? minDelaySeconds)
        {
            var requisicaoConditions = new List<string>();
            var execucaoConditions = new List<string>();
            var unionConditions = new List<string>();

            // Common conditions for both REQUISICAO and EXECUCAO
            if (minDelaySeconds.HasValue)
            {
                requisicaoConditions.Add($"EXTRACT(SECOND FROM (E.DATA_FIM - E.DATA_INICIO)) + EXTRACT(MINUTE FROM (E.DATA_FIM - E.DATA_INICIO)) * 60 + EXTRACT(HOUR FROM (E.DATA_FIM - E.DATA_INICIO)) * 3600 >= {minDelaySeconds.Value}");
                execucaoConditions.Add($"EXTRACT(SECOND FROM (E.DATA_FIM - E.DATA_INICIO)) + EXTRACT(MINUTE FROM (E.DATA_FIM - E.DATA_INICIO)) * 60 + EXTRACT(HOUR FROM (E.DATA_FIM - E.DATA_INICIO)) * 3600 >= {minDelaySeconds.Value}");
                unionConditions.Add($"EXTRACT(SECOND FROM (RE.DATA_FIM - RE.DATA_INICIO)) + EXTRACT(MINUTE FROM (RE.DATA_FIM - RE.DATA_INICIO)) * 60 + EXTRACT(HOUR FROM (RE.DATA_FIM - RE.DATA_INICIO)) * 3600 >= {minDelaySeconds.Value}");
            }

            if (!string.IsNullOrEmpty(urlFilter))
            {
                requisicaoConditions.Add($"(E.URL LIKE '%{urlFilter}%' OR E.NOME_FLUXO LIKE '%{urlFilter}%')");
                execucaoConditions.Add($"(E.URL LIKE '%{urlFilter}%' OR E.NOME_FLUXO LIKE '%{urlFilter}%')");
                unionConditions.Add($"(RE.URL LIKE '%{urlFilter}%' OR RE.NOME_FLUXO LIKE '%{urlFilter}%')");
            }

            if (!string.IsNullOrEmpty(httpMethod))
            {
                requisicaoConditions.Add($"E.HTTP_METHOD = '{httpMethod}'");
                execucaoConditions.Add($"E.HTTP_METHOD = '{httpMethod}'");
                unionConditions.Add($"RE.HTTP_METHOD = '{httpMethod}'");
            }

            if (userId.HasValue)
            {
                requisicaoConditions.Add($"E.ID_USUARIO_INCLUSAO = {userId}");
                execucaoConditions.Add($"E.ID_USUARIO_INCLUSAO = {userId}");
                unionConditions.Add($"RE.ID_USUARIO_INCLUSAO = {userId}");
            }

            if (execucaoId.HasValue)
            {
                requisicaoConditions.Add($"E.ID_EXECUCAO = {execucaoId}");
                execucaoConditions.Add($"E.ID_EXECUCAO = {execucaoId}");
                unionConditions.Add($"RE.ID_EXECUCAO = {execucaoId}");
            }

            if (!string.IsNullOrEmpty(httpStatusRange))
            {
                var range = httpStatusRange.Replace("xx", "");
                requisicaoConditions.Add($"E.HTTP_STATUS_CODE LIKE '{range}%'");
                execucaoConditions.Add($"E.HTTP_STATUS_CODE LIKE '{range}%'");
                unionConditions.Add($"RE.HTTP_STATUS_CODE LIKE '{range}%'");
            }

            // Specific conditions for REQUISICAO table (using DATA_INCLUSAO for index IX_REQUISICAO_DATA_STATUS)
            if (startDate.HasValue)
                requisicaoConditions.Add($"E.DATA_INCLUSAO >= TO_DATE('{startDate:yyyy-MM-dd HH:mm:ss}', 'YYYY-MM-DD HH24:MI:SS')");
            if (endDate.HasValue)
                requisicaoConditions.Add($"E.DATA_INCLUSAO <= TO_DATE('{endDate:yyyy-MM-dd HH:mm:ss}', 'YYYY-MM-DD HH24:MI:SS')");
            requisicaoConditions.Add("E.HTTP_STATUS_CODE IS NOT NULL"); // Ensure index usage

            if (!string.IsNullOrEmpty(genericText))
                requisicaoConditions.Add($@"
                    (
                        REGEXP_LIKE(REQ.CONTEUDO, '{genericText}')
                        OR
                        REGEXP_LIKE(RESP.CONTEUDO, '{genericText}')
                    )");

            if (!string.IsNullOrEmpty(responseStatus))
            {
                requisicaoConditions.Add($@"
                    (
                        (REGEXP_LIKE(RESP.CONTEUDO, '<Status>' || '{responseStatus}'  || '</Status>'))
                        OR
                        (REGEXP_LIKE(RESP.CONTEUDO, '""Status""\\s*:\\s*' || '{responseStatus}'))
                    )");
            }

            // Specific conditions for EXECUCAO table (using DATA_INICIO for index IX_PESQUISA)
            if (startDate.HasValue)
                execucaoConditions.Add($"E.DATA_INICIO >= TO_DATE('{startDate:yyyy-MM-dd HH:mm:ss}', 'YYYY-MM-DD HH24:MI:SS')");
            if (endDate.HasValue)
                execucaoConditions.Add($"E.DATA_INICIO <= TO_DATE('{endDate:yyyy-MM-dd HH:mm:ss}', 'YYYY-MM-DD HH24:MI:SS')");

            if (!string.IsNullOrEmpty(genericText))
                execucaoConditions.Add($@"
                    (
                        REGEXP_LIKE(REQ.CONTEUDO, '{genericText}')
                        OR
                        REGEXP_LIKE(RESP.CONTEUDO, '{genericText}')
                        OR
                        REGEXP_LIKE(ERRO.CONTEUDO, '{genericText}')
                    )");

            if (!string.IsNullOrEmpty(responseStatus))
            {
                execucaoConditions.Add($@"
                    (
                        (REGEXP_LIKE(RESP.CONTEUDO, '<Status>' || '{responseStatus}'  || '</Status>'))
                        OR
                        (REGEXP_LIKE(RESP.CONTEUDO, '""Status""\\s*:\\s*' || '{responseStatus}'))
                    )");
            }

            var requisicaoWhereClause = requisicaoConditions.Any()
                ? $"WHERE {string.Join(" AND ", requisicaoConditions)}"
                : "WHERE 1=1";

            var execucaoWhereClause = execucaoConditions.Any()
                ? $"WHERE {string.Join(" AND ", execucaoConditions)}"
                : "WHERE 1=1";

            var unionWhereClause = unionConditions.Any()
                ? $"WHERE {string.Join(" AND ", unionConditions)}"
                : "WHERE 1=1";

            var sql = @$"
WITH RequisicaoExecucao AS (
    SELECT 'Requisição' AS SOURCE, E.ID_EXECUCAO, e.HTTP_METHOD, e.HTTP_STATUS_CODE,
           REQ.CONTEUDO AS REQUISICAO, RESP.CONTEUDO AS RESPOSTA, NULL as ERRO,
           E.NOME_FLUXO, E.END_POINT, E.URL, E.DATA_FIM, (COALESCE(e.data_fim, SYSDATE) - e.data_inicio) as DURATION,
           E.DATA_INICIO, E.ID_USUARIO_INCLUSAO, L.LOGIN as USER_LOGIN
    FROM TCPESB.REQUISICAO E
    LEFT JOIN TCPCAD.LOGIN L ON E.ID_USUARIO_INCLUSAO = L.ID_USUARIO
    LEFT JOIN TCPESB.MENSAGEM REQ ON E.ID_MSG_ENTRADA = REQ.ID_MENSAGEM
    LEFT JOIN TCPESB.MENSAGEM RESP ON E.ID_MSG_SAIDA = RESP.ID_MENSAGEM
    {requisicaoWhereClause}

    UNION ALL

    SELECT 'Execução' AS SOURCE, E.ID_EXECUCAO, null as HTTP_METHOD,
           null as HTTP_STATUS_CODE, REQ.CONTEUDO AS REQUISICAO,
           RESP.CONTEUDO AS RESPOSTA, ERRO.CONTEUDO AS ERRO,
           E.NOME_FLUXO, null as END_POINT, E.URL, E.DATA_FIM, (COALESCE(e.data_fim, SYSDATE) - e.data_inicio) as DURATION,
           E.DATA_INICIO, E.ID_USUARIO_INCLUSAO, L.LOGIN as USER_LOGIN
    FROM TCPESB.EXECUCAO E
    LEFT JOIN TCPCAD.LOGIN L ON E.ID_USUARIO_INCLUSAO = L.ID_USUARIO
    LEFT JOIN TCPESB.MENSAGEM REQ ON E.ID_MSG_ENTRADA = REQ.ID_MENSAGEM
    LEFT JOIN TCPESB.MENSAGEM RESP ON E.ID_MSG_SAIDA = RESP.ID_MENSAGEM
    LEFT JOIN TCPESB.MENSAGEM ERRO ON E.ID_MSG_ERRO = ERRO.ID_MENSAGEM
    {execucaoWhereClause}
),
CountQuery AS (
    SELECT COUNT(*) as TOTAL_COUNT FROM RequisicaoExecucao RE
    {unionWhereClause}
),
PagedQuery AS (
    SELECT RE.*
    FROM RequisicaoExecucao RE
    {unionWhereClause}
    ORDER BY RE.DATA_INICIO DESC
    OFFSET {(pageNumber - 1) * pageSize} ROWS FETCH NEXT {pageSize} ROWS ONLY
)
SELECT q.*, c.TOTAL_COUNT
FROM PagedQuery q
CROSS JOIN CountQuery c";

            return SqlFormatter.Of(Dialect.PlSql).Format(sql);
        }

        public async Task<string> GetEsbSequencesAsync(string soapRequest, EsbServerConfig esbServer)
        {
            if (esbServer == null || string.IsNullOrEmpty(esbServer.Url))
            {
                throw new InvalidOperationException("ESB Server configuration is invalid or URL is missing.");
            }


            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            using var client = new HttpClient(handler);

            if (!string.IsNullOrEmpty(esbServer.Username) && !string.IsNullOrEmpty(esbServer.Password))
            {
                var byteArray = Encoding.ASCII.GetBytes($"{esbServer.Username}:{esbServer.Password}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }

            var fullUrl = esbServer.Url + "/services/SequenceAdminService.SequenceAdminServiceHttpsSoap11Endpoint";

            var request = new HttpRequestMessage(HttpMethod.Post, fullUrl);
            request.Headers.Add("SOAPAction", "urn:getSequences");
            request.Content = new StringContent(soapRequest, System.Text.Encoding.UTF8, "text/xml");

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

         public async Task<List<SequenceInfo>> GetSequencesAsync(EsbServerConfig esbServer)
         {
             var soapRequest = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:xsd=""http://org.apache.synapse/xsd"">
                <soapenv:Header/>
                <soapenv:Body>
                   <xsd:getSequences>
                      <xsd:pageNumber>0</xsd:pageNumber>
                      <xsd:sequencePerPage>9999</xsd:sequencePerPage>
                   </xsd:getSequences>
                </soapenv:Body>
             </soapenv:Envelope>";

             var soapResponse = await GetEsbSequencesAsync(soapRequest, esbServer);
             return ParseSoapResponse(soapResponse);
         }

         private List<SequenceInfo> ParseSoapResponse(string soapResponse)
         {
             var parsedSequences = new List<SequenceInfo>();
             try
             {
                 System.Xml.Linq.XDocument doc = System.Xml.Linq.XDocument.Parse(soapResponse);
                 System.Xml.Linq.XNamespace ns = "http://org.apache.synapse/xsd";
                 System.Xml.Linq.XNamespace ax2522 = "http://to.common.sequences.carbon.wso2.org/xsd";

                 foreach (var returnElement in doc.Descendants(ns + "return"))
                 {
                     var sequence = new SequenceInfo
                     {
                         ArtifactContainerName = (string)returnElement.Element(ax2522 + "artifactContainerName"),
                         Description = (string)returnElement.Element(ax2522 + "description"),
                         EnableStatistics = (bool?)returnElement.Element(ax2522 + "enableStatistics") ?? false,
                         EnableTracing = (bool?)returnElement.Element(ax2522 + "enableTracing") ?? false,
                         IsEdited = (bool?)returnElement.Element(ax2522 + "isEdited") ?? false,
                         Name = (string)returnElement.Element(ax2522 + "name")
                     };
                     parsedSequences.Add(sequence);
                 }
             }
             catch (Exception ex)
             {
                 // Log the error, but don't re-throw to allow partial parsing
                 Console.WriteLine($"Error parsing SOAP response: {ex.Message}");
             }
             return parsedSequences;
         }
    }
}
