using Common.Models;
using Oracle.ManagedDataAccess.Client;
using SQL.Formatter;
using SQL.Formatter.Language;

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
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? containerNumber = null,
            string? placa = null,
            string? motorista = null,
            string? moveType = null,
            int? idAgendamento = null,
            string? status = null,
            int pageSize = 10,
            int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            var config = _configService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(x => x.Name == environment)
                ?? throw new ArgumentException($"Environment {environment} not found");

            using var connection = new OracleConnection(oracleEnv.ConnectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = BuildQuery(startDate, endDate, containerNumber, placa, motorista, moveType, idAgendamento, status, pageSize, pageNumber);

            var result = new List<LtdbLtvcRecord>();
            int totalCount = 0;

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new LtdbLtvcRecord
                {
                    DataLtdb = reader.IsDBNull(reader.GetOrdinal("DATA_LTDB")) ? null : reader.GetDateTime(reader.GetOrdinal("DATA_LTDB")),
                    DataLtvc = reader.IsDBNull(reader.GetOrdinal("DATA_LTVC")) ? null : reader.GetDateTime(reader.GetOrdinal("DATA_LTVC")),
                    RequestId = reader.IsDBNull(reader.GetOrdinal("REQUEST_ID")) ? null : reader.GetString(reader.GetOrdinal("REQUEST_ID")),
                    IdAgendamento = reader.IsDBNull(reader.GetOrdinal("ID_AGENDAMENTO")) ? null : reader.GetInt32(reader.GetOrdinal("ID_AGENDAMENTO")),
                    MoveType = reader.IsDBNull(reader.GetOrdinal("MOVETYPE")) ? null : reader.GetString(reader.GetOrdinal("MOVETYPE")),
                    Placa = reader.IsDBNull(reader.GetOrdinal("PLACA")) ? null : reader.GetString(reader.GetOrdinal("PLACA")),
                    Motorista = reader.IsDBNull(reader.GetOrdinal("MOTORISTA")) ? null : reader.GetString(reader.GetOrdinal("MOTORISTA")),
                    LtdbXml = reader.IsDBNull(reader.GetOrdinal("LTDB_XML")) ? null : reader.GetString(reader.GetOrdinal("LTDB_XML")),
                    LtvcXml = reader.IsDBNull(reader.GetOrdinal("LTVC_XML")) ? null : reader.GetString(reader.GetOrdinal("LTVC_XML")),
                    Delay = reader.IsDBNull(reader.GetOrdinal("DELAY")) ? null : reader.GetTimeSpan(reader.GetOrdinal("DELAY")),
                    Status = reader.IsDBNull(reader.GetOrdinal("STATUS")) ? null : reader.GetString(reader.GetOrdinal("STATUS")),
                    MessageText = reader.IsDBNull(reader.GetOrdinal("MESSAGE_TEXT")) ? null : reader.GetString(reader.GetOrdinal("MESSAGE_TEXT")),
                    ContainerNumbers = reader.IsDBNull(reader.GetOrdinal("CONTAINER_NUMBERS")) ? null : reader.GetString(reader.GetOrdinal("CONTAINER_NUMBERS"))
                });
                totalCount = reader.GetInt32(reader.GetOrdinal("TotalCount"));
            }

            return (Results: result, TotalCount: totalCount);
        }

        public string BuildQuery(
            DateTime? startDate,
            DateTime? endDate,
            string? containerNumber,
            string? placa,
            string? motorista,
            string? moveType,
            int? idAgendamento,
            string? status,
            int pageSize,
            int pageNumber)
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
                conditions.Add($"LTVC.ID_AGENDAMENTO = {idAgendamento}");

            if (!string.IsNullOrEmpty(status))
                conditions.Add($"LTVC_STATUS = '{status}'");

            var whereClause = conditions.Any()
                ? $"AND {string.Join(" AND ", conditions)}"
                : "";

            var sql = @$"
WITH /*+ INLINE */ CONTAINERS_AGG AS (
    SELECT
        LTDB_ID,
        LISTAGG(NVL(CONTAINER_NUM, 'INVALID_XML'), ', ') WITHIN GROUP (ORDER BY CONTAINER_SEQ) AS CONTAINER_NUMBERS
    FROM (
        SELECT 
            LTDB.ID AS LTDB_ID,
            CONTAINER_SEQ,
            CONTAINER_NUM
        FROM 
            TCPSGATE.TRACKING LTDB
        CROSS JOIN XMLTABLE(
            XMLNAMESPACES('http://www.aps-technology.com' AS ""ns""),
            '/ns:LTDB/ns:Chassis/ns:Container'
            PASSING CASE 
                WHEN LTDB.XML LIKE '<%</LTDB>' THEN XMLTYPE(LTDB.XML)
                ELSE NULL
            END
            COLUMNS 
                CONTAINER_SEQ NUMBER PATH '@sequence',
                CONTAINER_NUM VARCHAR2(100) PATH '@number'
        ) CONTAINERS
        WHERE LTDB.TYPE = 'LTDB'
            AND LTDB.XML LIKE '<%</LTDB>'
    )
    GROUP BY LTDB_ID
),
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
        NVL(LTVC_MESSAGE, 'NO_MESSAGE') AS MESSAGE_TEXT,
        CONTAINERS_AGG.CONTAINER_NUMBERS
    FROM 
        TCPSGATE.TRACKING LTDB
    LEFT JOIN TCPSGATE.TRACKING LTVC 
        ON LTDB.REQUEST_ID = LTVC.REQUEST_ID 
        AND LTVC.TYPE = 'LTVC'
    LEFT JOIN TCPAPI.AGE_GUIA_AGENDAMENTO GUI 
        ON GUI.ID_AGENDAMENTO = LTVC.ID_AGENDAMENTO
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
    LEFT JOIN CONTAINERS_AGG
        ON CONTAINERS_AGG.LTDB_ID = LTDB.ID
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
    }
}
