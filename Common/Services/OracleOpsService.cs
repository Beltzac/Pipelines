using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Models;
using Common.Repositories.TCP.Interfaces;
using Common.Services.Interfaces;

namespace Common.Services
{
    public class OracleOpsService
    {
        private readonly IOracleRepository _repo;
        private readonly IConfigurationService _config;

        public OracleOpsService(IOracleRepository repo, IConfigurationService config)
        {
            _repo = repo;
            _config = config;
        }

        private class HourlyTeu
        {
            public DateTime Hour { get; set; }
            public int Teus { get; set; }
        }

        public async Task<Dictionary<DateTime, VesselPlan>> FetchVesselPlansAsync(DateTime startDate, DateTime endDate)
        {
            var result = new Dictionary<DateTime, VesselPlan>();
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == "CTOS OPS");
           // Query CNTRS via view V_CNTRS to compute TEUs in/out per hour
           var ins = await _repo.GetFromSqlAsync<HourlyTeu>(
               env.ConnectionString,
               (FormattableString)$@"
                   SELECT TRUNC(CNTR_YARD_TIME_IN,'HH24') as Hour,
                          SUM(CASE WHEN SUBSTR(CNTR_ISO,1,2)='40' THEN 2 ELSE 1 END) as Teus
                   FROM V_CNTRS
                   WHERE CNTR_IB_VISIT_ID IS NOT NULL
                     AND CNTR_YARD_TIME_IN BETWEEN {startDate} AND {endDate}
                   GROUP BY TRUNC(CNTR_YARD_TIME_IN,'HH24')",
               default);

           var outs = await _repo.GetFromSqlAsync<HourlyTeu>(
               env.ConnectionString,
               (FormattableString)$@"
                   SELECT TRUNC(CNTR_TIME_OUT,'HH24') as Hour,
                          SUM(CASE WHEN SUBSTR(CNTR_ISO,1,2)='40' THEN 2 ELSE 1 END) as Teus
                   FROM V_CNTRS
                   WHERE CNTR_OB_VISIT_ID IS NOT NULL
                     AND CNTR_TIME_OUT BETWEEN {startDate} AND {endDate}
                   GROUP BY TRUNC(CNTR_TIME_OUT,'HH24')",
               default);

           // Merge results into VesselPlan dictionary using counters
           var temp = new Dictionary<DateTime,(int InTeus,int OutTeus)>();

           foreach (var entry in ins)
           {
               if (!temp.ContainsKey(entry.Hour))
                   temp[entry.Hour] = (entry.Teus, 0);
               else
                   temp[entry.Hour] = (temp[entry.Hour].InTeus + entry.Teus, temp[entry.Hour].OutTeus);
           }
           foreach (var entry in outs)
           {
               if (!temp.ContainsKey(entry.Hour))
                   temp[entry.Hour] = (0, entry.Teus);
               else
                   temp[entry.Hour] = (temp[entry.Hour].InTeus, temp[entry.Hour].OutTeus + entry.Teus);
           }

           foreach (var kv in temp)
           {
               result[kv.Key] = new VesselPlan(kv.Key, kv.Value.InTeus, kv.Value.OutTeus);
           }
           // Fill missing hours with zero TEU entries
           for (var h = startDate; h <= endDate; h = h.AddHours(1))
           {
               if (!result.ContainsKey(h))
                   result[h] = new VesselPlan(h, 0, 0);
           }

           return result;
        }

        /// <summary>
        /// Fetch rail plans from base tables used in V_OPER_REEFER_PAINEL_RAIL_IN_PENDING
        /// (TOSBRIDGE.TOS_CNTRS, TOSBRIDGE.TOS_TRAIN_VISIT, TCPOPER.CADASTRO_CONTEINER, etc.)
        /// </summary>
        public async Task<Dictionary<DateTime, RailPlan>> FetchRailPlansAsync(DateTime startDate, DateTime endDate)
        {
            var result = new Dictionary<DateTime, RailPlan>();

            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == "CTOS OPS");

            var railIns = await _repo.GetFromSqlAsync<DateTime>(
                env.ConnectionString,
                (FormattableString)$@"
                    SELECT CNTR_YARD_TIME_IN
                    FROM TOSBRIDGE.TOS_CNTRS
                    WHERE CNTR_IB_TYPE IN ('R','U')
                      AND CNTR_STATUS = 'YA'
                      AND CNTR_YARD_TIME_IN BETWEEN {startDate} AND {endDate}",
                default);

            var railOuts = await _repo.GetFromSqlAsync<DateTime>(
                env.ConnectionString,
                (FormattableString)$@"
                    SELECT EVENT_DATE
                    FROM TOSBRIDGE.TOS_CNTR_EVENTS
                    WHERE EVENT_TYPE_CODE = 'UNIT_YARD_MOVE'
                      AND EVENT_DATE BETWEEN {startDate} AND {endDate}",
                default);

            for (var hour = startDate; hour <= endDate; hour = hour.AddHours(1))
            {
                var inCount = railIns.Count(t => t >= hour && t < hour.AddHours(1));
                var outCount = railOuts.Count(t => t >= hour && t < hour.AddHours(1));
                result[hour] = new RailPlan(hour, inCount, outCount);
            }

            return result;
        }
    }
}