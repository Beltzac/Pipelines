using Common.Models;
using Dapper;
using Oracle.ManagedDataAccess.Client;
using SQL.Formatter;
using SQL.Formatter.Language;
using System.Data;

namespace Common.Services
{

    public class LtdbLtvcService : ILtdbLtvcService
    {
        private readonly IConfigurationService _configService;

        public LtdbLtvcService(IConfigurationService configService)
        {
            _configService = configService;
        }

        public async Task<(List<LtdbLtvcRecord> Results, int TotalCount)> ExecuteQueryAsync(
            string environment,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null,
            string? containerNumber = null,
            string? placa = null,
            string? motorista = null,
            string? moveType = null,
            long? idAgendamento = null,
            string? status = null,
            double? minDelay = null,
            int pageSize = 10,
            int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            var config = _configService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(x => x.Name == environment)
                ?? throw new ArgumentException($"Environment {environment} not found");

            using var connection = new OracleConnection(oracleEnv.ConnectionString);
            await connection.OpenAsync();

            var sql = BuildQuery(startDate, endDate, containerNumber, placa, motorista, moveType, idAgendamento, status, minDelay, pageSize, pageNumber);

            var queryResult = await connection.QueryAsync<LtdbLtvcRecord, decimal, (LtdbLtvcRecord Result, int TotalCount)>(
                sql,
                (record, total) => (record, Convert.ToInt32(total)),
                splitOn: "TotalCount",
                commandTimeout: 120
            );

            var processed = queryResult.ToList();
            return (
                Results: processed.Select(x => x.Result).ToList(),
                TotalCount: processed.FirstOrDefault().TotalCount
            );
        }
public string BuildQuery(
    DateTimeOffset? startDate,
    DateTimeOffset? endDate,
    string? containerNumber = null,
    string? placa = null,
    string? motorista = null,
    string? moveType = null,
    long? idAgendamento = null,
    string? status = null,
    double? minDelay = null,
    int pageSize = 10,
    int pageNumber = 1)
{
    var conditions = new List<string>();

    if (startDate.HasValue)
        conditions.Add($"LTDB.CREATED_AT >= TO_DATE('{startDate:yy-MM-dd HH:mm:ss}', 'YY-MM-DD HH24:MI:SS')");

    if (endDate.HasValue)
        conditions.Add($"LTDB.CREATED_AT <= TO_DATE('{endDate:yy-MM-dd HH:mm:ss}', 'YY-MM-DD HH24:MI:SS')");

    if (!string.IsNullOrEmpty(containerNumber))
        conditions.Add($"LTDB.XML LIKE '%{containerNumber}%'");

    if (!string.IsNullOrEmpty(placa))
        conditions.Add($"PLACA LIKE '%{placa}%'");

    if (!string.IsNullOrEmpty(motorista))
        conditions.Add($"MOTORISTA LIKE '%{motorista}%'");

    if (!string.IsNullOrEmpty(moveType))
        conditions.Add($"MOVETYPE = '{moveType}'");

    if (idAgendamento.HasValue)
        conditions.Add($"LTVC.ID_AGENDAMENTO = {idAgendamento.Value}");

    if (!string.IsNullOrEmpty(status))
        conditions.Add($"LTVC_STATUS = '{status}'");

    if (minDelay.HasValue)
        conditions.Add($"EXTRACT(SECOND FROM (LTVC.CREATED_AT - LTDB.CREATED_AT)) >= {minDelay.Value}");

            var whereClause = conditions.Any()
                ? $"AND {string.Join(" AND ", conditions)}"
                : "";

            var sql = @$"
WITH 
MainQuery AS (
    SELECT
        LTDB.CREATED_AT AS DATA_LTDB,
        LTVC.CREATED_AT AS DATA_LTVC,
        LTDB.REQUEST_ID,
        LTVC.ID_AGENDAMENTO,
        NVL(MOVETYPE, 'INVALID_XML') AS MOVETYPE,
        NVL(PLACA, 'INVALID_XML') AS PLACA,
        NVL(MOTORISTA, 'INVALID_XML') AS MOTORISTA,
        LTDB.XML AS LTDB_XML,
        LTVC.XML AS LTVC_XML,
        LTVC.CREATED_AT - LTDB.CREATED_AT AS DELAY,
        NVL(LTVC_STATUS, 'UNKNOWN') AS STATUS,
        COALESCE(
            TO_CHAR(LTVC_MESSAGE),
            --TO_CHAR(LTDB.EXCEPTION),
            --TO_CHAR(LTVC.EXCEPTION),
            'NO_MESSAGE'
        ) AS MESSAGE_TEXT,
		(
          SELECT
             LISTAGG(NVL(X.container_num, 'UNKNOWN'), ', ')
                WITHIN GROUP (ORDER BY X.container_seq)
          FROM 
        XMLTABLE(
          XMLNAMESPACES('http://www.aps-technology.com' AS ""ns""),
          '/ns:LTDB/ns:Chassis/ns:Container' 
					PASSING CASE
						WHEN LTDB.XML LIKE '<%</LTDB>' THEN XMLTYPE(LTDB.XML)
						ELSE NULL
					END
					COLUMNS 
					container_seq NUMBER PATH '@sequence',
          container_num VARCHAR2(100) PATH '@number'
        ) X
        ) AS CONTAINER_NUMBERS,
        AGE.CODIGO_BARRAS
    FROM
        TCPSGATE.TRACKING LTDB
    LEFT JOIN TCPSGATE.TRACKING LTVC
        ON LTDB.REQUEST_ID = LTVC.REQUEST_ID
        AND LTVC.TYPE = 'LTVC'
    LEFT JOIN TCPAGEND.AGENDAMENTO AGE
        ON LTVC.ID_AGENDAMENTO = AGE.ID_AGENDAMENTO
    LEFT JOIN XMLTABLE(
        XMLNAMESPACES('http://www.aps-technology.com' AS ""ns""),
        '/ns:LTDB'
        PASSING CASE
            WHEN LTDB.XML LIKE '<%</LTDB>' THEN XMLTYPE(LTDB.XML)
            ELSE NULL
        END
        COLUMNS
            MOVETYPE  VARCHAR2(100) PATH 'ns:Move/@movetype',
            PLACA     VARCHAR2(100) PATH 'ns:LPR/@number',
            MOTORISTA VARCHAR2(100) PATH 'ns:Driver/ns:ID'
    ) XML_EXTRACT_LTD
        ON 1=1


    LEFT JOIN XMLTABLE(
        XMLNAMESPACES('http://www.aps-technology.com' AS ""ns""),
        '/ns:LTVC'
        PASSING CASE
            WHEN LTVC.XML LIKE '<%</LTVC>' THEN XMLTYPE(LTVC.XML)
            ELSE NULL
        END
        COLUMNS
            LTVC_STATUS  VARCHAR2(10)  PATH 'ns:RequestResult/@status',
            LTVC_MESSAGE VARCHAR2(200) PATH 'ns:Errors/ns:Error/ns:MessageText'
    ) XML_EXTRACT_LTV
        ON 1=1

    WHERE 1 = 1
        AND LTDB.TYPE = 'LTDB'
        {whereClause}
),
CountQuery AS (
    SELECT COUNT(*) as TotalCount
    FROM MainQuery
),
PagedQuery AS (
    SELECT *
    FROM MainQuery
    ORDER BY DATA_LTDB DESC
    OFFSET {(pageNumber - 1) * pageSize} ROWS FETCH NEXT {pageSize} ROWS ONLY
)
SELECT q.*, c.TotalCount
FROM PagedQuery q
CROSS JOIN CountQuery c";

            return SqlFormatter.Of(Dialect.PlSql).Format(sql);
        }

