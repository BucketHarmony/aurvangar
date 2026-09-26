using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Xunit;

namespace Aurvangar.Sim.Tests.Perf;

[Trait("Category", "Perf")]
public class WaterPerfTests
{
    [Fact(Skip = "M2-T6")]
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
        Assert.True(median <= 4.0 * PerfHelpers.Scale, $"water step median {median:F2} ms");
    }
}

[Trait("Category", "Perf")]
public class OtherPerfTests
{
    [Fact(Skip = "M4-T12")]
    public void AStar_P95_100CellPaths() => Placeholder.Write("PTH-P1: 200 random ~100-cell queries on seed 1, p95 <= 1.5 ms * PERF_SCALE");

    [Fact(Skip = "M4-T12")]
    public void RegionRebuild_Seed1() => Placeholder.Write("PTH-P2: Regions full rebuild on seed 1 median <= 25 ms * PERF_SCALE");

    [Fact(Skip = "M3-T7")]
    public void Mesher_SurfaceChunk() => Placeholder.Write("MESH-P1: ChunkMesher.Build on the busiest seed-1 surface chunk median <= 6 ms * PERF_SCALE");

    [Fact(Skip = "M6-T1")]
    public void Moisture_Recompute() => Placeholder.Write("ECO-16: MoistureMap recompute on seed 1 median <= 3 ms * PERF_SCALE");

    [Fact(Skip = "M6-T7")]
    public void FullTick_Seed1_Day5() => Placeholder.Write("SIM-P1: seed 1 + SurvivalScript to day 5, then median Tick() over 500 ticks <= 8 ms * PERF_SCALE");
}
