using Common.Services.Interfaces;
using System.Diagnostics;

public record VesselPlan(DateTime T, int DischargeTEU, int LoadTEU, List<string> VesselNames);
public record RailPlan(DateTime T, int InTEU, int OutTEU, List<string> TrainNames);
public record OpsCaps(DateTime T, int GateTrucksInPerHour, int GateTrucksOutPerHour, int YardMovesPerHour, int VesselIn = 0, int VesselOut = 0, int RailIn = 0, int RailOut = 0, int OtherIn = 0, int OtherOut = 0);
public record YardBand(int MinTEU, int TargetTEU, int MaxTEU);

public record DistributeLoadUnload(DateTime T, int DischargeTEU, int LoadTEU);

public record HourWindow(
    DateTime T,
    HourWindowData Real,
    HourWindowData Simulated
);

public record HourWindowData(
    int TotalTeu,

    int TotalTeuOnlyVessel,
    int TotalTeuOnlyRail,
    int TotalTeuOnlyTruck,

    int TeuTruckIn,
    int TeuTruckOut,

    int TeuVesselIn,
    int TeuVesselOut,

    int TeuRailIn,
    int TeuRailOut
);

// Problemas:
//  Yard tem areas definidas pra Vazios / Cheios / Reefers. Teria que rodar o algoritimo pra cada um deles
//  Esses fluxos tem gate e operacao compartilhados, temos que ver o que é fixo ou o que podemos remanejar
//  verificar quantas lanes são entradas e quantas são saida
//  adicionar entradas e saidas para o armazem

