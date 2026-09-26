using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Aurvangar.Sim.Tests.Perf;

[Trait("Category", "Perf")]
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
public class OtherPerfTests
{
    [Fact(Skip = "M6-T1")]
    public void Moisture_Recompute() => Placeholder.Write("ECO-16: MoistureMap recompute on seed 1 median <= 3 ms * PERF_SCALE");

    [Fact(Skip = "M6-T7")]
    public void FullTick_Seed1_Day5() => Placeholder.Write("SIM-P1: seed 1 + SurvivalScript to day 5, then median Tick() over 500 ticks <= 8 ms * PERF_SCALE");
}
