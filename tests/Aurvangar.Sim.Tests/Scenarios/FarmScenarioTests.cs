using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Farming;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M6-T2: crops on farm tiles (ECO-11..14, JOB-11). One agent, a stocked hub, dirt ground with its top at
/// y=4, and a one-cell water pit at (10,3,10) whose water cannot spread or evaporate: columns within 5 of it are
/// moist (ECO-15), columns further away are dry.</summary>
[Trait("Category", "Scenario")]
public class FarmScenarioTests
{
    internal static readonly Int3 Pit = new(10, 3, 10);
    internal static readonly Int3 MoistTile = new(12, 4, 10);
    internal static readonly Int3 DryTile = new(24, 4, 24);

    internal static Simulation FarmWorld(bool water = true, int hubPotatoRoom = -1)
    {
        var b = new ScenarioBuilder(32, 32, 32).Ground(4)
            .FillBox(new Int3(0, 4, 0), new Int3(31, 4, 31), BlockId.Dirt)
            .FillBox(Pit, Pit + Int3.Up, BlockId.Air)
            .Hub(new Int3(2, 5, 20)).Stock("water", 30).Stock("berries", 30)
            .Agent(new Int3(16, 5, 16));
        // Hub per-item cap is 100. Berries are topped up to the cap too, so the dwarf's meal is berries (a tie goes to
        // the lower item id, ECO-04 / ADR-057) and the potato room stays what the test asked for.
        if (hubPotatoRoom >= 0) b.Stock("potato", 100 - hubPotatoRoom).Stock("berries", 70);
        if (water) b.Water(Pit, WaterGrid.Full);
        return b.Build();
    }

    internal static FarmTile Tile(Simulation sim, Int3 c) => sim.Farms.Get(c) ?? throw new Xunit.Sdk.XunitException($"no farm tile at {c}");

    /// <summary>Ticks until the tile is planted (Growing); returns the number of ticks run.</summary>
    internal static int RunUntilPlanted(Simulation sim, Int3 c, int max = 600)
    {
        for (int i = 1; i <= max; i++)
        {
            sim.Tick();
            if (sim.Farms.Get(c) is { State: CropState.Growing }) return i;
        }
        throw new Xunit.Sdk.XunitException($"tile {c} not planted within {max} ticks");
    }

    private static int HubPotatoes(Simulation sim) =>
        sim.Buildings.All.First().Stored.GetValueOrDefault(TestContent.Db.Item("potato").Value);

    /// <summary>ECO-12: planted on a moist tile, the crop gains 1 progress per tick and is Mature exactly 7200 ticks
    /// after the planting tick.</summary>
    [Fact]
    public void MoistTile_MaturesAt7200()
    {
        var sim = FarmWorld();
        sim.Enqueue(new DesignateFarm(MoistTile.X, MoistTile.Z, MoistTile.X, MoistTile.Z));
        RunUntilPlanted(sim, MoistTile);
        Assert.Equal(0, Tile(sim, MoistTile).Progress);

        sim.RunTicks(FarmSystem.MatureTicks - 1);
        Assert.Equal(CropState.Growing, Tile(sim, MoistTile).State);
        Assert.Equal(FarmSystem.MatureTicks - 1, Tile(sim, MoistTile).Progress);
        Assert.True(sim.Moisture.IsMoist(MoistTile.X, MoistTile.Z));

        sim.Tick();
        Assert.Equal(CropState.Mature, Tile(sim, MoistTile).State);
    }

    /// <summary>ECO-12: on a dry tile the crop never makes progress (it withers and is replanted, never matures).</summary>
    [Fact]
    public void DryTile_NeverGrows()
    {
        var sim = FarmWorld();
        sim.Enqueue(new DesignateFarm(DryTile.X, DryTile.Z, DryTile.X, DryTile.Z));
        RunUntilPlanted(sim, DryTile);
        Assert.False(sim.Moisture.IsMoist(DryTile.X, DryTile.Z));

        bool replanted = false;
        var seen = CropState.Growing;
        for (int i = 0; i < 3 * FarmSystem.WitherTicks; i++)
        {
            sim.Tick();
            var t = Tile(sim, DryTile);
            Assert.Equal(0, t.Progress);
            Assert.NotEqual(CropState.Mature, t.State);
            if (seen == CropState.Empty && t.State == CropState.Growing) replanted = true;
            seen = t.State;
        }
        Assert.True(replanted, "a withered tile gets a new Plant job and is planted again");
    }

