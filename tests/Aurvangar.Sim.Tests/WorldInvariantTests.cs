using Aurvangar.Sim.Core;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

public class WorldInvariantTests
{
    [Fact(Skip = "M1-T3")]
    public void EveryColumnHasBedrockAtZero()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        for (int z = 0; z < sim.World.SizeZ; z++)
            for (int x = 0; x < sim.World.SizeX; x++)
                Assert.Equal(BlockId.Bedrock, sim.World.GetBlock(x, 0, z));
    }

    [Fact(Skip = "M1-T3")]
    public void HubFootprintIsBuildingSolid_EntranceStandable() // GEN-05
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var hub = Assert.Single(sim.Buildings.All);
        Assert.All(hub.FootprintCells(), c => Assert.Equal(BlockId.BuildingSolid, sim.World.GetBlock(c)));
        Assert.True(sim.PathGrid.IsStandable(hub.EntranceCell));
    }

    [Fact(Skip = "M1-T3")]
    public void TreesRegisteredAsPlants()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        Assert.InRange(sim.Plants.All.Count(p => p.Kind == PlantKind.Tree), 120, 150);
        Assert.Equal(24, sim.Plants.All.Count(p => p.Kind == PlantKind.Bush));
    }

    [Fact(Skip = "M4-T3")]
    public void HubReachesRiverAndHill()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        sim.Tick(); // regions build at end of tick
        var hub = sim.Buildings.All.Single();
        int hubRegion = sim.Regions.RegionOf(hub.EntranceCell);
        Assert.NotEqual(Paths.Regions.None, hubRegion);
        // A cell on the river bank (standable, within 2 of wet cells) and a cell on the hill slope share the hub region.
        Placeholder.Write("pick a river-bank cell and a hill cell from TerrainResult and assert same region");
    }
}
