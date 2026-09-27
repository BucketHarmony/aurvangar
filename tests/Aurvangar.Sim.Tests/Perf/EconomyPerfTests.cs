using System.Diagnostics;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Scripts;
using Xunit;
using Xunit.Abstractions;

namespace Aurvangar.Sim.Tests.Perf;

/// <summary>CRF-P1 (M11-T7): SIM-P1 holds with the economy running.</summary>
[Trait("Category", "Perf")]
[Collection(PerfCollection.Name)]
public class EconomyPerfTests
{
    /// <summary>The window starts after the hall's release (<see cref="EconomyScript.HallTick"/>): the trader is in
    /// (3000..4800), both workshops refill their Keep orders, and the hall's Build jobs are open.</summary>
    public const long WindowStart = 3700;

    private readonly ITestOutputHelper _out;

    public EconomyPerfTests(ITestOutputHelper output) => _out = output;

    /// <summary>CRF-P1: seed 1 with <see cref="EconomyScript"/> to tick <see cref="WindowStart"/>, then the median of
    /// 500 <c>Tick()</c> calls while the trader is in and both workshops work. Only <c>Tick()</c> is timed. Prints the
    /// Workshops and Traders phase totals over the 500 ticks.</summary>
    [Fact]
    public void SimP1_EconomyWithTraderIn() // CRF-P1
    {
        var sim = WorldFactory.Create(EconomyScript.Seed, TestContent.Db);
        for (long t = 0; t < WindowStart; t++)
        {
            EconomyScript.EnqueueDue(sim);
            sim.Tick();
            sim.Events.Drain();
        }
        var workshops = sim.Buildings.All.Where(b => b.Def.Workshop is not null).ToList();
        Assert.Equal(2, workshops.Count);
        var phases = new PhaseTimer();
        sim.Profiler = phases;
        var working = new int[workshops.Count];
        int traderIn = 0;
        PerfHelpers.SettleGc();
        var times = new double[500];
        for (int i = 0; i < times.Length; i++)
        {
            EconomyScript.EnqueueDue(sim);
            long t0 = Stopwatch.GetTimestamp();
            sim.Tick();
            times[i] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            sim.Events.Drain();
            if (sim.Trader.IsHere) traderIn++;
            for (int w = 0; w < workshops.Count; w++)
                if (Workshops.StatusOf(sim, workshops[w]).Status == WorkshopStatus.Working) working[w]++;
        }
        Array.Sort(times);
        double median = PerfHelpers.Median(times);
        _out.WriteLine($"CRF-P1: full tick median {median:F3} ms, p95 {PerfHelpers.P95(times):F3} ms, max {times[^1]:F2} ms " +
                       $"(ticks {WindowStart}-{WindowStart + 499}, trader in {traderIn} ticks, working ticks " +
                       $"{string.Join(", ", workshops.Select((b, w) => $"{b.Def.Id} {working[w]}"))}); totals over 500 ticks: " +
                       $"workshops {phases.Total(TickPhase.Workshops):F1} ms, traders {phases.Total(TickPhase.Traders):F1} ms, " +
                       $"build poster {phases.Total(TickPhase.BlockBuild):F1} ms, water {phases.Total(TickPhase.Water):F0} ms");
        Assert.Equal(times.Length, traderIn);
        Assert.All(working, n => Assert.True(n > 0, "a workshop never worked in the window"));
        Assert.True(median <= 8.0 * PerfHelpers.Scale, $"full tick median {median:F2} ms");
    }
}