    /// <summary>ECO-13: a growing crop dry for 2400 continuous ticks withers to Empty with no yield.</summary>
    [Fact]
    public void DryForADay_Withers()
    {
        var sim = FarmWorld();
        sim.Enqueue(new DesignateFarm(DryTile.X, DryTile.Z, DryTile.X, DryTile.Z));
        RunUntilPlanted(sim, DryTile);

        sim.RunTicks(FarmSystem.WitherTicks - 1);
        Assert.Equal(CropState.Growing, Tile(sim, DryTile).State);
        Assert.Equal(FarmSystem.WitherTicks - 1, Tile(sim, DryTile).DryTicks);

        sim.Tick();
        Assert.Equal(CropState.Empty, Tile(sim, DryTile).State);
        Assert.Equal(0, Tile(sim, DryTile).DryTicks);
        Assert.Equal(0, HubPotatoes(sim));
        Assert.Equal(0, sim.Piles.Count);
        Assert.Contains(sim.Jobs.All, j => j.Kind == JobKind.Plant && j.Target == DryTile);
    }

    /// <summary>ECO-13: the dry spell must be continuous. A moist crop that dries out for less than a day and then
    /// gets water again keeps growing from where it stopped.</summary>
    [Fact]
    public void ShortDrySpell_DoesNotWither()
    {
        var sim = FarmWorld();
        sim.Enqueue(new DesignateFarm(MoistTile.X, MoistTile.Z, MoistTile.X, MoistTile.Z));
        RunUntilPlanted(sim, MoistTile);
        sim.RunTicks(500);
        int grown = Tile(sim, MoistTile).Progress;
        Assert.True(grown >= 500);

        sim.Water.SetLevel(Pit, 0);
        sim.RunTicks(2000);
        var t = Tile(sim, MoistTile);
        Assert.False(sim.Moisture.IsMoist(MoistTile.X, MoistTile.Z));
        Assert.Equal(CropState.Growing, t.State);
        Assert.True(t.DryTicks > 1900 && t.DryTicks < FarmSystem.WitherTicks, $"dry ticks {t.DryTicks}");
        int progressDry = t.Progress;
        Assert.True(progressDry < grown + 100, "no progress while dry (at most one recompute interval of lag)");

        sim.Water.SetLevel(Pit, WaterGrid.Full);
        sim.RunTicks(1000);
        t = Tile(sim, MoistTile);
        Assert.Equal(CropState.Growing, t.State);
        Assert.Equal(0, t.DryTicks);
        Assert.True(t.Progress > progressDry + 900);
    }

    /// <summary>ECO-12, JOB-11: harvesting a mature crop gives 3 potatoes, which the harvester carries straight to
    /// storage in the same job; the tile goes back to Empty and is planted again.</summary>
    [Fact]
    public void Harvest_Yields3Potatoes()
    {
        var sim = FarmWorld();
        sim.Enqueue(new DesignateFarm(MoistTile.X, MoistTile.Z, MoistTile.X, MoistTile.Z));
        RunUntilPlanted(sim, MoistTile);
        sim.RunTicks(FarmSystem.MatureTicks);
        Assert.Equal(CropState.Mature, Tile(sim, MoistTile).State);

        bool carried = false;
        for (int i = 0; i < 400 && HubPotatoes(sim) == 0; i++)
        {
            sim.Tick();
            var a = sim.Agents.All.First();
            if (a.Carried.Count == FarmSystem.Yield && a.Carried.Item == TestContent.Db.Item("potato")) carried = true;
            Assert.Equal(0, sim.Piles.Count);   // never dropped: delivered in the harvest job itself
            Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Haul);
        }
        Assert.True(carried);
        Assert.Equal(FarmSystem.Yield, HubPotatoes(sim));
        Assert.NotEqual(CropState.Mature, Tile(sim, MoistTile).State);

        RunUntilPlanted(sim, MoistTile);
        Assert.Equal(0, Tile(sim, MoistTile).Progress);
    }

    /// <summary>ECO-14: a mature crop is harvested even when no storage has room; the potatoes are then dropped as a
    /// pile next to the harvester, and the crop does not rot.</summary>
    [Fact]
    public void Harvest_StorageFull_DropsPile()
    {
        var sim = FarmWorld(hubPotatoRoom: 0);
        sim.Enqueue(new DesignateFarm(MoistTile.X, MoistTile.Z, MoistTile.X, MoistTile.Z));
        RunUntilPlanted(sim, MoistTile);
        sim.RunTicks(FarmSystem.MatureTicks);
        Assert.Equal(CropState.Mature, Tile(sim, MoistTile).State);
        Assert.Contains(sim.Jobs.All, j => j.Kind == JobKind.Harvest && j.Target == MoistTile);

        for (int i = 0; i < 400 && sim.Piles.Count == 0; i++) sim.Tick();
        var (cell, stack) = sim.Piles.All.Single();
        Assert.Equal(TestContent.Db.Item("potato"), stack.Item);
        Assert.Equal(FarmSystem.Yield, stack.Count);
        Assert.True(Math.Abs(cell.X - MoistTile.X) <= 4 && Math.Abs(cell.Z - MoistTile.Z) <= 4);
        Assert.Equal(100, HubPotatoes(sim));
    }
}
