public record VesselPlan(DateTime T, int DischargeTEU, int LoadTEU, List<string> VesselNames);
public record RailPlan(DateTime T, int InTEU, int OutTEU, List<string> TrainNames);
public record OpsCaps(DateTime T, int GateTrucksInPerHour, int GateTrucksOutPerHour, int YardMovesPerHour, int VesselIn = 0, int VesselOut = 0, int RailIn = 0, int RailOut = 0, int OtherIn = 0, int OtherOut = 0);
public record YardBand(int MinTEU, int TargetTEU, int MaxTEU);

public record DistributeLoadUnload(DateTime T, int DischargeTEU, int LoadTEU);
public record HourWindow(
    DateTime T,
    int TotalSlots,
    int SlotsIn,
    int SlotsOut,
    int YardTeuProjection,
    int YardTeuNoGate,
    int YardTeuRealGate,
    int TruckIn,
    int TruckOut,
    int VesselIn,
    int VesselOut,
    int RailIn,
    int RailOut,
    int SimVesselDiff,
    int SimRailDiff,
    int SimTruckDiff,
    int RealVesselDiff,
    int RealRailDiff,
    int RealTruckDiff
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
        Dictionary<DateTime, int> actualHistory,
        YardBand band,
        double avgTeuPerTruck,
        double reserveRho,
        double easingStrength,
        double vesselLoadRate,
        double vesselUnloadRate,
        double railLoadRate,
        double railUnloadRate
    )
    {
        if (avgTeuPerTruck <= 0) throw new ArgumentOutOfRangeException(nameof(avgTeuPerTruck));
        if (reserveRho < 0 || reserveRho >= 1) throw new ArgumentOutOfRangeException(nameof(reserveRho));

        var now = DateTime.Now;
        var vesselTeus = vessels.ToDictionary(kv => kv.Key, kv => (kv.Value.DischargeTEU, kv.Value.LoadTEU));
        var distributedVessels = DistributeTeusOverTime(vesselTeus, vesselLoadRate, vesselUnloadRate, start, end);

        var railTeus = rails.ToDictionary(kv => kv.Key, kv => (kv.Value.InTEU, kv.Value.OutTEU));
        var distributedRails = DistributeTeusOverTime(railTeus, railLoadRate, railUnloadRate, start, end);


        // Forecast yard occupancy *without trucks*
        var O_noGate = ForecastYardNoGate(start, end, initialYardTeu, distributedVessels, distributedRails);

        // Track yard state dynamically responding to in/out slots
        var results = new List<HourWindow>();
        int currentYardTeus = initialYardTeu;
        int yardTeuRealGate = initialYardTeu;

        int simVesselDiff = initialYardTeu;
        int simRailDiff = initialYardTeu;
        int simTruckDiff = initialYardTeu;
        int realVesselDiff = initialYardTeu;
        int realRailDiff = initialYardTeu;
        int realTruckDiff = initialYardTeu;

        // Interpolate missing caps
        var interpolatedCaps = InterpolateCaps(caps, start, end);

        for (var t = start; t <= end; t = t.AddHours(1))
        {
            var isHistory = t < now;
            var cap = interpolatedCaps[t];

            // 1. Determine Gate Capacity/Actuals in TEUs and Trucks
            // In history, cap holds actual gate moves (already in TEUs). 
            // In future, it holds planned capacity (in trucks).
            int gateTeuInCap = isHistory ? cap.GateTrucksInPerHour : (int)(cap.GateTrucksInPerHour * avgTeuPerTruck);
            int gateTeuOutCap = isHistory ? cap.GateTrucksOutPerHour : (int)(cap.GateTrucksOutPerHour * avgTeuPerTruck);
            
            int gateTrucksInCap = isHistory ? (int)(cap.GateTrucksInPerHour / avgTeuPerTruck) : cap.GateTrucksInPerHour;
            int gateTrucksOutCap = isHistory ? (int)(cap.GateTrucksOutPerHour / avgTeuPerTruck) : cap.GateTrucksOutPerHour;

            // 2. Calculate available truck slots based on bottlenecks
            // YardMovesPerHour is in moves (approx trucks).
            int yardTrucksCap = (int)Math.Floor(cap.YardMovesPerHour / avgTeuPerTruck);
            int totalGateTrucksCap = gateTrucksInCap + gateTrucksOutCap;
            int rawTruckSlots = Math.Min(totalGateTrucksCap, yardTrucksCap);
            int totalTruckSlots = Math.Max(0, (int)Math.Floor((1.0 - reserveRho) * rawTruckSlots));
            
            var vesselFlow = distributedVessels.TryGetValue(t, out var v) ? v : new DistributeLoadUnload(t, 0, 0);
            var railFlow = distributedRails.TryGetValue(t, out var r) ? r : new DistributeLoadUnload(t, 0, 0);

            // 3. Update Projections with Vessel and Rail flows (always in TEUs)
            // We use the distributed (projected) plans for both historical and future periods.
            // This allows comparing how the planned vessel/rail schedule + real gate data 
            // performs against the actual historical inventory.
            // The "Yellow/Orange" line (yardTeuRealGate) uses these projected plans + real gate data.
            int netVesselRailTeu = vesselFlow.DischargeTEU + railFlow.DischargeTEU - vesselFlow.LoadTEU - railFlow.LoadTEU;

            int netVessel = vesselFlow.DischargeTEU - vesselFlow.LoadTEU;
            int netRail = railFlow.DischargeTEU - railFlow.LoadTEU;

            simVesselDiff += netVessel;
            simRailDiff += netRail;
            realVesselDiff += netVessel;
            realRailDiff += netRail;

            currentYardTeus += netVesselRailTeu;
            yardTeuRealGate += netVesselRailTeu;

            // 4. Update Real Gate Projection (Orange/Yellow) using full capacity/actuals
            // In history, gateTeuInCap/OutCap are actuals. In future, they are planned capacity.
            // This line shows the "Real" impact of the gate on the planned vessel/rail schedule.
            yardTeuRealGate += gateTeuInCap - gateTeuOutCap;
            realTruckDiff += gateTeuInCap - gateTeuOutCap;

            // 5. Calculate Algorithm Allocation (Blue)
            int diffTEU = band.TargetTEU - currentYardTeus;
            double biasFactor = (band.MaxTEU != band.MinTEU) ? (double)diffTEU / (band.MaxTEU - band.MinTEU) : 0.0;

            var (allocatedIn, allocatedOut) = ApplyEasing(totalTruckSlots, biasFactor, easingStrength);

            // Cap allocated slots to individual capacities
            int wantIn = Math.Max(0, Math.Min(allocatedIn, gateTrucksInCap));
            int wantOut = Math.Max(0, Math.Min(allocatedOut, gateTrucksOutCap));

            // 6. Update Algorithm Projection (Blue) using allocated slots
            int allocatedTeuIn = (int)(wantIn * avgTeuPerTruck);
            int allocatedTeuOut = (int)(wantOut * avgTeuPerTruck);
            currentYardTeus += allocatedTeuIn - allocatedTeuOut;
            simTruckDiff += allocatedTeuIn - allocatedTeuOut;

            // 7. Record results
            results.Add(new HourWindow(
                t,
                isHistory ? totalGateTrucksCap : totalTruckSlots,
                isHistory ? gateTrucksInCap : wantIn,
                isHistory ? gateTrucksOutCap : wantOut,
                currentYardTeus,
                O_noGate.TryGetValue(t, out var gate) ? gate : 0,
                yardTeuRealGate,
                isHistory ? gateTeuInCap : allocatedTeuIn,
                isHistory ? gateTeuOutCap : allocatedTeuOut,
                vesselFlow.DischargeTEU,
                vesselFlow.LoadTEU,
                railFlow.DischargeTEU,
                railFlow.LoadTEU,
                simVesselDiff,
                simRailDiff,
                simTruckDiff,
                realVesselDiff,
                realRailDiff,
                realTruckDiff
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
    /// Forecasts yard occupancy without considering gate operations.
    /// </summary>
    private static Dictionary<DateTime, int> ForecastYardNoGate(
    DateTime start, DateTime end, int O0,
    IReadOnlyDictionary<DateTime, DistributeLoadUnload> vessels,
    IReadOnlyDictionary<DateTime, DistributeLoadUnload> rails)
    {
        var res = new Dictionary<DateTime, int>();
        int cur = O0;
        for (var t = start; t <= end; t = t.AddHours(1))
        {
            var v = vessels.TryGetValue(t, out var vp) ? vp : new DistributeLoadUnload(t, 0, 0);
            var r = rails.TryGetValue(t, out var rp) ? rp : new DistributeLoadUnload(t, 0, 0);
            cur = cur + v.DischargeTEU + r.DischargeTEU - v.LoadTEU - r.LoadTEU;
            res[t] = cur;
        }
        return res;
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
        double shareInEased = PowerRatio(t, (1-easingStrength) * 10);

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
        DateTime end
    )
    {
        var distributed = new Dictionary<DateTime, DistributeLoadUnload>();
    
        foreach (var entry in hourlyInputs)
        {
            var time = entry.Key;
            var (inTeus, outTeus) = entry.Value;
    
            // Distribute incoming TEUs
            int remainingIn = inTeus;
            int hourOffsetIn = 0;
            while (remainingIn > 0)
            {
                var currentHour = time.AddHours(hourOffsetIn);
                if (currentHour > end) break;
    
                int teusThisHour = (int)Math.Min(remainingIn, unloadRate);
                if (!distributed.ContainsKey(currentHour))
                {
                    distributed[currentHour] = new DistributeLoadUnload(currentHour, 0, 0);
                }
                distributed[currentHour] = distributed[currentHour] with { DischargeTEU = distributed[currentHour].DischargeTEU + teusThisHour };
                remainingIn -= teusThisHour;
                hourOffsetIn++;
            }
    
            // Distribute outgoing TEUs
            int remainingOut = outTeus;
            int hourOffsetOut = 0;
            while (remainingOut > 0)
            {
                var currentHour = time.AddHours(hourOffsetOut);
                if (currentHour > end) break;
    
                int teusThisHour = (int)Math.Min(remainingOut, loadRate);
                if (!distributed.ContainsKey(currentHour))
                {
                    distributed[currentHour] = new DistributeLoadUnload(currentHour, 0, 0);
                }
                distributed[currentHour] = distributed[currentHour] with { LoadTEU = distributed[currentHour].LoadTEU + teusThisHour };
                remainingOut -= teusThisHour;
                hourOffsetOut++;
            }
        }
    
        return distributed;
    }

}