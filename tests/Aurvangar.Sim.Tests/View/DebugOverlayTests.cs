using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.ViewCore.Diagnostics;
using Aurvangar.ViewCore.Picking;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M3-T5: F3 debug overlay data (VIEW-17).</summary>
public class DebugOverlayTests
{
    [Fact]
    public void RollingAverage_AveragesLastSixtySamples()
    {
        var avg = new RollingAverage(60);
        Assert.Equal(0.0, avg.Average);
        for (int i = 0; i < 60; i++) avg.Add(100.0);
        for (int i = 0; i < 60; i++) avg.Add(i < 30 ? 1.0 : 3.0);
        Assert.Equal(2.0, avg.Average, 6);
        Assert.Equal(60, avg.Count);
    }

    [Fact]
    public void RateMeter_ReportsCountDeltaPerSecond()
    {
        var rate = new RateMeter();
        rate.Sample(0.0, 100);
        Assert.Equal(0.0, rate.PerSecond);
        rate.Sample(0.5, 110);          // window not complete yet
        Assert.Equal(0.0, rate.PerSecond);
        rate.Sample(1.0, 130);
        Assert.Equal(30.0, rate.PerSecond, 6);
        rate.Sample(3.0, 150);
        Assert.Equal(10.0, rate.PerSecond, 6);
    }

    [Fact]
    public void PhaseTimer_TimesWaterAndRegionPhasesOfARealTick()
    {
        var sim = new Simulation(TestContent.Db, 32, 32, 32, seed: 1);
        var timer = new PhaseTimer();
        sim.Profiler = timer;
        sim.RunTicks(3);
        Assert.Equal(3, timer.For(TickPhase.Water).Count);
        Assert.Equal(3, timer.For(TickPhase.Regions).Count);
        Assert.True(timer.For(TickPhase.Water).Average >= 0.0);

        timer.End(TickPhase.Water);     // End without Begin is ignored
        Assert.Equal(3, timer.For(TickPhase.Water).Count);
    }

    [Fact]
    public void Profiler_DoesNotChangeStateHash()
    {
        var a = new Simulation(TestContent.Db, 32, 32, 32, seed: 7);
        var b = new Simulation(TestContent.Db, 32, 32, 32, seed: 7);
        b.Profiler = new PhaseTimer();
        a.RunTicks(5); b.RunTicks(5);
        Assert.Equal(a.StateHash(), b.StateHash());
    }

    [Fact]
    public void Text_ListsEveryVIEW17Field()
    {
        var snap = new DebugSnapshot
        {
            Fps = 59.6,
            SimMsPerTick = 1.234,
            WaterActiveCells = 1500,
            WaterStepMs = 0.8,
            PathSearchesPerSecond = 12,
            RegionRebuildMs = 0.05,
            OpenJobsByKind = new[] { new KeyValuePair<string, int>("Haul", 2), new KeyValuePair<string, int>("Dig", 5) },
            Tick = 1200,
            SliceY = 20,
            MaxSliceY = 63,
            SpeedMultiplier = 3,
            Hover = new PickHit(new Int3(3, 4, 5), Int3.Up),
            HoverBlock = "Stone",
        };
        string text = DebugOverlayText.Build(snap);
        Assert.Contains("FPS 60", text);
        Assert.Contains("sim 1.23 ms/tick", text);
        Assert.Contains("water 1500 active", text);
        Assert.Contains("step 0.80 ms", text);
        Assert.Contains("paths 12.0/s", text);
        Assert.Contains("regions 0.05 ms", text);
        Assert.Contains("jobs Dig 5, Haul 2", text);   // sorted by kind
        Assert.Contains("slice 20/63", text);
        Assert.Contains("hover (3,4,5) Stone +Y", text);
    }

    [Fact]
    public void Text_NoJobsYet_AndNoHover()
    {
        string text = DebugOverlayText.Build(new DebugSnapshot { MaxSliceY = 63, SliceY = 63 });
        Assert.Contains("jobs none", text);
        Assert.Contains("slice off", text);
        Assert.Contains("hover -", text);
    }
}
