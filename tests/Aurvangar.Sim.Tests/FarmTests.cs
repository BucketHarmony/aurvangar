using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Farming;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Scenarios;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>M6-T2: farm designation (ECO-11), cancel (DSG-06), tile removal, save and hash (SAV-01, ARCH-06).</summary>
public class FarmTests
{
    /// <summary>ECO-11: in the XZ rectangle, each column's top solid cell becomes a farm tile (and Farmland at once)
    /// when it is Grass or Dirt and the cell above it is standable air. Stone, a tree's floor, a cell that is not
    /// its column's top, and building floors are skipped.</summary>
    [Fact]
    public void DesignateFarm_MarksTopSurfaceGrassAndDirt()
    {
        var sim = new ScenarioBuilder(32, 32, 32).Ground(4)
            .FillBox(new Int3(5, 4, 5), new Int3(8, 4, 5), BlockId.Dirt)
            .Layer(new Int3(5, 4, 6), "GGSG")                  // (7,4,6) stone
            .Layer(new Int3(5, 5, 6), "   D")                  // (8,5,6) dirt on top of grass: that is the surface
            .Layer(new Int3(5, 5, 5), " T  ")                  // tree standing on (6,4,5)
            .Layer(new Int3(5, 4, 7), "GS..")                  // (6,4,7) stone; (7..8,4,7) air over stone
            .FillBox(new Int3(5, 4, 9), new Int3(7, 4, 11), BlockId.Dirt)
            .Hub(new Int3(5, 5, 9))                            // footprint x 5..7, z 9..11 on dirt
            .Build();
        sim.Enqueue(new DesignateFarm(8, 11, 5, 5));   // corners in any order
        sim.Tick();

        var tiles = sim.Farms.All.Select(t => t.Cell).ToList();
        var expected = new[]
        {
            new Int3(5, 4, 5), new Int3(7, 4, 5), new Int3(8, 4, 5),
            new Int3(5, 4, 6), new Int3(6, 4, 6), new Int3(8, 5, 6),
            new Int3(5, 4, 7),
        };
        Assert.Equal(expected.OrderBy(c => sim.World.Index(c)), tiles);
        foreach (var c in expected) Assert.Equal(BlockId.Farmland, sim.World.GetBlock(c));
        Assert.Equal(BlockId.Dirt, sim.World.GetBlock(new Int3(6, 4, 5)));   // tree floor untouched
        Assert.Equal(BlockId.Grass, sim.World.GetBlock(new Int3(8, 4, 6)));  // not the column top
        Assert.All(sim.Farms.All, t => Assert.Equal(CropState.Empty, t.State));
    }

