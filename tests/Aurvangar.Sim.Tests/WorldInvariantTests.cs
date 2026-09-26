using Aurvangar.Sim.Core;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

public class WorldInvariantTests
{
    [Fact]
    public void EveryColumnHasBedrockAtZero()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        for (int z = 0; z < sim.World.SizeZ; z++)
            for (int x = 0; x < sim.World.SizeX; x++)
                Assert.Equal(BlockId.Bedrock, sim.World.GetBlock(x, 0, z));
    }

    [Fact]
    public void HubFootprintIsBuildingSolid_EntranceStandable() // GEN-05
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var hub = Assert.Single(sim.Buildings.All);
        Assert.All(hub.FootprintCells(), c => Assert.Equal(BlockId.BuildingSolid, sim.World.GetBlock(c)));
        Assert.True(sim.PathGrid.IsStandable(hub.EntranceCell));
    }

    [Fact]
    public void TreesRegisteredAsPlants()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        Assert.InRange(sim.Plants.All.Count(p => p.Kind == PlantKind.Tree), 120, 150);
        Assert.Equal(24, sim.Plants.All.Count(p => p.Kind == PlantKind.Bush));
    }

    [Fact]
    public void HubReachesRiverAndHill()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        sim.Tick(); // regions build at end of tick
        var hub = sim.Buildings.All.Single();
        int hubRegion = sim.Regions.RegionOf(hub.EntranceCell);
        Assert.NotEqual(Paths.Regions.None, hubRegion);
        // A cell on the river bank (standable, within 2 of wet cells) and a cell on the hill slope share the hub region.
        // TerrainResult is a pure function of (seed, size): regenerate it into a scratch world to read the layout.
        var terrain = TerrainGenerator.Generate(
            new VoxelWorld(WorldFactory.SizeX, WorldFactory.SizeY, WorldFactory.SizeZ, TestContent.Db.SolidTable), 1);
        var w = sim.World;

        // River bank on the hub's side: the walkable dry cell closest to the hub that has water within 2 cells.
        Int3? bank = null; int bankDist = int.MaxValue;
        var entrance = hub.EntranceCell;
        for (int z = 0; z < w.SizeZ; z++)
            for (int x = 0; x < w.SizeX; x++)
            {
                var c = new Int3(x, terrain.Heights[x + z * w.SizeX] + 1, z);
                if (!sim.PathGrid.IsWalkable(c) || sim.PathGrid.IsWet(c) || !WaterWithin2(sim, c)) continue;
                int d = Math.Abs(x - entrance.X) + Math.Abs(z - entrance.Z);
                if (d < bankDist) { bankDist = d; bank = c; }
            }
        Assert.NotNull(bank);
        Assert.Equal(hubRegion, sim.Regions.RegionOf(bank!.Value));

        // Hill slope: the highest walkable surface cell within half the hill radius of the hill center.
        Int3? hill = null;
        int r = TerrainShape.HillRadius / 2;
        for (int z = TerrainShape.HillZ - r; z <= TerrainShape.HillZ + r; z++)
            for (int x = TerrainShape.HillX - r; x <= TerrainShape.HillX + r; x++)
            {
                var c = new Int3(x, terrain.Heights[x + z * w.SizeX] + 1, z);
                if (sim.PathGrid.IsWalkable(c) && (hill == null || c.Y > hill.Value.Y)) hill = c;
            }
        Assert.NotNull(hill);
        Assert.True(hill!.Value.Y >= entrance.Y + 8, $"hill cell {hill} is not on the hill (entrance y {entrance.Y})");
        Assert.Equal(hubRegion, sim.Regions.RegionOf(hill.Value));
    }

    private static bool WaterWithin2(Simulation sim, Int3 c)
    {
        for (int dy = -2; dy <= 0; dy++)
            for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    var n = new Int3(c.X + dx, c.Y + dy, c.Z + dz);
                    if (sim.World.InBounds(n) && sim.Water.GetLevel(n) > 0) return true;
                }
        return false;
    }
}
