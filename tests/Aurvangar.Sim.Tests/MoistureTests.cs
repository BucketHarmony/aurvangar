using Aurvangar.Sim.Core;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>M6-T1: MoistureMap (ECO-15, ARCH-01 step 4, ADR-046).</summary>
public class MoistureTests
{
    /// <summary>Flat stone ground with its top at y=4 and one full water cell standing on it at (10,5,10).</summary>
    private static Simulation OnePuddle(int level = WaterGrid.Full) =>
        new ScenarioBuilder(32, 32, 32).Ground(4).Water(new Int3(10, 5, 10), level).Build();

    [Fact]
    public void Radius5Moist_Radius6Dry()
    {
        var sim = OnePuddle();
        sim.Moisture.Recompute();
        var m = sim.Moisture;

        Assert.True(m.IsMoist(10, 10));
        foreach (var (x, z) in new[] { (15, 10), (5, 10), (10, 15), (10, 5), (15, 15), (5, 5), (15, 5) })
            Assert.True(m.IsMoist(x, z), $"({x},{z}) is 5 columns from the water and must be moist");
        foreach (var (x, z) in new[] { (16, 10), (4, 10), (10, 16), (10, 4), (16, 16), (16, 12), (4, 4) })
            Assert.False(m.IsMoist(x, z), $"({x},{z}) is 6 columns from the water and must be dry");
        Assert.Equal(11 * 11, CountMoist(sim));
    }

    [Theory]
    [InlineData(127, false)]
    [InlineData(128, true)]
    public void LevelBelow128_DoesNotMoisten(int level, bool moist)
    {
        var sim = OnePuddle(level);
        sim.Moisture.Recompute();
        Assert.Equal(moist, sim.Moisture.IsMoist(12, 10));
    }

    /// <summary>The target column (12,10) gets surface heights 3..8 while the water stays at y=5, so the water sits
    /// at surfaceY + 2 .. surfaceY - 3. Only surfaceY - 2 .. surfaceY + 1 moistens.</summary>
    [Theory]
    [InlineData(3, false)]   // water at surface + 2
    [InlineData(4, true)]    // surface + 1 (standing on the ground)
    [InlineData(5, true)]    // surface
    [InlineData(7, true)]    // surface - 2
    [InlineData(8, false)]   // surface - 3
    public void HeightWindow_Respected(int surfaceY, bool moist)
    {
        var b = new ScenarioBuilder(32, 32, 32).Ground(4).Water(new Int3(10, 5, 10), WaterGrid.Full);
        if (surfaceY < 4) b.FillBox(new Int3(12, surfaceY + 1, 10), new Int3(12, 4, 10), BlockId.Air);
        else if (surfaceY > 4) b.FillBox(new Int3(12, 5, 10), new Int3(12, surfaceY, 10), BlockId.Stone);
        var sim = b.Build();
        sim.Moisture.Recompute();

        Assert.Equal(surfaceY, sim.Moisture.SurfaceY(12, 10));
        Assert.Equal(moist, sim.Moisture.IsMoist(12, 10));
    }

    [Fact]
    public void ColumnWithoutSolid_IsDry()
    {
        var sim = new ScenarioBuilder(32, 32, 32).Water(new Int3(3, 0, 3), WaterGrid.Full).Build();
        sim.Moisture.Recompute();
        Assert.Equal(-1, sim.Moisture.SurfaceY(4, 3));
        Assert.False(sim.Moisture.IsMoist(4, 3));
    }

    /// <summary>ARCH-01 step 4: the map is recomputed only in ticks whose number is a multiple of 50, after the water
    /// step. Water sits in a one-cell pit (10,3,10) so it cannot spread or evaporate.</summary>
    [Fact]
    public void RecomputesEvery50Ticks()
    {
        var sim = PitWorld();
        sim.Tick();                                   // tick 0 recomputes: dry
        Assert.False(sim.Moisture.IsMoist(11, 10));

        sim.Water.SetLevel(new Int3(10, 3, 10), WaterGrid.Full);
        sim.RunTicks(49);                              // ticks 1..49
        Assert.Equal(50, sim.Clock.Tick);
        Assert.False(sim.Moisture.IsMoist(11, 10));
        sim.Tick();                                    // tick 50
        Assert.True(sim.Moisture.IsMoist(11, 10));
        Assert.True(sim.Moisture.IsMoist(10, 10));

        sim.Water.SetLevel(new Int3(10, 3, 10), 0);
        sim.RunTicks(49);                              // ticks 51..99
        Assert.True(sim.Moisture.IsMoist(11, 10));
        sim.Tick();                                    // tick 100
        Assert.False(sim.Moisture.IsMoist(11, 10));
    }

    /// <summary>The map depends on the water at the last multiple of 50, so it is saved and hashed, not recomputed on
    /// load (ADR-046). A save between recomputes keeps the stale map and the hash.</summary>
    [Fact]
    public void SaveBetweenRecomputes_KeepsMapAndHash()
    {
        var sim = PitWorld();
        sim.Tick();
        sim.Water.SetLevel(new Int3(10, 3, 10), WaterGrid.Full);
        sim.RunTicks(24);                              // tick 25: water present, map still dry

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);

        Assert.False(loaded.Moisture.IsMoist(11, 10));
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        sim.RunTicks(26);                              // ticks 25..50
        loaded.RunTicks(26);
        Assert.True(loaded.Moisture.IsMoist(11, 10));
        Assert.Equal(sim.StateHash(), loaded.StateHash());
    }

    [Fact]
    public void MoistureIsHashed()
    {
        var a = OnePuddle();
        var b = OnePuddle();
        a.Moisture.Recompute();
        Assert.NotEqual(a.StateHash(), b.StateHash());
    }

    /// <summary>Seed 1 after settling: the river moistens its banks, the dry hill and the hub do not all count.</summary>
    [Fact]
    public void Seed1_RiverBanksMoist()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        sim.RunTicks(51);
        int moist = CountMoist(sim);
        int columns = sim.World.SizeX * sim.World.SizeZ;
        Assert.True(moist > 0 && moist < columns, $"{moist} of {columns} columns moist");
        var hub = sim.Buildings.All.First().Origin;
        Assert.False(sim.Moisture.IsMoist(hub.X, hub.Z), "the Great Hall is not next to the river");
    }

    private static Simulation PitWorld() =>
        new ScenarioBuilder(32, 32, 32).Ground(4).FillBox(new Int3(10, 3, 10), new Int3(10, 4, 10), BlockId.Air).Build();

    private static int CountMoist(Simulation sim)
    {
        int n = 0;
        for (int z = 0; z < sim.World.SizeZ; z++)
            for (int x = 0; x < sim.World.SizeX; x++)
                if (sim.Moisture.IsMoist(x, z)) n++;
        return n;
    }
}
