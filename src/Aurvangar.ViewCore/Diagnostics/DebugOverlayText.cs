using System.Globalization;
using System.Text;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Diagnostics;

/// <summary>Everything the F3 overlay shows (VIEW-17), gathered by the view each frame.</summary>
public sealed class DebugSnapshot
{
    public double Fps { get; init; }
    /// <summary>Wall-clock ms per <c>Simulation.Tick()</c>, average over the last 60 ticks.</summary>
    public double SimMsPerTick { get; init; }
    public int WaterActiveCells { get; init; }
    public double WaterStepMs { get; init; }
    public double PathSearchesPerSecond { get; init; }
    public double RegionRebuildMs { get; init; }
    /// <summary>Open jobs per kind; null or empty until the job board exists (M4-T6).</summary>
    public IReadOnlyList<KeyValuePair<string, int>>? OpenJobsByKind { get; init; }
    public long Tick { get; init; }
    public int SliceY { get; init; }
    public int MaxSliceY { get; init; }
    public int SpeedMultiplier { get; init; }
    public PickHit? Hover { get; init; }
    public string? HoverBlock { get; init; }
}

/// <summary>Formats a <see cref="DebugSnapshot"/> as the overlay's multi-line text (invariant culture).</summary>
public static class DebugOverlayText
{
    public static string Build(DebugSnapshot s)
    {
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(ci, $"FPS {s.Fps:F0}   tick {s.Tick}   speed {s.SpeedMultiplier}x").Append('\n');
        sb.Append(ci, $"sim {s.SimMsPerTick:F2} ms/tick (avg 60)").Append('\n');
        sb.Append(ci, $"water {s.WaterActiveCells} active, step {s.WaterStepMs:F2} ms").Append('\n');
        sb.Append(ci, $"paths {s.PathSearchesPerSecond:F1}/s   regions {s.RegionRebuildMs:F2} ms").Append('\n');
        sb.Append("jobs ").Append(FormatJobs(s.OpenJobsByKind)).Append('\n');
        sb.Append(s.SliceY < s.MaxSliceY ? string.Create(ci, $"slice {s.SliceY}/{s.MaxSliceY}") : "slice off").Append('\n');
        sb.Append("hover ");
        if (s.Hover is { } h)
            sb.Append(ci, $"({h.Cell.X},{h.Cell.Y},{h.Cell.Z}) {s.HoverBlock ?? "?"} {FormatNormal(h.Normal)}");
        else
            sb.Append('-');
        return sb.ToString();
    }

    private static string FormatJobs(IReadOnlyList<KeyValuePair<string, int>>? jobs)
    {
        if (jobs == null || jobs.Count == 0) return "none";
        var sorted = jobs.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => string.Create(CultureInfo.InvariantCulture, $"{kv.Key} {kv.Value}"));
        return string.Join(", ", sorted);
    }

    private static string FormatNormal(Int3 n) =>
        n == Int3.Up ? "+Y" : n == Int3.Down ? "-Y" : n == Int3.East ? "+X" : n == Int3.West ? "-X" : n == Int3.South ? "+Z" : "-Z";
}
