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
                   SELECT TRUNC(vv.VESSEL_VISIT_ETB,'HH24') as Hour,
                          SUM(CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) as Teus
                   FROM V_CNTRS c
                   INNER JOIN TOSBRIDGE.TOS_VESSEL_VISIT vv ON c.CNTR_IB_VISIT_ID = vv.VESSEL_VISIT_ID
                   WHERE vv.VESSEL_VISIT_ETB BETWEEN {startDate} AND {endDate}
                   GROUP BY TRUNC(vv.VESSEL_VISIT_ETB,'HH24')",
               default);

           var outs = await _repo.GetFromSqlAsync<HourlyTeu>(
               env.ConnectionString,
               (FormattableString)$@"
                   SELECT TRUNC(vv.VESSEL_VISIT_ETB,'HH24') as Hour,
                          SUM(CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) as Teus
                   FROM V_CNTRS c
                   INNER JOIN TOSBRIDGE.TOS_VESSEL_VISIT vv ON c.CNTR_OB_VISIT_ID = vv.VESSEL_VISIT_ID
                   WHERE vv.VESSEL_VISIT_ETB BETWEEN {startDate} AND {endDate}
                   GROUP BY TRUNC(vv.VESSEL_VISIT_ETB,'HH24')",
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
var railIns = await _repo.GetFromSqlAsync<HourlyTeu>(
    env.ConnectionString,
    (FormattableString)$@"
        SELECT TRUNC(tv.TRAIN_VISIT_ARRIVE,'HH24') as Hour,
               SUM(CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) as Teus
        FROM TOSBRIDGE.TOS_CNTRS c
        INNER JOIN TOSBRIDGE.TOS_TRAIN_VISIT tv ON c.CNTR_IB_VISIT_ID = tv.TRAIN_VISIT_ID
        WHERE tv.TRAIN_VISIT_ARRIVE BETWEEN {startDate} AND {endDate}
        GROUP BY TRUNC(tv.TRAIN_VISIT_ARRIVE,'HH24')",
    default);

var railOuts = await _repo.GetFromSqlAsync<HourlyTeu>(
    env.ConnectionString,
    (FormattableString)$@"
        SELECT TRUNC(tv.TRAIN_VISIT_ARRIVE,'HH24') as Hour,
               SUM(CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) as Teus
        FROM TOSBRIDGE.TOS_CNTRS c
        INNER JOIN TOSBRIDGE.TOS_TRAIN_VISIT tv ON c.CNTR_OB_VISIT_ID = tv.TRAIN_VISIT_ID
        WHERE tv.TRAIN_VISIT_ARRIVE BETWEEN {startDate} AND {endDate}
        GROUP BY TRUNC(tv.TRAIN_VISIT_ARRIVE,'HH24')",
    default);
            // Create dictionaries for easy access
            var railInsDict = railIns.ToDictionary(x => x.Hour, x => x.Teus);
            var railOutsDict = railOuts.ToDictionary(x => x.Hour, x => x.Teus);

            for (var hour = startDate; hour <= endDate; hour = hour.AddHours(1))
            {
                var inTeus = railInsDict.TryGetValue(hour, out var inVal) ? inVal : 0;
                var outTeus = railOutsDict.TryGetValue(hour, out var outVal) ? outVal : 0;
                result[hour] = new RailPlan(hour, inTeus, outTeus);
            }

            return result;
        }

        public async Task<int> GetCurrentYardTeuAsync()
        {
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == "CTOS OPS");
            var teusList = await _repo.GetFromSqlAsync<int>(
                env.ConnectionString,
                (FormattableString)$@"SELECT SUM(CASE WHEN SUBSTR(CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) as TotalTeus FROM V_CNTRS WHERE CNTR_STATUS = 'YA'",
                default);
            return teusList.FirstOrDefault();
        }
    }
}