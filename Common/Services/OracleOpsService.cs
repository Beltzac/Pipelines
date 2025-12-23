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

        private class HourlyInOut
        {
            public DateTime Hour { get; set; }
            public int InCount { get; set; }
            public int OutCount { get; set; }
            public int VesselIn { get; set; }
            public int VesselOut { get; set; }
            public int RailIn { get; set; }
            public int RailOut { get; set; }
            public int OtherIn { get; set; }
            public int OtherOut { get; set; }
        }

        /// <summary>
        /// Fetch vessel plans with vessel names
        /// </summary>
        public async Task<Dictionary<DateTime, VesselPlan>> FetchVesselPlansWithNamesAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<DateTime, VesselPlan>();
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);

            var ins = await _repo.GetFromSqlAsync<HourlyTeu>(
                env.ConnectionString,
                (FormattableString)$@"
            SELECT TRUNC(vv.VESSEL_VISIT_ETB,'HH24') as Hour,
                   SUM(CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) as Teus,
                   MAX(vv.VESSEL_NAME) as Name
             FROM TOSBRIDGE.TOS_CNTRS c
             INNER JOIN TOSBRIDGE.TOS_VESSEL_VISIT vv ON c.CNTR_IB_VISIT_ID = vv.VESSEL_VISIT_ID
             WHERE vv.VESSEL_VISIT_ETB BETWEEN {startDate} AND {endDate}
               AND c.CNTR_CATEGORY != 'H'
             GROUP BY TRUNC(vv.VESSEL_VISIT_ETB,'HH24')",
                cancellationToken);

            var outs = await _repo.GetFromSqlAsync<HourlyTeu>(
                env.ConnectionString,
                (FormattableString)$@"
            SELECT TRUNC(vv.VESSEL_VISIT_ETB,'HH24') as Hour,
                   SUM(CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) as Teus,
                   MAX(vv.VESSEL_NAME) as Name
             FROM TOSBRIDGE.TOS_CNTRS c
             INNER JOIN TOSBRIDGE.TOS_VESSEL_VISIT vv ON c.CNTR_OB_VISIT_ID = vv.VESSEL_VISIT_ID
             WHERE vv.VESSEL_VISIT_ETB BETWEEN {startDate} AND {endDate}
               AND c.CNTR_CATEGORY != 'H'
             GROUP BY TRUNC(vv.VESSEL_VISIT_ETB,'HH24')",
                cancellationToken);

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
        public async Task<Dictionary<DateTime, RailPlan>> FetchRailPlansWithNamesAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<DateTime, RailPlan>();
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);

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

        public async Task<int> GetCurrentYardTeuAsync(string envName, CancellationToken cancellationToken = default)
        {
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);
            var teusList = await _repo.GetFromSqlAsync<int>(
                env.ConnectionString,
                (FormattableString)$@"SELECT SUM(CASE WHEN SUBSTR(CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) as TotalTeus FROM TOSBRIDGE.TOS_CNTRS WHERE CNTR_STATUS = 'YA'",
                default);
            return teusList.FirstOrDefault();
        }


        public async Task<Dictionary<DateTime, Common.Services.Interfaces.InOut>> FetchGateTrucksAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<DateTime, Common.Services.Interfaces.InOut>();
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);

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

        public async Task<Dictionary<DateTime, int>> FetchYardMovesAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<DateTime, int>();
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);

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


        public async Task<Common.Services.Interfaces.LoadUnloadRate> GetVesselLoadUnloadRatesAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default)
        {
            var actualStartDate = startDate;
            var actualEndDate = endDate;
            if (actualStartDate > DateTime.Now)
            {
                actualEndDate = DateTime.Now;
                actualStartDate = actualEndDate.AddDays(-30);
            }

            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);

            var vesselRate = await _repo.GetFromSqlAsync<Common.Services.Interfaces.LoadUnloadRate>(
                env.ConnectionString,
                (FormattableString)$@"
                SELECT
                   'Average Vessel' as NAME,
                   NVL(AVG(LoadTeus), 0) as TOTAL_LOAD_TEUS,
                   NVL(AVG(UnloadTeus), 0) as TOTAL_UNLOAD_TEUS,
                   NVL(AVG(LoadDurationHours + UnloadDurationHours), 0) as TOTAL_DURATION_HOURS,
                   CASE WHEN AVG(LoadDurationHours) > 0 THEN AVG(LoadTeus) / AVG(LoadDurationHours) ELSE 0 END as LOAD_RATE_TEUS_PER_HOUR,
                   CASE WHEN AVG(UnloadDurationHours) > 0 THEN AVG(UnloadTeus) / AVG(UnloadDurationHours) ELSE 0 END as UNLOAD_RATE_TEUS_PER_HOUR
                FROM (
                   SELECT vv.VESSEL_VISIT_ID,
                          SUM(CASE WHEN m.MOV_FROM_TYPE = 'V' AND c.CNTR_CATEGORY != 'H' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as UnloadTeus,
                          SUM(CASE WHEN m.MOV_TO_TYPE = 'V' AND c.CNTR_CATEGORY != 'H' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as LoadTeus,
                          NVL((MAX(CASE WHEN m.MOV_TO_TYPE = 'V' THEN m.MOV_TIME_PUT END) - MIN(CASE WHEN m.MOV_TO_TYPE = 'V' THEN m.MOV_TIME_PUT END)) * 24, 0) as LoadDurationHours,
                          NVL((MAX(CASE WHEN m.MOV_FROM_TYPE = 'V' THEN m.MOV_TIME_PUT END) - MIN(CASE WHEN m.MOV_FROM_TYPE = 'V' THEN m.MOV_TIME_PUT END)) * 24, 0) as UnloadDurationHours
                   FROM TOSBRIDGE.TOS_VESSEL_VISIT vv
                   JOIN TOSBRIDGE.TOS_CNTR_MOV m ON m.VISIT_ID = vv.VESSEL_VISIT_ID
                   JOIN TOSBRIDGE.TOS_CNTRS c ON m.CNTR_ID = c.CNTR_ID
                   WHERE vv.VESSEL_VISIT_ETB BETWEEN {actualStartDate} AND {actualEndDate}
                   GROUP BY vv.VESSEL_VISIT_ID
                )",
               default);

            if (vesselRate != null && vesselRate.Any())
            {
                return vesselRate.First();
            }

            return new Common.Services.Interfaces.LoadUnloadRate { Name = "Average Vessel" };
        }
        public async Task<Common.Services.Interfaces.LoadUnloadRate> GetTrainLoadUnloadRatesAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default)
        {
            var actualStartDate = startDate;
            var actualEndDate = endDate;
            if (actualStartDate > DateTime.Now)
            {
                actualEndDate = DateTime.Now;
                actualStartDate = actualEndDate.AddDays(-30);
            }

            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);

            var trainRate = await _repo.GetFromSqlAsync<Common.Services.Interfaces.LoadUnloadRate>(
                env.ConnectionString,
                (FormattableString)$@"
                SELECT
                   'Average Train' as NAME,
                   NVL(AVG(LoadTeus), 0) as TOTAL_LOAD_TEUS,
                   NVL(AVG(UnloadTeus), 0) as TOTAL_UNLOAD_TEUS,
                   NVL(AVG(LoadDurationHours + UnloadDurationHours), 0) as TOTAL_DURATION_HOURS,
                   CASE WHEN AVG(LoadDurationHours) > 0 THEN AVG(LoadTeus) / AVG(LoadDurationHours) ELSE 0 END as LOAD_RATE_TEUS_PER_HOUR,
                   CASE WHEN AVG(UnloadDurationHours) > 0 THEN AVG(UnloadTeus) / AVG(UnloadDurationHours) ELSE 0 END as UNLOAD_RATE_TEUS_PER_HOUR
                FROM (
                   SELECT tv.TRAIN_VISIT_ID,
                          SUM(CASE WHEN m.MOV_FROM_TYPE = 'R' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as UnloadTeus,
                          SUM(CASE WHEN m.MOV_TO_TYPE = 'R' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as LoadTeus,
                          NVL((MAX(CASE WHEN m.MOV_TO_TYPE = 'R' THEN m.MOV_TIME_PUT END) - MIN(CASE WHEN m.MOV_TO_TYPE = 'R' THEN m.MOV_TIME_PUT END)) * 24, 0) as LoadDurationHours,
                          NVL((MAX(CASE WHEN m.MOV_FROM_TYPE = 'R' THEN m.MOV_TIME_PUT END) - MIN(CASE WHEN m.MOV_FROM_TYPE = 'R' THEN m.MOV_TIME_PUT END)) * 24, 0) as UnloadDurationHours
                   FROM TOSBRIDGE.TOS_TRAIN_VISIT tv
                   JOIN TOSBRIDGE.TOS_CNTR_MOV m ON m.VISIT_ID = tv.TRAIN_VISIT_ID
                   JOIN TOSBRIDGE.TOS_CNTRS c ON m.CNTR_ID = c.CNTR_ID
                   WHERE tv.TRAIN_VISIT_ARRIVE BETWEEN {actualStartDate} AND {actualEndDate}
                   GROUP BY tv.TRAIN_VISIT_ID
                )",
               default);

            if (trainRate != null && trainRate.Any())
            {
                return trainRate.First();
            }

            return new Common.Services.Interfaces.LoadUnloadRate { Name = "Average Train" };
        }

        public async Task<int> GetHistoricalYardTeuAsync(DateTime targetDate, string envName, CancellationToken cancellationToken = default)
        {
            var currentTeu = await GetCurrentYardTeuAsync(envName, cancellationToken);

            if (targetDate > DateTime.Now) return currentTeu;

            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);

            // Calculate moves from targetDate to NOW to reverse-engineer inventory
            // Historical = Current - (In - Out) = Current - In + Out
            // In: Moved INTO Yard (To=Y, From!=Y)
            // Out: Moved OUT OF Yard (From=Y, To!=Y)
            // Note: We use LoadUnloadRate just as a container for sums
            var moves = await _repo.GetFromSqlAsync<Common.Services.Interfaces.LoadUnloadRate>(
                env.ConnectionString,
                (FormattableString)$@"
                SELECT
                  'Moves' as NAME,
                  SUM(CASE WHEN m.MOV_TO_TYPE = 'Y' AND m.MOV_FROM_TYPE != 'Y' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as TOTAL_LOAD_TEUS,
                  SUM(CASE WHEN m.MOV_FROM_TYPE = 'Y' AND m.MOV_TO_TYPE != 'Y' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as TOTAL_UNLOAD_TEUS,
                  0 as TOTAL_DURATION_HOURS, 0 as LOAD_RATE_TEUS_PER_HOUR, 0 as UNLOAD_RATE_TEUS_PER_HOUR
                FROM TOSBRIDGE.TOS_CNTR_MOV m
                INNER JOIN TOSBRIDGE.TOS_CNTRS c ON m.CNTR_ID = c.CNTR_ID
                WHERE m.MOV_TIME_PUT >= {targetDate}
                ",
                cancellationToken);

            var diff = moves.FirstOrDefault();
            if (diff != null)
            {
                var inSinceThen = diff.TotalLoadTeus;
                var outSinceThen = diff.TotalUnloadTeus;
                return currentTeu - inSinceThen + outSinceThen;
            }

            return currentTeu;
        }

        public async Task<Dictionary<DateTime, Common.Services.Interfaces.InOut>> FetchActualGateTrucksAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default)

        {

            var result = new Dictionary<DateTime, Common.Services.Interfaces.InOut>();

            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);



            var data = await _repo.GetFromSqlAsync<HourlyInOut>(

                env.ConnectionString,

                (FormattableString)$@"

                                SELECT

                                    TRUNC(m.MOV_TIME_PUT, 'HH24') as Hour,

                                    SUM(CASE WHEN m.MOV_TO_TYPE = 'Y' AND m.MOV_FROM_TYPE = 'T' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as IN_COUNT,
                                    SUM(CASE WHEN m.MOV_FROM_TYPE = 'Y' AND m.MOV_TO_TYPE = 'T' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as OUT_COUNT,
                                    SUM(CASE WHEN m.MOV_TO_TYPE = 'Y' AND m.MOV_FROM_TYPE = 'V' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as VESSEL_IN,
                                    SUM(CASE WHEN m.MOV_FROM_TYPE = 'Y' AND m.MOV_TO_TYPE = 'V' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as VESSEL_OUT,
                                    SUM(CASE WHEN m.MOV_TO_TYPE = 'Y' AND m.MOV_FROM_TYPE = 'R' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as RAIL_IN,
                                    SUM(CASE WHEN m.MOV_FROM_TYPE = 'Y' AND m.MOV_TO_TYPE = 'R' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as RAIL_OUT,
                                    SUM(CASE WHEN m.MOV_TO_TYPE = 'Y' AND m.MOV_FROM_TYPE NOT IN ('Y', 'T', 'V', 'R') THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as OTHER_IN,
                                    SUM(CASE WHEN m.MOV_FROM_TYPE = 'Y' AND m.MOV_TO_TYPE NOT IN ('Y', 'T', 'V', 'R') THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END) as OTHER_OUT

                                FROM TOSBRIDGE.TOS_CNTR_MOV m
                                INNER JOIN TOSBRIDGE.TOS_CNTRS c ON m.CNTR_ID = c.CNTR_ID

                                WHERE m.MOV_TIME_PUT BETWEEN {startDate} AND {endDate}

                                  AND ( (m.MOV_FROM_TYPE != 'Y' AND m.MOV_TO_TYPE = 'Y') OR (m.MOV_FROM_TYPE = 'Y' AND m.MOV_TO_TYPE != 'Y') )

                                GROUP BY TRUNC(m.MOV_TIME_PUT, 'HH24')",

                cancellationToken);



            foreach (var item in data)

            {

                result[item.Hour] = new Common.Services.Interfaces.InOut

                {

                    In = item.InCount,

                    Out = item.OutCount,

                    VesselIn = item.VesselIn,

                    VesselOut = item.VesselOut,

                    RailIn = item.RailIn,

                    RailOut = item.RailOut,

                    OtherIn = item.OtherIn,

                    OtherOut = item.OtherOut

                };

            }



            // Fill missing hours

            for (var h = startDate; h <= endDate; h = h.AddHours(1))

            {

                var k = new DateTime(h.Year, h.Month, h.Day, h.Hour, 0, 0); // Normalize

                if (!result.ContainsKey(k))

                    result[k] = new Common.Services.Interfaces.InOut { In = 0, Out = 0, OtherIn = 0, OtherOut = 0 };

            }



            return result;

        }
        public async Task<Dictionary<DateTime, int>> FetchActualYardInventoryHistoryAsync(DateTime startDate, DateTime endDate, int initialInventory, string envName, CancellationToken cancellationToken = default)
        {
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);

            // Calculate Net Change per hour
            var changes = await _repo.GetFromSqlAsync<HourlyTeu>(
                env.ConnectionString,
                (FormattableString)$@"
                SELECT
                    TRUNC(m.MOV_TIME_PUT, 'HH24') as Hour,
                    SUM(
                    (CASE WHEN m.MOV_TO_TYPE = 'Y' AND m.MOV_FROM_TYPE != 'Y' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END)
                    -
                    (CASE WHEN m.MOV_FROM_TYPE = 'Y' AND m.MOV_TO_TYPE != 'Y' THEN (CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) ELSE 0 END)
                    ) as Teus,
                    'Net' as Name
                FROM TOSBRIDGE.TOS_CNTR_MOV m
                INNER JOIN TOSBRIDGE.TOS_CNTRS c ON m.CNTR_ID = c.CNTR_ID
                WHERE m.MOV_TIME_PUT BETWEEN {startDate} AND {endDate}
                  AND ( (m.MOV_FROM_TYPE != 'Y' AND m.MOV_TO_TYPE = 'Y') OR (m.MOV_FROM_TYPE = 'Y' AND m.MOV_TO_TYPE != 'Y') )
                GROUP BY TRUNC(m.MOV_TIME_PUT, 'HH24')
                ",
                cancellationToken);

            var result = new Dictionary<DateTime, int>();
            int current = initialInventory;

            var changeDict = changes.ToDictionary(x => x.Hour, x => x.Teus);

            for (var t = startDate; t <= endDate; t = t.AddHours(1))
            {
                if (t > DateTime.Now) break;

                // Apply changes that happened IN this hour to reflect inventory at end of hour (or start of next?)
                // Usually Inventory(t) is state at t.
                // Moves(t) are moves happening between t and t+1.
                // So Inventory(t+1) = Inventory(t) + Moves(t).

                // Let's assume result[t] is Inventory AT t.
                result[t] = current;

                if (changeDict.TryGetValue(t, out int net))
                {
                    current += net;
                }
            }
            // Add one more point for the end? Or just cover the range.
            // If EndDate is inclusive hour, we usually want the state at that hour.

            return result;
        }

        private class AvgResult
        {
            public double AvgValue { get; set; }
        }

        public async Task<double> GetAvgTeuPerTruckAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default)
        {
            var actualStartDate = startDate;
            var actualEndDate = endDate;
            if (actualStartDate > DateTime.Now)
            {
                actualEndDate = DateTime.Now;
                actualStartDate = actualEndDate.AddDays(-30);
            }

            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);

            var result = await _repo.GetFromSqlAsync<AvgResult>(
                env.ConnectionString,
                (FormattableString)$@"
                SELECT
                    CASE WHEN COUNT(DISTINCT tv.TRUCK_VISIT_ID) > 0
                         THEN SUM(CASE WHEN SUBSTR(c.CNTR_ISO,1,1)='4' THEN 2 ELSE 1 END) / COUNT(DISTINCT tv.TRUCK_VISIT_ID)
                         ELSE 0 END as AvgValue
                FROM TCPAPI.V_TRUCK_VISIT tv
                INNER JOIN TOSBRIDGE.TOS_CNTRS c ON c.CNTR_IB_VISIT_ID = tv.TRUCK_VISIT_ID OR c.CNTR_OB_VISIT_ID = tv.TRUCK_VISIT_ID
                WHERE tv.TRUCK_VISIT_TIME_IN BETWEEN {actualStartDate} AND {actualEndDate}",
                cancellationToken);

            return result.FirstOrDefault()?.AvgValue ?? 2.5;
        }

        public async Task<List<VesselSchedule>> FetchVesselSchedulesAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default)
        {
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);
            return await _repo.GetFromSqlAsync<VesselSchedule>(
                env.ConnectionString,
                (FormattableString)$@"
                SELECT VESSEL_NAME as VESSEL_NAME,
                       NVL(VESSEL_VISIT_START_WORK, VESSEL_VISIT_ETB) as START_WORK,
                       NVL(VESSEL_VISIT_END_WORK, VESSEL_VISIT_ETD) as END_WORK
                FROM TOSBRIDGE.TOS_VESSEL_VISIT
                WHERE (VESSEL_VISIT_ETB BETWEEN {startDate} AND {endDate}
                   OR VESSEL_VISIT_ETD BETWEEN {startDate} AND {endDate}
                   OR VESSEL_VISIT_START_WORK BETWEEN {startDate} AND {endDate}
                   OR VESSEL_VISIT_END_WORK BETWEEN {startDate} AND {endDate})
                  AND VESSEL_NAME IS NOT NULL",
                cancellationToken);
        }

        public async Task<List<RailSchedule>> FetchRailSchedulesAsync(DateTime startDate, DateTime endDate, string envName, CancellationToken cancellationToken = default)
        {
            var env = _config.GetConfig().OracleEnvironments.First(e => e.Name == envName);
            return await _repo.GetFromSqlAsync<RailSchedule>(
                env.ConnectionString,
                (FormattableString)$@"
                SELECT TRAIN_VISIT_NAME as TRAIN_NAME,
                       NVL(TRAIN_VISIT_START_WORK, TRAIN_VISIT_ARRIVE) as START_WORK,
                       NVL(TRAIN_VISIT_END_WORK, TRAIN_VISIT_DEPART) as END_WORK
                FROM TOSBRIDGE.TOS_TRAIN_VISIT
                WHERE (TRAIN_VISIT_ARRIVE BETWEEN {startDate} AND {endDate}
                   OR TRAIN_VISIT_DEPART BETWEEN {startDate} AND {endDate}
                   OR TRAIN_VISIT_START_WORK BETWEEN {startDate} AND {endDate}
                   OR TRAIN_VISIT_END_WORK BETWEEN {startDate} AND {endDate})
                  AND TRAIN_VISIT_NAME IS NOT NULL",
                cancellationToken);
        }
    }
}
