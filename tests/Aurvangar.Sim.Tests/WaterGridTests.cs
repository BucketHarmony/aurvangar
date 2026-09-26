using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

public class WaterGridTests
{
    private const int Full = WaterGrid.Full;

    /// <summary>Stone everywhere up to y=9 with a 1x1 air shaft at (5, 5..9, 5).</summary>
    private static ScenarioBuilder Shaft() =>
        new ScenarioBuilder().Ground(9).FillBox(new Int3(5, 5, 5), new Int3(5, 9, 5), BlockId.Air);

    private static long Accounted(Simulation sim) =>
        sim.Water.TotalVolume() + sim.Water.Stats.Drained + sim.Water.Stats.Evaporated + sim.Water.Stats.Pumped
        - sim.Water.Stats.SourceAdded;

    [Fact]
    public void SetLevel_ClampsAndIgnoresSolid()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        sim.Water.SetLevel(new Int3(1, 5, 1), 5000);
        sim.Water.SetLevel(new Int3(1, 4, 1), 500);
        Assert.Equal(Full, sim.Water.GetLevel(new Int3(1, 5, 1)));
        Assert.Equal(0, sim.Water.GetLevel(new Int3(1, 4, 1)));
    }

    [Fact]
    public void Fall_MovesOneCellPerStep() // WAT-04
    {
        var sim = new ScenarioBuilder().Ground(4).Water(new Int3(5, 10, 5), Full).Build();
        sim.Tick();
        Assert.Equal(Full, sim.Water.GetLevel(new Int3(5, 9, 5)));
        Assert.Equal(0, sim.Water.GetLevel(new Int3(5, 10, 5)));
    }

    [Fact]
    public void Fall_TopsUpPartialCellBelow() // WAT-04
    {
        var sim = Shaft().Water(new Int3(5, 5, 5), 1000).Water(new Int3(5, 6, 5), 100).Build();
        sim.Tick();
        Assert.Equal(Full, sim.Water.GetLevel(new Int3(5, 5, 5)));
        Assert.Equal(76, sim.Water.GetLevel(new Int3(5, 6, 5)));
    }

    [Fact]
    public void Fall_ActiveSetEmptiesWhenSettled() // WAT-02
    {
        var sim = Shaft().Water(new Int3(5, 9, 5), Full).Build();
        sim.RunTicks(20);
        Assert.Equal(Full, sim.Water.GetLevel(new Int3(5, 5, 5)));
        Assert.Equal(0, sim.Water.ActiveCount);
    }

    [Fact]
    public void Conservation_SingleColumn() // WAT-11
    {
        var sim = Shaft().Water(new Int3(5, 9, 5), Full).Water(new Int3(5, 8, 5), 300).Build();
        long before = sim.Water.TotalVolume();
        for (int i = 0; i < 30; i++) { sim.Tick(); Assert.Equal(before, Accounted(sim)); }
    }

    [Fact]
    public void ActiveSet_SetLevelActivatesWetCellsOnly() // WAT-02, ADR-010
    {
        var sim = new ScenarioBuilder().Ground(4).Water(new Int3(5, 5, 5), Full).Build();
        Assert.Equal(1, sim.Water.ActiveCount);      // dry air neighbors and the solid floor do not join
        sim.Water.SetLevel(new Int3(7, 5, 5), 10);
        Assert.Equal(2, sim.Water.ActiveCount);
        sim.Water.SetLevel(new Int3(5, 5, 5), Full); // unchanged level: nothing new
        Assert.Equal(2, sim.Water.ActiveCount);
    }

    [Fact]
    public void Fall_ThroughOpenAirLandsOnFloor() // WAT-04, WAT-02
    {
        var sim = new ScenarioBuilder().Ground(4).Water(new Int3(5, 20, 5), 700).Build();
        sim.RunTicks(15);
        Assert.Equal(700, sim.Water.GetLevel(new Int3(5, 5, 5)));
        Assert.Equal(700, sim.Water.TotalVolume());
        Assert.Equal(1, sim.Water.ActiveCount);      // landed this step; leaves after a step with no change
    }

    [Fact(Skip = "M2-T2")]
    public void Spread_NotWhileFalling() // WAT-05
    {
        var sim = new ScenarioBuilder().Ground(4).Water(new Int3(5, 10, 5), Full).Build();
        sim.Tick();
        foreach (var d in Int3.Horizontal4) Assert.Equal(0, sim.Water.GetLevel(new Int3(5, 10, 5) + d));
    }

    [Fact(Skip = "M2-T2")]
    public void Spread_EqualizesInChannel() // WAT-05, WAT-06
    {
        // 5-long enclosed channel at y=5, z=5, x=1..5
        var sim = new ScenarioBuilder().Ground(9)
            .FillBox(new Int3(1, 5, 5), new Int3(5, 5, 5), BlockId.Air)
            .Water(new Int3(1, 5, 5), Full)
            .Build();
        sim.RunTicks(500);
        var levels = Enumerable.Range(1, 5).Select(x => sim.Water.GetLevel(new Int3(x, 5, 5))).ToArray();
        Assert.Equal(Full, levels.Sum());
        Assert.True(levels.Max() - levels.Min() <= 4, $"not level: {string.Join(",", levels)}");
    }

    [Fact(Skip = "M2-T2")]
    public void Spread_NeverFlowsUp() // WAT-08
    {
        var sim = Shaft().Water(new Int3(5, 5, 5), Full).Build();
        sim.RunTicks(50);
        Assert.Equal(0, sim.Water.GetLevel(new Int3(5, 6, 5)));
    }

    [Fact(Skip = "M2-T3")]
    public void Conservation_RandomPours() // WAT-11, WAT-16
    {
        var sim = new ScenarioBuilder().Ground(4)
            .FillBox(new Int3(0, 5, 0), new Int3(31, 8, 0), BlockId.Stone)
            .FillBox(new Int3(0, 5, 31), new Int3(31, 8, 31), BlockId.Stone)
            .FillBox(new Int3(0, 5, 0), new Int3(0, 8, 31), BlockId.Stone)
            .FillBox(new Int3(31, 5, 0), new Int3(31, 8, 31), BlockId.Stone)
            .Build();
        var rng = new Rng(99);
        long poured = 0;
        for (int t = 0; t < 300; t++)
        {
            if (t % 6 == 0)
            {
                var c = new Int3(rng.NextInt(1, 31), rng.NextInt(5, 12), rng.NextInt(1, 31));
                int before = sim.Water.GetLevel(c);
                sim.Water.SetLevel(c, before + rng.NextInt(1, Full));
                poured += sim.Water.GetLevel(c) - before;
            }
            sim.Tick();
            Assert.Equal(poured, Accounted(sim));
        }
    }

    [Fact(Skip = "M2-T3")]
    public void Source_AddsVolume_Drain_RemovesIt() // WAT-09, WAT-10
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Source(new Int3(2, 5, 2)).Drain(new Int3(8, 5, 2))
            .Build();
        sim.RunTicks(400);
        Assert.True(sim.Water.Stats.SourceAdded > 0);
        Assert.True(sim.Water.Stats.Drained > 0);
        Assert.Equal(0, sim.Water.GetLevel(new Int3(8, 5, 2)));
        Assert.Equal(0, Accounted(sim));
    }

    [Fact(Skip = "M2-T3")]
    public void Source_StrengthZero_AddsNothing() // WAT-09
    {
        var sim = new ScenarioBuilder().Ground(4).Source(new Int3(2, 5, 2)).Build();
        sim.Water.SourceStrength = 0;
        sim.RunTicks(50);
        Assert.Equal(0, sim.Water.Stats.SourceAdded);
    }
}
