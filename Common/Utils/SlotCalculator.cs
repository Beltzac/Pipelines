using System;
using System.Collections.Generic;
using System.Linq;

public enum MoveClass { ImpPick, ExpDrop, EmpPick, EmpDrop /* add Reefer/DG variants if needed */ }

public record VesselPlan(DateTime T, int DischargeTEU, int LoadTEU);
public record RailPlan(DateTime T, int InTEU, int OutTEU);
public record OpsCaps(DateTime T, int GateTrucksPerHour, int YardMovesPerHour);
public record YardBand(int MinTEU, int TargetTEU, int MaxTEU);

public record HourWindow(
    DateTime T,
    int TotalSlots,
    int SlotsIn,
    int SlotsOut,
    Dictionary<MoveClass,int> ByClass,
    int YardTeuProjection,
    int YardTeuNoGate,
    int TruckIn,
    int TruckOut,
    int VesselIn,
    int VesselOut,
    int RailIn,
    int RailOut
);

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
    /// <param name="reserveRho">Operational reserve fraction (e.g., 0.10)</param>
    /// <param name="backlogInTrucks">Function: feasible IN backlog at hour t (trucks)</param>
    /// <param name="backlogOutTrucks">Function: feasible OUT backlog at hour t (trucks)</param>
    /// <param name="specialCaps">
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
        // Func<DateTime, int> backlogInTrucks,
        // Func<DateTime, int> backlogOutTrucks,
        Func<DateTime, Dictionary<MoveClass, int>> specialCaps,
        Func<MoveClass, int> classWeights
    )
    {
        if (avgTeuPerTruck <= 0) throw new ArgumentOutOfRangeException(nameof(avgTeuPerTruck));
        if (reserveRho < 0 || reserveRho >= 1) throw new ArgumentOutOfRangeException(nameof(reserveRho));
        // Forecast yard occupancy *without trucks*
        var O_noGate = ForecastYardNoGate(start, end, initialYardTeu, vessels, rails);

        // Track yard state dynamically responding to in/out slots
        var results = new List<HourWindow>();
        int currentYard = initialYardTeu;

        for (var t = start; t <= end; t = t.AddHours(1))
        {

            if (!caps.TryGetValue(t, out var cap))
            {
                // No caps for this hour → publish zero slots
                results.Add(new HourWindow(t, 0, 0, 0, new(), currentYard, O_noGate[t], 0, 0, 0, 0, 0, 0));
                continue;
            }

            // Gate vs Yard handling bottleneck (convert YardMoves to trucks)
            int yardTrucks = (int)Math.Floor(cap.YardMovesPerHour / avgTeuPerTruck);
            int rawSlots = Math.Min(cap.GateTrucksPerHour, yardTrucks);
            int totalSlots = Math.Max(0, (int)Math.Floor((1.0 - reserveRho) * rawSlots));

            // First apply vessel and rail flows
            currentYard += vessels[t].DischargeTEU + rails[t].InTEU;
            currentYard -= vessels[t].LoadTEU + rails[t].OutTEU;

            // Yard steering: desired net TEU to nudge toward target
            int diffTEU = band.TargetTEU - currentYard; // + wants IN, - wants OUT
            int maxNudgeTEU = Math.Max(1, (int)Math.Round(1.0 * band.MaxTEU)); // 3%/h guard
            diffTEU = Math.Clamp(diffTEU, -maxNudgeTEU, maxNudgeTEU);

            // Convert TEU to truck counts
            int wantIn = diffTEU > 0 ? (int)Math.Round(diffTEU / avgTeuPerTruck) : 0;
            int wantOut = diffTEU < 0 ? (int)Math.Round(-diffTEU / avgTeuPerTruck) : 0;

            // Fit into available slots
            wantIn = Math.Min(wantIn, totalSlots);
            wantOut = Math.Min(wantOut, totalSlots - wantIn);
            int remaining = totalSlots - wantIn - wantOut;

            // Allocate remainder evenly

             wantIn += remaining / 2;
             wantOut += remaining / 2;

            // Per-class caps/weights
            var perClass = SplitIntoClasses(t, wantIn, wantOut, specialCaps, classWeights);

            // Update yard occupancy using allocated slots
            currentYard += (int)(wantIn * avgTeuPerTruck);
            currentYard -= (int)(wantOut * avgTeuPerTruck);

            results.Add(new HourWindow(
                t,
                totalSlots,
                wantIn,
                wantOut,
                perClass,
                currentYard,
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

    // === helpers ===

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

    private static Dictionary<MoveClass, int> SplitIntoClasses(
        DateTime t,
        int inSlots,
        int outSlots,
        Func<DateTime, Dictionary<MoveClass, int>> specialCaps,
        Func<MoveClass, int> classWeights)
    {
        var caps = specialCaps?.Invoke(t) ?? new Dictionary<MoveClass, int>();

        var result = new Dictionary<MoveClass, int>();
        // Define IN/OUT sets (adjust to your move taxonomy)
        var inOrder = new[] { MoveClass.ExpDrop, MoveClass.EmpDrop }; // IN increases yard
        var outOrder = new[] { MoveClass.ImpPick, MoveClass.EmpPick }; // OUT decreases yard

        AllocateWeightedRoundRobin(inSlots, inOrder, caps, classWeights, result);
        AllocateWeightedRoundRobin(outSlots, outOrder, caps, classWeights, result);

        return result;
    }

    private static void AllocateWeightedRoundRobin(
        int slots,
        MoveClass[] order,
        Dictionary<MoveClass, int> caps,
        Func<MoveClass, int> classWeights,
        Dictionary<MoveClass, int> sink)
    {
        if (slots <= 0 || order.Length == 0) return;

        var weights = order.ToDictionary(c => c, c => Math.Max(1, classWeights?.Invoke(c) ?? 1));
        var allocated = order.ToDictionary(c => c, c => 0);

        // Simple weighted RR honoring per-class caps
        // We cycle until slots consumed or no capacity remains.
        while (slots > 0)
        {
            bool any = false;
            foreach (var c in order)
            {
                if (slots == 0) break;

                int cap = caps.TryGetValue(c, out var v) ? v : int.MaxValue;
                int w = weights[c];

                for (int i = 0; i < w && slots > 0; i++)
                {
                    if (allocated[c] >= cap) break;
                    allocated[c]++;
                    slots--;
                    any = true;
                }
            }
            if (!any) break; // no class could take more
        }

        foreach (var kv in allocated)
        {
            sink[kv.Key] = (sink.TryGetValue(kv.Key, out var cur) ? cur : 0) + kv.Value;
        }
    }
}