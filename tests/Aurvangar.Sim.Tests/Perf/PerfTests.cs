using System.Diagnostics;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Scripts;
using Xunit;
using Xunit.Abstractions;

namespace Aurvangar.Sim.Tests.Perf;

[Trait("Category", "Perf")]
[Collection(PerfCollection.Name)]
public class WaterPerfTests
{
    private readonly ITestOutputHelper _out;

    public WaterPerfTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Step_20kActiveCells_Under4ms() // WAT-P1
    {
        var sim = new ScenarioBuilder(128, 32, 128).Ground(4).Build();
        var rng = new Rng(5);
        for (int z = 0; z < 128; z++)
            for (int x = 0; x < 128; x++)
                sim.Water.SetLevel(new Int3(x, 5, z), rng.NextInt(100, 900));
        sim.Tick();
        Assert.True(sim.Water.ActiveCount >= 16_000, $"setup produced only {sim.Water.ActiveCount} active cells");
        var times = PerfHelpers.Measure(() => sim.Water.Tick(sim.Events), iterations: 50);
        double median = PerfHelpers.Median(times);
        _out.WriteLine($"WAT-P1 (128x128): median {median:F2} ms, p95 {PerfHelpers.P95(times):F2} ms, active after {sim.Water.ActiveCount}");
        Assert.True(median <= 4.0 * PerfHelpers.Scale, $"water step median {median:F2} ms");
    }

    /// <summary>WAT-P1 at the full spec load: a 192x128 layer (world sizes are multiples of 32) keeps >= 20,000 cells active for every measured
    /// step (the 128x128 test above tops out at 16,384 cells).</summary>
    [Fact]
    public void Step_Sustained20kActiveCells_Under4ms() // WAT-P1
    {
        var sim = new ScenarioBuilder(192, 32, 128).Ground(4).Build();
        var rng = new Rng(5);
        for (int z = 0; z < 128; z++)
            for (int x = 0; x < 192; x++)
                sim.Water.SetLevel(new Int3(x, 5, z), rng.NextInt(100, 900));
        sim.Tick();
        int minActive = int.MaxValue;
        var times = PerfHelpers.Measure(() =>
        {
            minActive = Math.Min(minActive, sim.Water.ActiveCount);
            sim.Water.Tick(sim.Events);
        }, iterations: 50);
        double median = PerfHelpers.Median(times);
        _out.WriteLine($"WAT-P1 (192x128): median {median:F2} ms, p95 {PerfHelpers.P95(times):F2} ms, min active {minActive}");
        Assert.True(minActive >= 20_000, $"load dropped to {minActive} active cells");
        Assert.True(median <= 4.0 * PerfHelpers.Scale, $"water step median {median:F2} ms");
    }

    /// <summary>WAT-P2 in the perf run (also asserted by <c>RiverTests.Seed1_RiverSettles</c>), plus the settled
    /// river's step time for the record.</summary>
    [Fact]
    public void Seed1_SettledRiver_ActiveCellsAndStep() // WAT-P2
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        sim.RunTicks(1200);
        int active = sim.Water.ActiveCount;
        Assert.True(active <= 3000, $"active cells {active}");
        var times = PerfHelpers.Measure(() => sim.Water.Tick(sim.Events), iterations: 50);
        _out.WriteLine($"WAT-P2: {active} active cells at tick 1200; river step median {PerfHelpers.Median(times):F3} ms");
    }
}

[Trait("Category", "Perf")]
[Collection(PerfCollection.Name)]
public class OtherPerfTests
{
    private readonly ITestOutputHelper _out;

    public OtherPerfTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Moisture_Recompute() // ECO-16
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        sim.RunTicks(1200);
        var times = PerfHelpers.Measure(() => sim.Moisture.Recompute(), iterations: 50);
        double median = PerfHelpers.Median(times);
        _out.WriteLine($"ECO-16: moisture recompute median {median:F3} ms, p95 {PerfHelpers.P95(times):F3} ms");
        Assert.True(median <= 3.0 * PerfHelpers.Scale, $"moisture recompute median {median:F2} ms");
    }

    /// <summary>SIM-P1: seed 1 with the full <see cref="SurvivalScript"/> to day 5 (tick 12,000, the first drought
    /// tick: the river drains, the flooded tunnel and its levees exist), then the median of 500 <c>Tick()</c> calls.
    /// Only <c>Tick()</c> is timed; the script's enqueue and the event drain (the view's job) are outside the timer.</summary>
    [Fact]
    public void FullTick_Seed1_Day5() // SIM-P1
    {
        var sim = WorldFactory.Create(SurvivalScript.Seed, TestContent.Db);
        for (long t = 0; t < 12_000; t++)
        {
            SurvivalScript.EnqueueDue(sim);
            sim.Tick();
            sim.Events.Drain();
        }
        Assert.Equal(5, sim.Agents.All.Count(a => a.IsAlive));
        var phases = new PhaseTimer();
        sim.Profiler = phases;
        long rebuilds0 = sim.Counters.RegionRebuilds;
        PerfHelpers.SettleGc();
        var times = new double[500];
        for (int i = 0; i < times.Length; i++)
        {
            SurvivalScript.EnqueueDue(sim);
            long t0 = Stopwatch.GetTimestamp();
            sim.Tick();
            times[i] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            sim.Events.Drain();
        }
        Array.Sort(times);
        double median = PerfHelpers.Median(times);
        _out.WriteLine($"SIM-P1: full tick median {median:F3} ms, p95 {PerfHelpers.P95(times):F3} ms, max {times[^1]:F2} ms " +
                       $"(ticks 12000-12499, active water {sim.Water.ActiveCount}); phase totals over 500 ticks: " +
                       $"water {phases.Total(TickPhase.Water):F0} ms, regions {phases.Total(TickPhase.Regions):F0} ms " +
                       $"({sim.Counters.RegionRebuilds - rebuilds0} rebuilds)");
        Assert.True(median <= 8.0 * PerfHelpers.Scale, $"full tick median {median:F2} ms");
    }
}

/// <summary>Accumulates wall time per <see cref="TickPhase"/> for perf-test breakdowns.</summary>
internal sealed class PhaseTimer : ITickProfiler
{
    private readonly long[] _start = new long[8];
    private readonly double[] _total = new double[8];

    public void Begin(TickPhase phase) => _start[(int)phase] = Stopwatch.GetTimestamp();
    public void End(TickPhase phase) => _total[(int)phase] += Stopwatch.GetElapsedTime(_start[(int)phase]).TotalMilliseconds;
    public double Total(TickPhase phase) => _total[(int)phase];
}
