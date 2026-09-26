using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

[Trait("Category", "Scenario")]
public class WaterScenarioTests
{
    private const int Full = WaterGrid.Full;

    [Fact]
    public void SingleCellSpreadsAndEvaporates() // water.md scenario 1
    {
        var sim = new ScenarioBuilder().Ground(4).Water(new Int3(16, 5, 16), Full).Build();
        sim.RunTicks(400);
        Assert.Equal(0, sim.Water.TotalVolume());
        Assert.Equal(Full, sim.Water.Stats.Evaporated);
    }

    [Fact]
    public void ShaftFillsBottomUp() // water.md scenario 3
    {
        var sim = new ScenarioBuilder().Ground(9)
            .FillBox(new Int3(5, 5, 5), new Int3(5, 9, 5), BlockId.Air)
            .Source(new Int3(5, 11, 5))
            .Build();
        // Falling water moves one cell per step (WAT-04) and the double-buffered step (WAT-03) delivers the source's
        // stream as separate Full slugs, so wet cells sit over empty ones while in transit. "Bottom up" therefore
        // means (ADR-011): no water ever sits above a partially filled shaft cell, the bottom cell is Full from the
        // step water first reaches it, and at the end the whole shaft is Full.
        for (int t = 0; t < 200; t++)
        {
            sim.Tick();
            int bottom = sim.Water.GetLevel(new Int3(5, 5, 5));
            Assert.True(bottom == 0 || bottom == Full, $"tick {t}: bottom cell partial ({bottom})");
            for (int y = 6; y <= 9; y++)
            {
                if (sim.Water.GetLevel(new Int3(5, y, 5)) == 0) continue;
                int below = sim.Water.GetLevel(new Int3(5, y - 1, 5));
                Assert.True(below == 0 || below == Full, $"tick {t}: y={y} wet above partial cell ({below})");
            }
        }
        for (int y = 5; y <= 9; y++) Assert.Equal(Full, sim.Water.GetLevel(new Int3(5, y, 5)));
    }

    [Fact(Skip = "M2-T3")]
    public void BasinFillsThenOverflows() // water.md scenario 2
    {
        // Floor top y=4. Walls x=0/11, z=0/11 from y=5..7. Interior x,z in 1..10, 3 deep.
        var b = new ScenarioBuilder().Ground(4);
        b.FillBox(new Int3(0, 5, 0), new Int3(11, 7, 0), BlockId.Stone)
         .FillBox(new Int3(0, 5, 11), new Int3(11, 7, 11), BlockId.Stone)
         .FillBox(new Int3(0, 5, 0), new Int3(0, 7, 11), BlockId.Stone)
         .FillBox(new Int3(11, 5, 0), new Int3(11, 7, 11), BlockId.Stone)
         .Source(new Int3(1, 7, 1));
        var sim = b.Build();

        sim.RunTicks(2000);
        long interior = 0;
        for (int y = 5; y <= 7; y++)
            for (int z = 1; z <= 10; z++)
                for (int x = 1; x <= 10; x++)
                    interior += sim.Water.GetLevel(new Int3(x, y, z));
        Assert.True(interior >= 2L * 100 * Full, $"basin not filled: {interior}");

        sim.RunTicks(1000);
        long outside = 0;
        for (int z = 0; z < 32; z++)
            for (int x = 12; x < 32; x++)
                outside += sim.Water.GetLevel(new Int3(x, 5, z));
        Assert.True(outside > 0, "basin never overflowed");
    }

    [Fact(Skip = "M2-T4")]
    public void BreachFloodsTunnel() // water.md scenario 4, WAT-13
    {
        // Solid rock to y=9. Reservoir x1..5 z1..5 y5..6 full. Wall at x=6. Tunnel x7..16 at z=3, y5..6.
        var sim = new ScenarioBuilder().Ground(9)
            .FillBox(new Int3(1, 5, 1), new Int3(5, 6, 5), BlockId.Air)
            .FillBox(new Int3(7, 5, 3), new Int3(16, 6, 3), BlockId.Air)
            .Build();
        for (int y = 5; y <= 6; y++)
            for (int z = 1; z <= 5; z++)
                for (int x = 1; x <= 5; x++)
                    sim.Water.SetLevel(new Int3(x, y, z), Full);
        sim.RunTicks(20);
        Assert.Equal(0, sim.Water.GetLevel(new Int3(7, 5, 3)));

        sim.World.SetBlock(new Int3(6, 5, 3), BlockId.Air);
        sim.World.SetBlock(new Int3(6, 6, 3), BlockId.Air);
        sim.RunTicks(300);
        Assert.True(sim.Water.GetLevel(new Int3(16, 5, 3)) > 0, "flood did not reach the tunnel end");
    }

    [Fact(Skip = "M2-T4")]
    public void LeveePushConservesVolume() // water.md scenario 5, WAT-12
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Layer(5,
                "SSSSS",
                "SWWWS",
                "SWWWS",
                "SWWWS",
                "SSSSS")
            .Build();
        long before = sim.Water.TotalVolume();
        sim.World.SetBlock(new Int3(2, 5, 2), BlockId.BuildingSolid);
        sim.Tick();
        Assert.Equal(0, sim.Water.GetLevel(new Int3(2, 5, 2)));
        Assert.Equal(before, sim.Water.TotalVolume() + sim.Water.Stats.Evaporated);
    }

    [Fact(Skip = "M2-T4")]
    public void WaterDirty_EmittedForChangedChunks() // WAT-15
    {
        var sim = new ScenarioBuilder().Ground(4).Water(new Int3(5, 6, 5), Full).Build();
        sim.Tick();
        Assert.Contains(sim.Events.Drain(), e => e is Events.WaterDirty);
    }
}

[Trait("Category", "Scenario")]
public class RiverTests
{
    [Fact(Skip = "M2-T5")]
    public void Seed1_RiverFlowsAcrossMap()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        bool wetAtMiddle = false;
        for (int z = 0; z < sim.World.SizeZ && !wetAtMiddle; z++)
            for (int y = 0; y < sim.World.SizeY && !wetAtMiddle; y++)
                wetAtMiddle = sim.Water.GetLevel(new Int3(64, y, z)) > 0;
        Assert.True(wetAtMiddle, "no water at x=64 after pre-settle");
        Assert.Equal(0, sim.Clock.Tick); // pre-settle must reset the clock
    }

    [Fact(Skip = "M2-T5")]
    public void Seed1_RiverSettles() // WAT-P2
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        sim.RunTicks(1200);
        Assert.True(sim.Water.ActiveCount <= 3000, $"active cells {sim.Water.ActiveCount}");
    }

    [Fact(Skip = "M6-T4")]
    public void DroughtDrainsRiver() // water.md scenario 6, ECO-17
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        sim.RunTicks(5 * Core.SimClock.TicksPerDay);
        long wet = sim.Water.TotalVolume();
        sim.RunTicks(Core.SimClock.TicksPerDay);
        Assert.True(sim.Water.TotalVolume() <= wet * 30 / 100, "river did not drain during drought");
    }
}
