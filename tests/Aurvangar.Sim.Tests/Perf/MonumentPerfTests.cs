using System.Diagnostics;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Scripts;
using Xunit;
using Xunit.Abstractions;

namespace Aurvangar.Sim.Tests.Perf;

/// <summary>CON-P1 (M8-T6): SIM-P1 holds during construction.</summary>
[Trait("Category", "Perf")]
[Collection(PerfCollection.Name)]
public class MonumentPerfTests
{
    private readonly ITestOutputHelper _out;

    public MonumentPerfTests(ITestOutputHelper output) => _out = output;

    private static int Built(Simulation sim) =>
        MonumentScript.PlannedCells.Count(c => sim.World.GetBlock(c) == BlockId.Masonry);

    /// <summary>CON-P1: seed 1 with <see cref="MonumentScript"/> to tick 13,000 (the tower's middle courses going up:
    /// Build jobs open, dwarves on the walls and the stair), then the median of 500 <c>Tick()</c> calls. Only
    /// <c>Tick()</c> is timed. Prints the Build poster's time (<see cref="TickPhase.BlockBuild"/>) and the place
    /// trial's totals over the 500 ticks.</summary>
    [Fact]
    public void SimP1_DuringMonumentBuild() // CON-P1
    {
        var sim = WorldFactory.Create(MonumentScript.Seed, TestContent.Db);
        for (long t = 0; t < 13_000; t++)
        {
            MonumentScript.EnqueueDue(sim);
            sim.Tick();
            sim.Events.Drain();
        }
        Assert.True(sim.Plans.Count > 0 && sim.Jobs.All.Any(j => j.Kind == JobKind.Build), "not mid-build");
        var phases = new PhaseTimer();
        sim.Profiler = phases;
        long exact0 = sim.PlaceTrial.ExactChecks, cuts0 = sim.PlaceTrial.CutComputes;
        int built0 = Built(sim);
        PerfHelpers.SettleGc();
        var times = new double[500];
        for (int i = 0; i < times.Length; i++)
        {
            MonumentScript.EnqueueDue(sim);
            long t0 = Stopwatch.GetTimestamp();
            sim.Tick();
            times[i] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            sim.Events.Drain();
        }
        Array.Sort(times);
        double median = PerfHelpers.Median(times);
        _out.WriteLine($"CON-P1: full tick median {median:F3} ms, p95 {PerfHelpers.P95(times):F3} ms, max {times[^1]:F2} ms " +
                       $"(ticks 13000-13499, blocks built {built0} -> {Built(sim)} of {MonumentScript.PlannedCells.Count}); " +
                       $"totals over 500 ticks: build poster {phases.Total(TickPhase.BlockBuild):F1} ms, " +
                       $"water {phases.Total(TickPhase.Water):F0} ms, regions {phases.Total(TickPhase.Regions):F0} ms; " +
                       $"place trial {sim.PlaceTrial.ExactChecks - exact0} exact checks, {sim.PlaceTrial.CutComputes - cuts0} cut computes");
        Assert.True(median <= 8.0 * PerfHelpers.Scale, $"full tick median {median:F2} ms");
    }
}
