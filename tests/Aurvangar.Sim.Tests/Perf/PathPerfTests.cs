using Aurvangar.Sim.Core;
using Aurvangar.Sim.Paths;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Aurvangar.Sim.Tests.Perf;

/// <summary>PTH-P1 and PTH-P2 on seed 1 (ADR-034 defines the sampling).</summary>
[Trait("Category", "Perf")]
public class PathPerfTests
{
    public const int PairCount = 200, MinPathCells = 90, MaxPathCells = 110, MaxAttempts = 20_000;

    private readonly ITestOutputHelper _out;

    public PathPerfTests(ITestOutputHelper output) => _out = output;

    /// <summary>Seed 1 after one tick (regions built). Walkable cells of the largest region, ascending flat index.</summary>
    private static (Simulation sim, List<Int3> cells) Seed1Walkable()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        sim.Tick();
        var world = sim.World;
        var counts = new int[sim.Regions.Count + 1];
        var all = new List<Int3>();
        for (int i = 0; i < world.CellCount; i++)
        {
            var c = world.CellOf(i);
            int r = sim.Regions.RegionOf(c);
            if (r == Regions.None) continue;
            counts[r]++;
            all.Add(c);
        }
        int biggest = 1;
        for (int r = 2; r < counts.Length; r++) if (counts[r] > counts[biggest]) biggest = r;
        return (sim, all.Where(c => sim.Regions.RegionOf(c) == biggest).ToList());
    }

    /// <summary>Deterministic "~100-cell" pairs: random start/goal from the largest region (Rng seed 1234, goal 60..100
    /// cells away on the larger horizontal axis), kept when A* finds a path of 90..110 cells (start and end included).
    /// The selection pass also warms the flag cache and the search arrays.</summary>
    public static List<(Int3 a, Int3 b)> SelectPairs(Simulation sim, List<Int3> cells)
    {
        var rng = new Rng(1234);
        var pairs = new List<(Int3, Int3)>();
        for (int attempt = 0; attempt < MaxAttempts && pairs.Count < PairCount; attempt++)
        {
            var a = cells[rng.NextInt(cells.Count)];
            var b = cells[rng.NextInt(cells.Count)];
            int d = Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));
            if (d < 60 || d > 100) continue;
            var r = sim.Pathfinder.FindPath(a, b);
            if (r.Found && r.Path.Length >= MinPathCells && r.Path.Length <= MaxPathCells) pairs.Add((a, b));
        }
        return pairs;
    }

    [Fact]
    public void AStar_P95_100CellPaths() // PTH-P1
    {
        var (sim, cells) = Seed1Walkable();
        var pairs = SelectPairs(sim, cells);
        Assert.Equal(PairCount, pairs.Count);

        var times = new double[pairs.Count];
        int maxExpanded = 0;
        for (int k = 0; k < pairs.Count; k++)
        {
            long t = System.Diagnostics.Stopwatch.GetTimestamp();
            var r = sim.Pathfinder.FindPath(pairs[k].a, pairs[k].b);
            times[k] = System.Diagnostics.Stopwatch.GetElapsedTime(t).TotalMilliseconds;
            Assert.True(r.Found);
            maxExpanded = Math.Max(maxExpanded, sim.Pathfinder.LastExpanded);
        }
        Array.Sort(times);
        double p95 = PerfHelpers.P95(times);
        _out.WriteLine($"PTH-P1: {pairs.Count} paths of {MinPathCells}..{MaxPathCells} cells, median {PerfHelpers.Median(times):F3} ms, " +
            $"p95 {p95:F3} ms, max {times[^1]:F3} ms, max expanded {maxExpanded}");
        Assert.True(p95 <= 1.5 * PerfHelpers.Scale, $"A* p95 {p95:F3} ms");
    }

    /// <summary>PTH-P2: full rebuild with a warm flag cache (the in-game case: one walkability change, then the
    /// end-of-tick rebuild), and a cold one (after <see cref="PathGrid.InvalidateAll"/>, as after a load). Both
    /// must meet the budget.</summary>
    [Fact]
    public void RegionRebuild_Seed1() // PTH-P2
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        sim.Tick();
        int regions = sim.Regions.Count;
        var warm = PerfHelpers.Measure(() =>
        {
            sim.Regions.MarkDirty();
            Assert.True(sim.Regions.RebuildIfDirty());
        }, iterations: 30);
        var cold = PerfHelpers.Measure(() =>
        {
            sim.PathGrid.InvalidateAll();
            Assert.True(sim.Regions.RebuildIfDirty());
        }, iterations: 30);
        Assert.Equal(regions, sim.Regions.Count);
        double warmMedian = PerfHelpers.Median(warm), coldMedian = PerfHelpers.Median(cold);
        _out.WriteLine($"PTH-P2: {regions} regions; warm median {warmMedian:F2} ms (p95 {PerfHelpers.P95(warm):F2}), " +
            $"cold median {coldMedian:F2} ms (p95 {PerfHelpers.P95(cold):F2})");
        Assert.True(warmMedian <= 25.0 * PerfHelpers.Scale, $"warm region rebuild median {warmMedian:F2} ms");
        Assert.True(coldMedian <= 25.0 * PerfHelpers.Scale, $"cold region rebuild median {coldMedian:F2} ms");
    }
}
