using Aurvangar.Sim.Core;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

public class TerrainGeneratorTests
{
    private static VoxelWorld NewWorld() => new(WorldFactory.SizeX, WorldFactory.SizeY, WorldFactory.SizeZ, TestContent.Db.SolidTable);

    [Fact(Skip = "M1-T3")]
    public void SameSeed_IdenticalBlocks()
    {
        var a = NewWorld(); var b = NewWorld();
        TerrainGenerator.Generate(a, 1);
        TerrainGenerator.Generate(b, 1);
        Assert.True(a.Blocks.SequenceEqual(b.Blocks));
    }

    [Fact(Skip = "M1-T3")]
    public void DifferentSeed_DifferentBlocks()
    {
        var a = NewWorld(); var b = NewWorld();
        TerrainGenerator.Generate(a, 1);
        TerrainGenerator.Generate(b, 2);
        Assert.False(a.Blocks.SequenceEqual(b.Blocks));
    }

    [Fact(Skip = "M1-T3")]
    public void Heights_WithinSpecRange() // GEN-01, GEN-02
    {
        var w = NewWorld();
        var r = TerrainGenerator.Generate(w, 1);
        Assert.Equal(w.SizeX * w.SizeZ, r.Heights.Length);
        Assert.All(r.Heights, h => Assert.InRange(h, 10, 48)); // river bed can dip below 18
        Assert.True(r.Heights.Max() >= 34, "hill should rise well above base terrain");
    }

    [Fact(Skip = "M1-T3")]
    public void River_CrossesMap_WithSourcesAndDrains() // GEN-03, GEN-08
    {
        var w = NewWorld();
        var r = TerrainGenerator.Generate(w, 1);
        Assert.NotEmpty(r.WaterSources);
        Assert.NotEmpty(r.WaterDrains);
        Assert.All(r.WaterSources, s => Assert.Equal(0, s.X));
        Assert.All(r.WaterDrains, d => Assert.Equal(w.SizeX - 1, d.X));
        Assert.NotEmpty(r.InitialWater);
    }

    [Fact(Skip = "M1-T3")]
    public void Plants_CountsAndSpacing() // GEN-06, GEN-07
    {
        var w = NewWorld();
        var r = TerrainGenerator.Generate(w, 1);
        Assert.InRange(r.TreeBases.Count, 120, 150);
        Assert.Equal(24, r.BushBases.Count);
        for (int i = 0; i < r.TreeBases.Count; i++)
            for (int j = i + 1; j < r.TreeBases.Count; j++)
                Assert.True(r.TreeBases[i].ChebyshevXZ(r.TreeBases[j]) >= 4, "trees closer than min spacing");
        foreach (var t in r.TreeBases)
        {
            Assert.Equal(BlockId.Air, w.GetBlock(t));
            Assert.Equal(BlockId.Grass, w.GetBlock(t + Int3.Down));
        }
    }
}

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

public class GoldenHashTests
{
    private static readonly long[] Checkpoints = { 0, 1200, 3000, 6000 };

    private static ulong[] RunSeed1()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        // M5-T7 / M6-T6: apply Scripts.SurvivalScript here once it exists.
        var hashes = new List<ulong>();
        long last = 0;
        foreach (var cp in Checkpoints)
        {
            sim.RunTicks((int)(cp - last));
            last = cp;
            hashes.Add(sim.StateHash());
        }
        return hashes.ToArray();
    }

    [Fact(Skip = "M1-T4")]
    [Trait("Category", "Golden")]
    public void TwoRunsMatch()
    {
        Assert.Equal(RunSeed1(), RunSeed1());
    }

    [Fact(Skip = "M1-T4")]
    [Trait("Category", "Golden")]
    public void MatchesGoldenFile()
    {
        var path = Path.Combine(TestContent.RepoRoot, "tests", "golden", "seed1.txt");
        var actual = RunSeed1().Select((h, i) => $"{Checkpoints[i]} {h:x16}").ToArray();
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            File.WriteAllLines(path, actual);
            return;
        }
        Assert.True(File.Exists(path), $"Golden file missing: {path}. Run with UPDATE_GOLDEN=1 once, and log it in PROGRESS.md.");
        Assert.Equal(File.ReadAllLines(path), actual);
    }
}