        public async Task<List<DelayMetric>> GetDelayMetricsAsync(
            string environment,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null,
            string? containerNumber = null,
            string? placa = null,
            string? motorista = null,
            string? moveType = null,
            long? idAgendamento = null,
            string? status = null,
            CancellationToken cancellationToken = default)
        {
            var config = _configService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(x => x.Name == environment)
                ?? throw new ArgumentException($"Environment {environment} not found");

            using var connection = new OracleConnection(oracleEnv.ConnectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = BuildDelayMetricsQuery(startDate, endDate, containerNumber, placa, motorista, moveType, idAgendamento, status);

            var result = await connection.QueryAsync<DelayMetric>(
                cmd.CommandText,
                commandTimeout: cmd.CommandTimeout,
                commandType: CommandType.Text);

            return result.ToList();
        }

        private string BuildDelayMetricsQuery(
            DateTimeOffset? startDate,
            DateTimeOffset? endDate,
            string? containerNumber = null,
            string? placa = null,
            string? motorista = null,
            string? moveType = null,
            long? idAgendamento = null,
            string? status = null)
        {
            var conditions = new List<string>();

            if (startDate.HasValue)
                conditions.Add($"LTDB.CREATED_AT >= TO_DATE('{startDate:yy-MM-dd HH:mm:ss}', 'YY-MM-DD HH24:MI:SS')");

            if (endDate.HasValue)
                conditions.Add($"LTDB.CREATED_AT <= TO_DATE('{endDate:yy-MM-dd HH:mm:ss}', 'YY-MM-DD HH24:MI:SS')");

            if (!string.IsNullOrEmpty(containerNumber))
                conditions.Add($"LTDB.XML LIKE '%{containerNumber}%'");

            if (!string.IsNullOrEmpty(placa))
                conditions.Add($"LTDB.XML LIKE '%{placa}%'");

            if (!string.IsNullOrEmpty(motorista))
                conditions.Add($"LTDB.XML LIKE '%{motorista}%'");

            if (!string.IsNullOrEmpty(moveType))
                conditions.Add($"LTDB.XML LIKE '%{moveType}%'");

            if (idAgendamento.HasValue)
                conditions.Add($"LTVC.ID_AGENDAMENTO = {idAgendamento.Value}");

            if (!string.IsNullOrEmpty(status))
                conditions.Add($"LTVC.XML LIKE '%{status}%'");

            var whereClause = conditions.Any()
                ? $"AND {string.Join(" AND ", conditions)}"
                : "";

            return $@"
SELECT
    TO_CHAR(TRUNC(LTDB.CREATED_AT, 'HH'), 'YYYY-MM-DD HH24:MI:SS') as TIMESTAMP,
    CAST(AVG(EXTRACT(SECOND FROM (LTVC.CREATED_AT - LTDB.CREATED_AT))) AS NUMBER(10,2)) as AVG_DELAY_SECONDS,
    CAST(MAX(EXTRACT(SECOND FROM (LTVC.CREATED_AT - LTDB.CREATED_AT))) AS NUMBER(10,2)) as MAX_DELAY_SECONDS,
    COUNT(*) as REQUEST_COUNT
FROM
    TCPSGATE.TRACKING LTDB
INNER JOIN TCPSGATE.TRACKING LTVC
    ON LTDB.REQUEST_ID = LTVC.REQUEST_ID
    AND LTVC.TYPE = 'LTVC'
WHERE 1 = 1
    AND LTDB.TYPE = 'LTDB'
    {whereClause}
GROUP BY TRUNC(LTDB.CREATED_AT, 'HH')
ORDER BY TIMESTAMP ASC";
        }
    }
}
