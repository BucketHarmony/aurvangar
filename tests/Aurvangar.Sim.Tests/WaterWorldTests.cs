using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>WAT-12, WAT-13, WAT-15: water reacting to block changes, and WaterDirty throttling.</summary>
[Trait("Category", "Unit")]
public class WaterWorldTests
{
    private const int Full = WaterGrid.Full;

    [Fact]
    public void Push_SplitsEquallyAndSendsRemainderUp() // WAT-12
    {
        var sim = new ScenarioBuilder().Ground(4)
            .FillBox(new Int3(6, 5, 5), new Int3(6, 5, 5), BlockId.Stone)   // east neighbor is a wall
            .Water(new Int3(5, 5, 5), 1000)
            .Build();
        sim.World.SetBlock(new Int3(5, 5, 5), BlockId.BuildingSolid);
        sim.Water.EndTick(new EventBus());                                    // applies the change, no CA step

        Assert.Equal(0, sim.Water.GetLevel(new Int3(5, 5, 5)));
        Assert.Equal(333, sim.Water.GetLevel(new Int3(4, 5, 5)));
        Assert.Equal(333, sim.Water.GetLevel(new Int3(5, 5, 4)));
        Assert.Equal(333, sim.Water.GetLevel(new Int3(5, 5, 6)));
        Assert.Equal(1, sim.Water.GetLevel(new Int3(5, 6, 5)));
        Assert.Equal(0, sim.Water.Stats.Evaporated);
    }

    [Fact]
    public void Push_CapsNeighborsAtFull_OverflowGoesUp() // WAT-12
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Water(new Int3(5, 5, 5), 1000)
            .Water(new Int3(4, 5, 5), 900)
            .Build();
        sim.World.SetBlock(new Int3(5, 5, 5), BlockId.BuildingSolid);
        sim.Water.EndTick(new EventBus());

        Assert.Equal(Full, sim.Water.GetLevel(new Int3(4, 5, 5)));             // took 124 of its 250 share
        Assert.Equal(250, sim.Water.GetLevel(new Int3(6, 5, 5)));
        Assert.Equal(250, sim.Water.GetLevel(new Int3(5, 5, 4)));
        Assert.Equal(250, sim.Water.GetLevel(new Int3(5, 5, 6)));
        Assert.Equal(126, sim.Water.GetLevel(new Int3(5, 6, 5)));
        Assert.Equal(1900, sim.Water.TotalVolume());
    }

    [Fact]
    public void Push_EnclosedCellEvaporates() // WAT-12
    {
        var sim = new ScenarioBuilder().Ground(9)
            .FillBox(new Int3(5, 5, 5), new Int3(5, 5, 5), BlockId.Air)
            .Water(new Int3(5, 5, 5), 700)
            .Build();
        sim.World.SetBlock(new Int3(5, 5, 5), BlockId.BuildingSolid);
        sim.Tick();
        Assert.Equal(0, sim.Water.TotalVolume());
        Assert.Equal(700, sim.Water.Stats.Evaporated);
    }

    [Fact]
    public void Dig_ActivatesSettledNeighbors() // WAT-13
    {
        // A full 1x1 pocket at (5,5,5) next to a stone cell at (6,5,5); everything else solid to y=9.
        var sim = new ScenarioBuilder().Ground(9)
            .FillBox(new Int3(5, 5, 5), new Int3(5, 5, 5), BlockId.Air)
            .Water(new Int3(5, 5, 5), Full)
            .Build();
        sim.RunTicks(3);
        Assert.Equal(0, sim.Water.ActiveCount);

        sim.World.SetBlock(new Int3(6, 5, 5), BlockId.Air);
        sim.Tick();
        Assert.True(sim.Water.GetLevel(new Int3(6, 5, 5)) > 0, "water did not flow into the dug cell");
    }

    [Fact]
    public void WaterDirty_SmallDriftIsThrottled() // WAT-15
    {
        var sim = new ScenarioBuilder().Ground(9)
            .FillBox(new Int3(5, 5, 5), new Int3(5, 5, 5), BlockId.Air)
            .Water(new Int3(5, 5, 5), 500)
            .Build();
        var cell = new Int3(5, 5, 5);
        int chunk = sim.World.ChunkIndexOf(cell);
        sim.Tick();
        Assert.Contains(sim.Events.Drain(), e => e is WaterDirty d && d.ChunkIndex == chunk);

        sim.Water.SetLevel(cell, 520);
        sim.Tick();
        Assert.DoesNotContain(sim.Events.Drain(), e => e is WaterDirty);

        sim.Water.SetLevel(cell, 531);                                         // 31 from the last event
        sim.Tick();
        Assert.DoesNotContain(sim.Events.Drain(), e => e is WaterDirty);

        sim.Water.SetLevel(cell, 532);                                         // 32: fires
        sim.Tick();
        Assert.Contains(sim.Events.Drain(), e => e is WaterDirty d && d.ChunkIndex == chunk);

        sim.Water.SetLevel(cell, 540);                                         // baseline reset to 532
        sim.Tick();
        Assert.DoesNotContain(sim.Events.Drain(), e => e is WaterDirty);
    }

    [Fact]
    public void WaterDirty_FiresOnWetDryCrossing() // WAT-15
    {
        var sim = new ScenarioBuilder().Ground(9)
            .FillBox(new Int3(5, 5, 5), new Int3(5, 5, 5), BlockId.Air)
            .Build();
        sim.Tick();
        sim.Events.Drain();

        sim.Water.SetLevel(new Int3(5, 5, 5), 20);                             // below 32, but 0 -> wet
        sim.Tick();
        Assert.Contains(sim.Events.Drain(), e => e is WaterDirty);

        sim.World.SetBlock(new Int3(5, 5, 5), BlockId.BuildingSolid);          // pushed out and evaporated
        sim.Tick();
        Assert.Contains(sim.Events.Drain(), e => e is WaterDirty);
    }
}