public static class SlotCalculator
{
    /// <summary>
    /// Computes per-hour appointment slots (Total / IN / OUT) and per-class split for a horizon.
    /// - Clients will pick from the published buckets.
    /// - Yard health is steered toward YardBand.TargetTEU without exceeding YardBand.MaxTEU.
    /// </summary>
    /// <param name="start">Inclusive start (aligned to hour)</param>
    /// <param name="end">Inclusive end (aligned to hour)</param>
    /// <param name="initialYardTeu">O0 at (start - 1h)</param>
    /// <param name="vessels">Vessel plan per hour</param>
    /// <param name="rails">Rail plan per hour</param>
    /// <param name="caps">Ops caps per hour</param>
    /// <param name="band">Yard band (min/target/max TEU)</param>
    /// <param name="avgTeuPerTruck">Average TEU handled per truck (≈1.3–1.6)</param>
    /// Function: per-class max trucks allowed at hour t (e.g., reefers/DG/block windows).
    /// If none, return empty dict.
    /// </param>
    /// <param name="classWeights">
    /// Function: relative weights for classes (larger = higher share). If none, return 1.
    /// </param>
    public static IEnumerable<HourWindow> ComputeHourWindows(
        DateTime start,
        DateTime end,
        int initialYardTeu,
        Dictionary<DateTime, VesselPlan> vessels,
        Dictionary<DateTime, RailPlan> rails,
        Dictionary<DateTime, OpsCaps> caps,
        Dictionary<DateTime, InOut> actualFlows,
        YardBand band,
        double avgTeuPerTruck,
        double reserveRho,
        double easingStrength,
        double vesselLoadRate,
        double vesselUnloadRate,
        double railLoadRate,
        double railUnloadRate,
        double vesselLagHours = 0,
        double railLagHours = 0
    )
    {
        if (avgTeuPerTruck <= 0) throw new ArgumentOutOfRangeException(nameof(avgTeuPerTruck));
        if (reserveRho < 0 || reserveRho >= 1) throw new ArgumentOutOfRangeException(nameof(reserveRho));

        // Assert: Simulation plan (vessels/rails) should be based on estimated values (ETB/ETA)
        // Assert: Real data (actualFlows) should be based on actual values (MOV_TIME_PUT)
        Debug.Assert(vessels != null, "Vessel plan (Estimated) must be provided");
        Debug.Assert(actualFlows != null, "Actual flows (Real) must be provided");

        var now = DateTime.Now;
        var vesselTeus = vessels.ToDictionary(kv => kv.Key, kv => (kv.Value.DischargeTEU, kv.Value.LoadTEU));
        var distributedVesselsSim = DistributeTeusOverTime(vesselTeus, vesselLoadRate, vesselUnloadRate, start, end, vesselLagHours);

        var railTeus = rails.ToDictionary(kv => kv.Key, kv => (kv.Value.InTEU, kv.Value.OutTEU));
        var distributedRailsSim = DistributeTeusOverTime(railTeus, railLoadRate, railUnloadRate, start, end, railLagHours);

        // Track yard state dynamically responding to in/out slots
        var results = new List<HourWindow>();

        int realTeu = initialYardTeu;
        int simTeu = initialYardTeu;

        int simTeuVesselDiff = initialYardTeu;
        int simTeuRailDiff = initialYardTeu;
        int simTeuTruckDiff = initialYardTeu;
        int realTeuVesselDiff = initialYardTeu;
        int realTeuRailDiff = initialYardTeu;
        int realTeuTruckDiff = initialYardTeu;

        // Interpolate missing caps
        // CAPS ARE THE MAXIMUN, NOT THE REAL DATA
        var interpolatedCaps = InterpolateCaps(caps, start, end);

        for (var t = start; t <= end; t = t.AddHours(1))
        {
            var cap = interpolatedCaps[t];

            int gateTeuInCap = (int)Math.Round(cap.GateTrucksInPerHour * avgTeuPerTruck);
            int gateTeuOutCap = (int)Math.Round(cap.GateTrucksOutPerHour * avgTeuPerTruck);

            // Real data processing (Actual values from TOS_CNTR_MOV)
            var flow = actualFlows.TryGetValue(t, out var f) ? f : new InOut();
            
            // Assert: Real data should not exist for future dates in simulation
            if (t > now.AddHours(1))
            {
                Debug.Assert(flow.In == 0 && flow.Out == 0 && flow.VesselIn == 0 && flow.VesselOut == 0, 
                    $"Real data found for future date {t}. Real data must only contain actual values.");
            }

            // Actual flows are already in TEUs from OracleOpsService.FetchActualGateTrucksAsync
            int realTeuTruckIn = flow.In;
            int realTeuTruckOut = flow.Out;
            int realTeuVesselIn = flow.VesselIn;
            int realTeuVesselOut = flow.VesselOut;
            int realTeuRailIn = flow.RailIn;
            int realTeuRailOut = flow.RailOut;

            realTeuTruckDiff += realTeuTruckIn - realTeuTruckOut;
            realTeuVesselDiff += realTeuVesselIn - realTeuVesselOut;
            realTeuRailDiff += realTeuRailIn - realTeuRailOut;

            // Calculate realTeu based on flows
            realTeu += (realTeuTruckIn - realTeuTruckOut) +
                       (realTeuVesselIn - realTeuVesselOut) +
                       (realTeuRailIn - realTeuRailOut) +
                       (flow.OtherIn - flow.OtherOut);

            // Simulation data processing (Estimated values from TOS_VESSEL_VISIT ETB)
            var vesselFlowSim = distributedVesselsSim.TryGetValue(t, out var v) ? v : new DistributeLoadUnload(t, 0, 0);
            var railFlowSim = distributedRailsSim.TryGetValue(t, out var r) ? r : new DistributeLoadUnload(t, 0, 0);

            simTeu += vesselFlowSim.DischargeTEU + railFlowSim.DischargeTEU - vesselFlowSim.LoadTEU - railFlowSim.LoadTEU;

            int diffTEU = band.TargetTEU - simTeu;
            double biasFactor = (band.MaxTEU != band.MinTEU) ? (double)diffTEU / (band.MaxTEU - band.MinTEU) : 0.0;

            var (allocatedTeuIn, allocatedTeuOut) = ApplyEasing(gateTeuInCap + gateTeuOutCap, biasFactor, easingStrength);

            simTeu += allocatedTeuIn - allocatedTeuOut;

            simTeuTruckDiff += allocatedTeuIn - allocatedTeuOut;
            simTeuVesselDiff += vesselFlowSim.DischargeTEU - vesselFlowSim.LoadTEU;
            simTeuRailDiff += railFlowSim.DischargeTEU - railFlowSim.LoadTEU;

            var real = new HourWindowData
            (
                realTeu,
                realTeuVesselDiff,
                realTeuRailDiff,
                realTeuTruckDiff,
                realTeuTruckIn,
                realTeuTruckOut,
                realTeuVesselIn,
                realTeuVesselOut,
                realTeuRailIn,
                realTeuRailOut
            );

            var simulation = new HourWindowData
            (
                simTeu,
                simTeuVesselDiff,
                simTeuRailDiff,
                simTeuTruckDiff,
                allocatedTeuIn,
                allocatedTeuOut,
                vesselFlowSim.DischargeTEU,
                vesselFlowSim.LoadTEU,
                railFlowSim.DischargeTEU,
                railFlowSim.LoadTEU
            );

            // 7. Record results
            results.Add(new HourWindow(
                t,
                real,
                simulation
            ));
        }

        return results;
    }

