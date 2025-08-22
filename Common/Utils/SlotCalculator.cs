public record VesselPlan(DateTime T, int DischargeTEU, int LoadTEU);
public record RailPlan(DateTime T, int InTEU, int OutTEU);
public record OpsCaps(DateTime T, int GateTrucksPerHour, int YardMovesPerHour);
public record YardBand(int MinTEU, int TargetTEU, int MaxTEU);

public record HourWindow(
    DateTime T,
    int TotalSlots,
    int SlotsIn,
    int SlotsOut,
    int YardTeuProjection,
    int YardTeuNoGate,
    int TruckIn,
    int TruckOut,
    int VesselIn,
    int VesselOut,
    int RailIn,
    int RailOut
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
        YardBand band,
        double avgTeuPerTruck,
        double reserveRho,
        double easingStrength
    )
    {
        if (avgTeuPerTruck <= 0) throw new ArgumentOutOfRangeException(nameof(avgTeuPerTruck));
        if (reserveRho < 0 || reserveRho >= 1) throw new ArgumentOutOfRangeException(nameof(reserveRho));
        // Forecast yard occupancy *without trucks*
        var O_noGate = ForecastYardNoGate(start, end, initialYardTeu, vessels, rails);

        // Track yard state dynamically responding to in/out slots
        var results = new List<HourWindow>();
        int currentYardTeus = initialYardTeu;

        for (var t = start; t <= end; t = t.AddHours(1))
        {
            if (!caps.TryGetValue(t, out var cap))
            {
                // No caps for this hour → publish zero slots
                results.Add(new HourWindow(t, 0, 0, 0, currentYardTeus, O_noGate[t], 0, 0, 0, 0, 0, 0));
                continue;
            }

            // Gate vs Yard handling bottleneck (convert YardMoves to trucks)
            int yardTrucks = (int)Math.Floor(cap.YardMovesPerHour / avgTeuPerTruck);
            int rawTruckSlots = Math.Min(cap.GateTrucksPerHour, yardTrucks);
            int totalTruckSlots = Math.Max(0, (int)Math.Floor((1.0 - reserveRho) * rawTruckSlots));

            // First apply vessel and rail flows
            currentYardTeus += vessels[t].DischargeTEU + rails[t].InTEU;
            currentYardTeus -= vessels[t].LoadTEU + rails[t].OutTEU;

            // Yard steering: desired net TEU to nudge toward target
            int diffTEU = band.TargetTEU - currentYardTeus; // + wants IN, - wants OUT

            // biasFactor in range [-1, 1]
            var biasFactor = 0.0;
            if (band.MaxTEU != band.MinTEU)
            {
                biasFactor = (double)diffTEU / (band.MaxTEU - band.MinTEU);
            }

            // Distribute slots using easing
            var (allocatedIn, allocatedOut) = ApplyEasing(totalTruckSlots, biasFactor, easingStrength);

            int wantIn = allocatedIn;
            int wantOut = allocatedOut;

            if (wantIn < 0) wantIn = 0;
            if (wantOut < 0) wantOut = 0;

            // Update yard occupancy using allocated slots
            currentYardTeus += (int)(wantIn * avgTeuPerTruck);
            currentYardTeus -= (int)(wantOut * avgTeuPerTruck);

            results.Add(new HourWindow(
                t,
                totalTruckSlots,
                wantIn,
                wantOut,
                currentYardTeus,
                O_noGate[t],
                (int)(wantIn * avgTeuPerTruck),
                (int)(wantOut * avgTeuPerTruck),
                vessels[t].DischargeTEU,
                vessels[t].LoadTEU,
                rails[t].InTEU,
                rails[t].OutTEU
            ));
        }

        return results;
    }

    /// <summary>
    /// Forecasts yard occupancy without considering gate operations.
    /// </summary>
    private static Dictionary<DateTime, int> ForecastYardNoGate(
        DateTime start, DateTime end, int O0,
        IReadOnlyDictionary<DateTime, VesselPlan> vessels,
        IReadOnlyDictionary<DateTime, RailPlan> rails)
    {
        var res = new Dictionary<DateTime, int>();
        int cur = O0;
        for (var t = start; t <= end; t = t.AddHours(1))
        {
            var v = vessels.TryGetValue(t, out var vp) ? vp : new VesselPlan(t, 0, 0);
            var r = rails.TryGetValue(t, out var rp) ? rp : new RailPlan(t, 0, 0);
            cur = cur + v.DischargeTEU + r.InTEU - v.LoadTEU - r.OutTEU;
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

}