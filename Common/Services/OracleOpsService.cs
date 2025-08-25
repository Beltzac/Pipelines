using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Models;
using Common.Repositories.TCP.Interfaces;
using Common.Services.Interfaces;

namespace Common.Services
{
    public class OracleOpsService : IOracleOpsService
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
            public string Name { get; set; } = string.Empty;
        }

        private class HourlyCount
        {
            public int Weekday { get; set; }
            public int HourOfDay { get; set; }
            public int Count { get; set; }
        }

        /// <summary>
        /// Fetch vessel plans with vessel names
        /// </summary>
        public async Task<Dictionary<DateTime, VesselPlan>> FetchVesselPlansWithNamesAsync(DateTime startDate, DateTime endDate)
        {
            var result = new Dictionary<DateTime, VesselPlan>();
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == "CTOS OPS");

            var ins = await _repo.GetFromSqlAsync<HourlyTeu>(
                env.ConnectionString,
                (FormattableString)$@"
            SELECT TRUNC(vv.VESSEL_VISIT_ETB,'HH24') as Hour,
                   SUM(CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) as Teus,
                   MAX(vv.VESSEL_NAME) as Name
             FROM V_CNTRS c
             INNER JOIN TOSBRIDGE.TOS_VESSEL_VISIT vv ON c.CNTR_IB_VISIT_ID = vv.VESSEL_VISIT_ID
             WHERE vv.VESSEL_VISIT_ETB BETWEEN {startDate} AND {endDate}
               AND c.CNTR_CATEGORY != 'H'
             GROUP BY TRUNC(vv.VESSEL_VISIT_ETB,'HH24')",
                default);

            var outs = await _repo.GetFromSqlAsync<HourlyTeu>(
                env.ConnectionString,
                (FormattableString)$@"
            SELECT TRUNC(vv.VESSEL_VISIT_ETB,'HH24') as Hour,
                   SUM(CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) as Teus,
                   MAX(vv.VESSEL_NAME) as Name
             FROM V_CNTRS c
             INNER JOIN TOSBRIDGE.TOS_VESSEL_VISIT vv ON c.CNTR_OB_VISIT_ID = vv.VESSEL_VISIT_ID
             WHERE vv.VESSEL_VISIT_ETB BETWEEN {startDate} AND {endDate}
               AND c.CNTR_CATEGORY != 'H'
             GROUP BY TRUNC(vv.VESSEL_VISIT_ETB,'HH24')",
                default);

            var temp = new Dictionary<DateTime, (int InTeus, int OutTeus, List<string> InNames, List<string> OutNames)>();

            foreach (var entry in ins)
            {
                if (!temp.ContainsKey(entry.Hour))
                    temp[entry.Hour] = (entry.Teus, 0, new List<string> { entry.Name }, new List<string>());
                else
                {
                    var t = temp[entry.Hour];
                    t.InTeus += entry.Teus;
                    t.InNames.Add(entry.Name);
                    temp[entry.Hour] = t;
                }
            }
            foreach (var entry in outs)
            {
                if (!temp.ContainsKey(entry.Hour))
                    temp[entry.Hour] = (0, entry.Teus, new List<string>(), new List<string> { entry.Name });
                else
                {
                    var t = temp[entry.Hour];
                    t.OutTeus += entry.Teus;
                    t.OutNames.Add(entry.Name);
                    temp[entry.Hour] = t;
                }
            }

            foreach (var kv in temp)
            {
                result[kv.Key] = new VesselPlan(kv.Key, kv.Value.InTeus, kv.Value.OutTeus, kv.Value.InNames.Concat(kv.Value.OutNames).Distinct().ToList());
            }

            for (var h = startDate; h <= endDate; h = h.AddHours(1))
            {
                if (!result.ContainsKey(h))
                    result[h] = new VesselPlan(h, 0, 0, new List<string>());
            }

            return result;
        }

        /// <summary>
        /// Fetch rail plans with names
        /// </summary>
        public async Task<Dictionary<DateTime, RailPlan>> FetchRailPlansWithNamesAsync(DateTime startDate, DateTime endDate)
        {
            var result = new Dictionary<DateTime, RailPlan>();
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == "CTOS OPS");

            var railIns = await _repo.GetFromSqlAsync<HourlyTeu>(
                env.ConnectionString,
                (FormattableString)$@"
        SELECT TRUNC(tv.TRAIN_VISIT_ARRIVE,'HH24') as Hour,
               SUM(CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) as Teus,
               MAX(tv.TRAIN_VISIT_NAME) as Name
        FROM TOSBRIDGE.TOS_CNTRS c
        INNER JOIN TOSBRIDGE.TOS_TRAIN_VISIT tv ON c.CNTR_IB_VISIT_ID = tv.TRAIN_VISIT_ID
        WHERE tv.TRAIN_VISIT_ARRIVE BETWEEN {startDate} AND {endDate}
        GROUP BY TRUNC(tv.TRAIN_VISIT_ARRIVE,'HH24')",
                default);

            var railOuts = await _repo.GetFromSqlAsync<HourlyTeu>(
                env.ConnectionString,
                (FormattableString)$@"
        SELECT TRUNC(tv.TRAIN_VISIT_ARRIVE,'HH24') as Hour,
               SUM(CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) as Teus,
               MAX(tv.TRAIN_VISIT_NAME) as Name
        FROM TOSBRIDGE.TOS_CNTRS c
        INNER JOIN TOSBRIDGE.TOS_TRAIN_VISIT tv ON c.CNTR_OB_VISIT_ID = tv.TRAIN_VISIT_ID
        WHERE tv.TRAIN_VISIT_ARRIVE BETWEEN {startDate} AND {endDate}
        GROUP BY TRUNC(tv.TRAIN_VISIT_ARRIVE,'HH24')",
                default);

            var temp = new Dictionary<DateTime, (int InTeus, int OutTeus, List<string> InNames, List<string> OutNames)>();

            foreach (var entry in railIns)
            {
                if (!temp.ContainsKey(entry.Hour))
                    temp[entry.Hour] = (entry.Teus, 0, new List<string> { entry.Name }, new List<string>());
                else
                {
                    var t = temp[entry.Hour];
                    t.InTeus += entry.Teus;
                    t.InNames.Add(entry.Name);
                    temp[entry.Hour] = t;
                }
            }
            foreach (var entry in railOuts)
            {
                if (!temp.ContainsKey(entry.Hour))
                    temp[entry.Hour] = (0, entry.Teus, new List<string>(), new List<string> { entry.Name });
                else
                {
                    var t = temp[entry.Hour];
                    t.OutTeus += entry.Teus;
                    t.OutNames.Add(entry.Name);
                    temp[entry.Hour] = t;
                }
            }

            for (var hour = startDate; hour <= endDate; hour = hour.AddHours(1))
            {
                if (temp.TryGetValue(hour, out var t))
                {
                    result[hour] = new RailPlan(hour, t.InTeus, t.OutTeus, t.InNames.Concat(t.OutNames).Distinct().ToList());
                }
                else
                {
                    result[hour] = new RailPlan(hour, 0, 0, new List<string>());
                }
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


        public async Task<Dictionary<DateTime, Common.Services.Interfaces.InOut>> FetchGateTrucksAsync(DateTime startDate, DateTime endDate)
        {
            var result = new Dictionary<DateTime, Common.Services.Interfaces.InOut>();
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == "CTOS OPS");

            var trucksInCap = await _repo.GetFromSqlAsync<HourlyCount>(
                env.ConnectionString,
                (FormattableString)$@"
                    SELECT TO_NUMBER(TO_CHAR(DATA_ENTRADA_GATE, 'D')) - 1 AS WEEKDAY,
                            TO_NUMBER(TO_CHAR(DATA_ENTRADA_GATE, 'HH24')) AS HOUR_OF_DAY,
                            TRUNC(COUNT(*) / COUNT(DISTINCT TRUNC(DATA_ENTRADA_GATE))) AS COUNT
                    FROM TOSBRIDGE.V_RPT_MOVIMENTO_GATES_CNTR
                    WHERE DATA_ENTRADA_GATE BETWEEN {startDate.AddYears(-1)} AND {endDate}
                    GROUP BY TO_CHAR(DATA_ENTRADA_GATE, 'D'), TO_CHAR(DATA_ENTRADA_GATE, 'HH24')",
                default);

            var trucksOutCap = await _repo.GetFromSqlAsync<HourlyCount>(
                env.ConnectionString,
                (FormattableString)$@"
                    SELECT TO_NUMBER(TO_CHAR(DATA_SAIDA_GATE, 'D')) - 1 AS WEEKDAY,
                            TO_NUMBER(TO_CHAR(DATA_SAIDA_GATE, 'HH24')) AS HOUR_OF_DAY,
                            TRUNC(COUNT(*) / COUNT(DISTINCT TRUNC(DATA_SAIDA_GATE))) AS COUNT
                    FROM TOSBRIDGE.V_RPT_MOVIMENTO_GATES_CNTR
                    WHERE DATA_SAIDA_GATE BETWEEN {startDate.AddYears(-1)} AND {endDate}
                    GROUP BY TO_CHAR(DATA_SAIDA_GATE, 'D'), TO_CHAR(DATA_SAIDA_GATE, 'HH24')",
                default);

            // Map result capacity as maximum avg per weekday-hour slot
            // Map each requested hour to the matching weekday-hour slot
            for (var h = startDate; h <= endDate; h = h.AddHours(1))
            {
                var weekday = (int)h.DayOfWeek; // Sunday=0
                var hourKey = h.Hour;
                var inCap = trucksInCap.Where(x => x.Weekday == (int)h.DayOfWeek && x.HourOfDay == hourKey).Select(x => x.Count).DefaultIfEmpty(0).Max();
                var outCap = trucksOutCap.Where(x => x.Weekday == (int)h.DayOfWeek && x.HourOfDay == hourKey).Select(x => x.Count).DefaultIfEmpty(0).Max();
                result[h] = new Common.Services.Interfaces.InOut { In = inCap, Out = outCap };
            }

            return result;
        }

        public async Task<Dictionary<DateTime, int>> FetchYardMovesAsync(DateTime startDate, DateTime endDate)
        {
            var result = new Dictionary<DateTime, int>();
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == "CTOS OPS");

            var yardMovesCap = await _repo.GetFromSqlAsync<HourlyCount>(
                env.ConnectionString,
                (FormattableString)$@"
                    SELECT TO_NUMBER(TO_CHAR(MOV_TIME_PUT, 'D')) - 1 as WEEKDAY,
                            TO_NUMBER(TO_CHAR(MOV_TIME_PUT, 'HH24')) as HOUR_OF_DAY,
                            COUNT(*) / COUNT(DISTINCT TRUNC(MOV_TIME_PUT)) as COUNT
                    FROM TOSBRIDGE.TOS_CNTR_MOV
                    WHERE MOV_TIME_PUT BETWEEN {startDate.AddYears(-1)} AND {endDate}
                    GROUP BY TO_CHAR(MOV_TIME_PUT, 'D'), TO_CHAR(MOV_TIME_PUT, 'HH24')",
                default);

            for (var h = startDate; h <= endDate; h = h.AddHours(1))
            {
                var weekday = (int)h.DayOfWeek;
                var hourKey = h.Hour;
                var cap = yardMovesCap.Where(x => x.Weekday == (int)h.DayOfWeek && x.HourOfDay == hourKey).Select(x => x.Count).DefaultIfEmpty(0).Max();
                result[h] = cap;
            }
            return result;
        }


        /// <summary>
        /// Get vessel loading/unloading rates from the last year
        /// </summary>
        public async Task<Common.Services.Interfaces.LoadUnloadRate> GetVesselLoadUnloadRatesAsync()
        {
            var endDate = DateTime.Now;
            var startDate = endDate.AddYears(-1);
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == "CTOS OPS");

            var vesselRate = await _repo.GetFromSqlAsync<Common.Services.Interfaces.LoadUnloadRate>(
                env.ConnectionString,
                (FormattableString)$@"
                SELECT
                   'Average Vessel' as NAME,
                   CAST(AVG(LoadTeus) AS NUMBER) as TOTAL_LOAD_TEUS,
                   CAST(AVG(UnloadTeus) AS NUMBER) as TOTAL_UNLOAD_TEUS,
                   CAST(AVG(DurationHours) AS NUMBER) as TOTAL_DURATION_HOURS,
                   CASE WHEN CAST(AVG(DurationHours) AS NUMBER) > 0 THEN CAST(AVG(LoadTeus) AS NUMBER) / CAST(AVG(DurationHours) AS NUMBER) ELSE 0 END as LOAD_RATE_TEUS_PER_HOUR,
                   CASE WHEN CAST(AVG(DurationHours) AS NUMBER) > 0 THEN CAST(AVG(UnloadTeus) AS NUMBER) / CAST(AVG(DurationHours) AS NUMBER) ELSE 0 END as UNLOAD_RATE_TEUS_PER_HOUR
                FROM (
                   SELECT vv.VESSEL_VISIT_ID,
                          SUM(CASE WHEN c.CNTR_IB_VISIT_ID = vv.VESSEL_VISIT_ID
                                   AND c.CNTR_CATEGORY != 'H'
                                   THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4'
                                              THEN 2 ELSE 1 END)
                              ELSE 0 END) as LoadTeus,
                          SUM(CASE WHEN c.CNTR_OB_VISIT_ID = vv.VESSEL_VISIT_ID
                                   AND c.CNTR_CATEGORY != 'H'
                                   THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4'
                                              THEN 2 ELSE 1 END)
                              ELSE 0 END) as UnloadTeus,
                          ((vv.VESSEL_VISIT_ETD - vv.VESSEL_VISIT_ETB) * 24) as DurationHours
                   FROM TOSBRIDGE.TOS_VESSEL_VISIT vv
                   LEFT JOIN V_CNTRS c
                          ON c.CNTR_IB_VISIT_ID = vv.VESSEL_VISIT_ID
                          OR c.CNTR_OB_VISIT_ID = vv.VESSEL_VISIT_ID
                   WHERE vv.VESSEL_VISIT_ETB BETWEEN {startDate} AND {endDate}
                   GROUP BY vv.VESSEL_VISIT_ID, vv.VESSEL_VISIT_ETB, vv.VESSEL_VISIT_ETD
                )",
               default);

           if (vesselRate != null && vesselRate.Any())
           {
               return vesselRate.First();
           }

            return new Common.Services.Interfaces.LoadUnloadRate { Name = "Average Vessel" };
        }
        /// <summary>
        /// Get train loading/unloading rates from the last year
        /// </summary>
        public async Task<Common.Services.Interfaces.LoadUnloadRate> GetTrainLoadUnloadRatesAsync()
        {
            var endDate = DateTime.Now;
            var startDate = endDate.AddYears(-1);
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == "CTOS OPS");

            var trainRate = await _repo.GetFromSqlAsync<Common.Services.Interfaces.LoadUnloadRate>(
                env.ConnectionString,
                (FormattableString)$@"
                SELECT
                   'Average Train' as NAME,
                   CAST(AVG(LoadTeus) AS NUMBER) as TOTAL_LOAD_TEUS,
                   CAST(AVG(UnloadTeus) AS NUMBER) as TOTAL_UNLOAD_TEUS,
                   CAST(AVG(DurationHours) AS NUMBER) as TOTAL_DURATION_HOURS,
                   CASE WHEN CAST(AVG(DurationHours) AS NUMBER) > 0 THEN CAST(AVG(LoadTeus) AS NUMBER) / CAST(AVG(DurationHours) AS NUMBER) ELSE 0 END as LOAD_RATE_TEUS_PER_HOUR,
                   CASE WHEN CAST(AVG(DurationHours) AS NUMBER) > 0 THEN CAST(AVG(UnloadTeus) AS NUMBER) / CAST(AVG(DurationHours) AS NUMBER) ELSE 0 END as UNLOAD_RATE_TEUS_PER_HOUR
                FROM (
                   SELECT tv.TRAIN_VISIT_ID,
                          SUM(CASE WHEN c.CNTR_IB_VISIT_ID = tv.TRAIN_VISIT_ID
                                   THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4'
                                              THEN 2 ELSE 1 END)
                              ELSE 0 END) as LoadTeus,
                          SUM(CASE WHEN c.CNTR_OB_VISIT_ID = tv.TRAIN_VISIT_ID
                                   THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4'
                                              THEN 2 ELSE 1 END)
                              ELSE 0 END) as UnloadTeus,
                          ((tv.TRAIN_VISIT_DEPART - tv.TRAIN_VISIT_ARRIVE) * 24) as DurationHours
                   FROM TOSBRIDGE.TOS_TRAIN_VISIT tv
                   LEFT JOIN TOSBRIDGE.TOS_CNTRS c
                          ON c.CNTR_IB_VISIT_ID = tv.TRAIN_VISIT_ID
                          OR c.CNTR_OB_VISIT_ID = tv.TRAIN_VISIT_ID
                   WHERE tv.TRAIN_VISIT_ARRIVE BETWEEN {startDate} AND {endDate}
                   GROUP BY tv.TRAIN_VISIT_ID, tv.TRAIN_VISIT_ARRIVE, tv.TRAIN_VISIT_DEPART
                )",
               default);

           if (trainRate != null && trainRate.Any())
           {
               return trainRate.First();
           }

            return new Common.Services.Interfaces.LoadUnloadRate { Name = "Average Train" };
        }
    }
}