    private static Dictionary<DateTime, OpsCaps> InterpolateCaps(Dictionary<DateTime, OpsCaps> originalCaps, DateTime start, DateTime end)
    {
        var interpolatedCaps = new Dictionary<DateTime, OpsCaps>();

        // Copy existing caps
        foreach (var kvp in originalCaps)
        {
            interpolatedCaps[kvp.Key] = kvp.Value;
        }

        // Fill missing hours with interpolated values
        for (var t = start; t <= end; t = t.AddHours(1))
        {
            if (!interpolatedCaps.ContainsKey(t))
            {
                // Find previous and next available caps
                OpsCaps? previousCap = null;
                OpsCaps? nextCap = null;

                // Search backwards
                var prevTime = t.AddHours(-1);
                while (prevTime >= start && previousCap == null)
                {
                    if (interpolatedCaps.TryGetValue(prevTime, out var cap))
                    {
                        previousCap = cap;
                    }
                    else
                    {
                        prevTime = prevTime.AddHours(-1);
                    }
                }

                // Search forwards
                var nextTime = t.AddHours(1);
                while (nextTime <= end && nextCap == null)
                {
                    if (interpolatedCaps.TryGetValue(nextTime, out var cap))
                    {
                        nextCap = cap;
                    }
                    else
                    {
                        nextTime = nextTime.AddHours(1);
                    }
                }

                if (previousCap != null && nextCap != null)
                {
                    // Linear interpolation
                    double totalHours = (nextCap.T - previousCap.T).TotalHours;
                    double hoursFromPrevious = (t - previousCap.T).TotalHours;
                    double factor = hoursFromPrevious / totalHours;

                    int gateIn = InterpolateValue(previousCap.GateTrucksInPerHour, nextCap.GateTrucksInPerHour, factor);
                    int gateOut = InterpolateValue(previousCap.GateTrucksOutPerHour, nextCap.GateTrucksOutPerHour, factor);
                    int yardMoves = InterpolateValue(previousCap.YardMovesPerHour, nextCap.YardMovesPerHour, factor);
                    int vesselIn = InterpolateValue(previousCap.VesselIn, nextCap.VesselIn, factor);
                    int vesselOut = InterpolateValue(previousCap.VesselOut, nextCap.VesselOut, factor);
                    int railIn = InterpolateValue(previousCap.RailIn, nextCap.RailIn, factor);
                    int railOut = InterpolateValue(previousCap.RailOut, nextCap.RailOut, factor);
                    int otherIn = InterpolateValue(previousCap.OtherIn, nextCap.OtherIn, factor);
                    int otherOut = InterpolateValue(previousCap.OtherOut, nextCap.OtherOut, factor);

                    interpolatedCaps[t] = new OpsCaps(t, gateIn, gateOut, yardMoves, vesselIn, vesselOut, railIn, railOut, otherIn, otherOut);
                }
                else if (previousCap != null)
                {
                    // Use previous cap values
                    interpolatedCaps[t] = new OpsCaps(t, previousCap.GateTrucksInPerHour, previousCap.GateTrucksOutPerHour, previousCap.YardMovesPerHour, previousCap.VesselIn, previousCap.VesselOut, previousCap.RailIn, previousCap.RailOut, previousCap.OtherIn, previousCap.OtherOut);
                }
                else if (nextCap != null)
                {
                    // Use next cap values
                    interpolatedCaps[t] = new OpsCaps(t, nextCap.GateTrucksInPerHour, nextCap.GateTrucksOutPerHour, nextCap.YardMovesPerHour, nextCap.VesselIn, nextCap.VesselOut, nextCap.RailIn, nextCap.RailOut, nextCap.OtherIn, nextCap.OtherOut);
                }
                else
                {
                    // No caps available, use defaults
                    interpolatedCaps[t] = new OpsCaps(t, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                }
            }
        }

        return interpolatedCaps;
    }

    private static int InterpolateValue(int a, int b, double factor)
    {
        return (int)Math.Round(a + (b - a) * factor);
    }


    /// <summary>
    /// Applies an easing function to distribute remaining slots based on a bias factor.
    /// The bias factor skews allocation towards 'in' or 'out' slots, with adjustable easing strength.
    /// </summary>
    /// <param name="teusToDistribute">The number of slots to distribute.</param>
    /// <param name="biasFactor">A value between -1 (favor out) and +1 (favor in).</param>
    /// <param name="easingStrength">
    /// A value between 0 (linear, no easing) and 1 (full cosine easing).
    /// Values in between blend linear and eased allocation.
    /// </param>
    /// <returns>A tuple containing the allocated 'in' slots and 'out' slots.</returns>
    private static (int inSlots, int outSlots) ApplyEasing(int teusToDistribute, double biasFactor, double easingStrength)
    {
        // Clamp inputs
        biasFactor = Math.Clamp(biasFactor, -1.0, 1.0);
        easingStrength = Math.Clamp(easingStrength, 0.0, 1.0);

        // Convert bias [-1,1] → [0,1]
        double t = (biasFactor + 1.0) / 2.0;

        // Linear share: direct proportion to t
        //double shareInLinear = t;

        // Cosine easing share: smooth nonlinear curve
        double shareInEased = PowerRatio(t, (1 - easingStrength) * 10);

        // Blend linear and eased shares
        double shareIn = shareInEased;
        double shareOut = 1.0 - shareIn;

        int allocIn = (int)Math.Round(teusToDistribute * shareIn);
        int allocOut = teusToDistribute - allocIn; // ensure sum is exact

        return (allocIn, allocOut);
    }

    // 1) Linear (baseline)
    static double Lin(double t) => t;

    // 2) Smoothstep (gentle S-curve)
    static double Smoothstep(double t) => t * t * (3 - 2 * t);

    // 3) Smootherstep (stronger S-curve, flatter ends)
    static double Smootherstep(double t) => t * t * t * (t * (6 * t - 15) + 10);

    // 4) Power-ratio (adjustable steepness; p=1 linear, p>1 steeper)
    static double PowerRatio(double t, double p)
    {
        t = Math.Clamp(t, 0, 1);
        p = Math.Max(1e-6, p);
        double a = Math.Pow(t, p);
        double b = Math.Pow(1 - t, p);
        return a / (a + b);
    }

    // 5) Tanh (sigmoid; k controls steepness, exact 0/1 at ends via normalization)
    static double TanhEase(double t, double k)
    {
        t = Math.Clamp(t, 0, 1);
        k = Math.Max(1e-6, k);
        return 0.5 * (Math.Tanh(k * (2 * t - 1)) / Math.Tanh(k) + 1);
    }

    // 6) Arctan (sigmoid; k controls steepness, normalized to 0/1)
    static double AtanEase(double t, double k)
    {
        t = Math.Clamp(t, 0, 1);
        k = Math.Max(1e-6, k);
        return 0.5 * (Math.Atan(k * (2 * t - 1)) / Math.Atan(k) + 1);
    }

    // 7) Exponential in/out (very aggressive near the middle)
    static double EaseInOutExpo(double t)
    {
        if (t <= 0) return 0;
        if (t >= 1) return 1;
        return t < 0.5
            ? 0.5 * Math.Pow(2, 20 * t - 10)
            : 1 - 0.5 * Math.Pow(2, -20 * t + 10);
    }

    // 8) Cubic (classic easing)
    static double EaseInOutQuad(double t) => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
    static double EaseInOutCubic(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    private static Dictionary<DateTime, DistributeLoadUnload> DistributeTeusOverTime(
        Dictionary<DateTime, (int In, int Out)> hourlyInputs,
        double loadRate, // teus per hour
        double unloadRate, // teus per hour
        DateTime start,
        DateTime end,
        double lagHours = 0
    )
    {
        // Use double for accumulation to avoid rounding errors during distribution
        var tempDistributed = new Dictionary<DateTime, (double Discharge, double Load)>();

        foreach (var entry in hourlyInputs)
        {
            // Precise start time with lag
            // We removed the 30 minutes centering as it was pushing the peaks forward
            var arrivalTime = entry.Key.AddHours(lagHours);
            var (inTeus, outTeus) = entry.Value;

            // 1. Discharge
            if (inTeus > 0 && unloadRate > 0)
            {
                double durationHours = inTeus / unloadRate;
                DateTime dischargeEnd = arrivalTime.AddHours(durationHours);

                DistributeRange(tempDistributed, arrivalTime, dischargeEnd, unloadRate, true, start, end);
            }

            // 2. Load
            if (outTeus > 0 && loadRate > 0)
            {
                double durationHours = outTeus / loadRate;
                DateTime loadEnd = arrivalTime.AddHours(durationHours);

                DistributeRange(tempDistributed, arrivalTime, loadEnd, loadRate, false, start, end);
            }
        }

        // Convert to final result
        var distributed = new Dictionary<DateTime, DistributeLoadUnload>();
        foreach (var kvp in tempDistributed)
        {
            // We only care about buckets within the requested range
            if (kvp.Key >= start && kvp.Key <= end)
            {
                distributed[kvp.Key] = new DistributeLoadUnload(
                    kvp.Key,
                    (int)Math.Round(kvp.Value.Discharge),
                    (int)Math.Round(kvp.Value.Load)
                );
            }
        }
        return distributed;
    }

    private static void DistributeRange(
        Dictionary<DateTime, (double Discharge, double Load)> bucketDict,
        DateTime startTime,
        DateTime endTime,
        double ratePerHour,
        bool isDischarge,
        DateTime horizonStart,
        DateTime horizonEnd)
    {
        var current = startTime;

        while (current < endTime)
        {
            // Bucket key is the start of the hour
            var bucketKey = new DateTime(current.Year, current.Month, current.Day, current.Hour, 0, 0);
            var nextHour = bucketKey.AddHours(1);

            // The segment ends at the earlier of: end of operation OR end of current hour
            var segmentEnd = (endTime < nextHour) ? endTime : nextHour;

            // Only accumulate if within horizon
            if (bucketKey >= horizonStart && bucketKey <= horizonEnd)
            {
                double duration = (segmentEnd - current).TotalHours;
                double amount = duration * ratePerHour;

                if (!bucketDict.ContainsKey(bucketKey))
                {
                    bucketDict[bucketKey] = (0, 0);
                }

                var (d, l) = bucketDict[bucketKey];
                if (isDischarge)
                    bucketDict[bucketKey] = (d + amount, l);
                else
                    bucketDict[bucketKey] = (d, l + amount);
            }

            current = segmentEnd;
        }
    }

}