    [Fact]
    public void DesignateFarm_OutsideWorld_Rejected()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        sim.Enqueue(new DesignateFarm(40, 40, 50, 50));
        sim.Tick();
        Assert.Contains(sim.Events.Drain(), e => e is CommandRejected { Command: "DesignateFarm" });
        Assert.Equal(0, sim.Farms.Count);
    }

    /// <summary>ECO-11: an existing Farmland cell without a tile (left by a cancel) can be designated again;
    /// re-designating a tile leaves it as it is.</summary>
    [Fact]
    public void DesignateFarm_FarmlandAgain_And_Idempotent()
    {
        var sim = FarmScenarioTests.FarmWorld();
        var c = FarmScenarioTests.MoistTile;
        sim.Enqueue(new DesignateFarm(c.X, c.Z, c.X, c.Z));
        FarmScenarioTests.RunUntilPlanted(sim, c);
        sim.RunTicks(10);
        sim.Enqueue(new DesignateFarm(c.X, c.Z, c.X, c.Z));
        sim.Tick();
        Assert.Equal(CropState.Growing, FarmScenarioTests.Tile(sim, c).State);
        Assert.Equal(11, FarmScenarioTests.Tile(sim, c).Progress);

        var d = c + new Int3(1, 0, 0);
        sim.Enqueue(new DesignateFarm(d.X, d.Z, d.X, d.Z));
        sim.Enqueue(new CancelDesignation(d, d));
        sim.Tick();
        Assert.Null(sim.Farms.Get(d));
        Assert.Equal(BlockId.Farmland, sim.World.GetBlock(d));
        sim.Enqueue(new DesignateFarm(d.X, d.Z, d.X, d.Z));
        sim.Tick();
        Assert.NotNull(sim.Farms.Get(d));
    }

    /// <summary>DSG-06: cancel removes Empty tiles (Farmland stays) and cancels their Plant jobs, claimed or not;
    /// tiles with a crop stay.</summary>
    [Fact]
    public void Cancel_RemovesEmptyTiles_KeepsCrops()
    {
        var sim = FarmScenarioTests.FarmWorld();
        var c = FarmScenarioTests.MoistTile;
        var e = new Int3(20, 4, 20);
        sim.Enqueue(new DesignateFarm(c.X, c.Z, c.X, c.Z));
        FarmScenarioTests.RunUntilPlanted(sim, c);
        sim.Enqueue(new DesignateFarm(e.X, e.Z, e.X, e.Z));
        sim.Tick();
        Assert.Contains(sim.Jobs.All, j => j.Kind == JobKind.Plant && j.Target == e);

        sim.Enqueue(new CancelDesignation(new Int3(0, 0, 0), new Int3(31, 31, 31)));
        sim.Tick();
        Assert.Null(sim.Farms.Get(e));
        Assert.Equal(BlockId.Farmland, sim.World.GetBlock(e));
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Plant);
        Assert.Equal(CropState.Growing, FarmScenarioTests.Tile(sim, c).State);
        Assert.All(sim.Agents.All, a => Assert.NotEqual(JobKind.Plant, sim.Jobs.Get(a.CurrentJob)?.Kind));
    }

    /// <summary>A tile whose Farmland is dug away, or covered by a solid block, stops being a tile; its open job
    /// is withdrawn.</summary>
    [Fact]
    public void TileRemoved_WhenFarmlandGoneOrCovered()
    {
        var sim = FarmScenarioTests.FarmWorld();
        var a = new Int3(20, 4, 20);
        var b = new Int3(22, 4, 20);
        sim.Enqueue(new DesignateFarm(20, 20, 22, 20));
        sim.Tick();
        Assert.Equal(3, sim.Farms.Count);

        sim.World.SetBlock(a, BlockId.Air);
        sim.World.SetBlock(b + Int3.Up, BlockId.Stone);
        sim.Tick();
        Assert.Null(sim.Farms.Get(a));
        Assert.Null(sim.Farms.Get(b));
        Assert.NotNull(sim.Farms.Get(new Int3(21, 4, 20)));
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Plant && !j.IsClaimed && (j.Target == a || j.Target == b));
    }

    /// <summary>SAV-01/03, ARCH-06: farm tiles are saved and hashed; a load mid-growth hashes equal and stays equal.</summary>
    [Fact]
    public void SaveLoad_MidGrowth_HashEqual()
    {
        var sim = FarmScenarioTests.FarmWorld();
        var c = FarmScenarioTests.MoistTile;
        sim.Enqueue(new DesignateFarm(c.X, c.Z, c.X + 2, c.Z + 1));
        FarmScenarioTests.RunUntilPlanted(sim, c);
        sim.RunTicks(137);

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.Equal(sim.Farms.All.Select(t => (t.Cell, t.State, t.Progress, t.DryTicks)),
            loaded.Farms.All.Select(t => (t.Cell, t.State, t.Progress, t.DryTicks)));
        for (int i = 0; i < 10; i++)
        {
            sim.RunTicks(100);
            loaded.RunTicks(100);
            Assert.Equal(sim.StateHash(), loaded.StateHash());
        }
    }

    [Fact]
    public void FarmState_IsHashed()
    {
        var sim = FarmScenarioTests.FarmWorld();
        var c = FarmScenarioTests.MoistTile;
        sim.Enqueue(new DesignateFarm(c.X, c.Z, c.X, c.Z));
        FarmScenarioTests.RunUntilPlanted(sim, c);
        ulong h = sim.StateHash();
        FarmScenarioTests.Tile(sim, c).Progress++;
        Assert.NotEqual(h, sim.StateHash());
    }
}
