using Aurvangar.Sim.Core;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

public class TerrainGeneratorTests
{
    private static VoxelWorld NewWorld() => new(WorldFactory.SizeX, WorldFactory.SizeY, WorldFactory.SizeZ, TestContent.Db.SolidTable);

    [Fact]
    public void SameSeed_IdenticalBlocks()
    {
        var a = NewWorld(); var b = NewWorld();
        TerrainGenerator.Generate(a, 1);
        TerrainGenerator.Generate(b, 1);
        Assert.True(a.Blocks.SequenceEqual(b.Blocks));
    }

    [Fact]
    public void DifferentSeed_DifferentBlocks()
    {
        var a = NewWorld(); var b = NewWorld();
        TerrainGenerator.Generate(a, 1);
        TerrainGenerator.Generate(b, 2);
        Assert.False(a.Blocks.SequenceEqual(b.Blocks));
    }

    [Fact]
    public void Heights_WithinSpecRange() // GEN-01, GEN-02
    {
        var w = NewWorld();
        var r = TerrainGenerator.Generate(w, 1);
        Assert.Equal(w.SizeX * w.SizeZ, r.Heights.Length);
        Assert.All(r.Heights, h => Assert.InRange(h, 10, 48)); // river bed can dip below 18
        Assert.True(r.Heights.Max() >= 34, "hill should rise well above base terrain");
    }

    [Fact]
    public void River_CrossesMap_WithSourcesAndDrains() // GEN-03, GEN-08
    {
        var w = NewWorld();
        var r = TerrainGenerator.Generate(w, 1);
        Assert.NotEmpty(r.WaterSources);
        Assert.NotEmpty(r.WaterDrains);
        // ADR-021: inlet at x=0 plus spring columns every RiverSpringSpacing cells, never in the drain column.
        Assert.Contains(r.WaterSources, s => s.X == 0);
        Assert.All(r.WaterSources, s => Assert.Equal(0, s.X % TerrainShape.RiverSpringSpacing));
        Assert.All(r.WaterSources, s => Assert.True(s.X < w.SizeX - 1));
        Assert.True(r.WaterSources.Select(s => s.X).Distinct().Count() > 1, "no springs along the channel");
        Assert.All(r.WaterDrains, d => Assert.Equal(w.SizeX - 1, d.X));
        Assert.NotEmpty(r.InitialWater);
    }

    [Fact]
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

    [Theory]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    public void Plants_CountsHoldAcrossSeeds(ulong seed) // GEN-06, GEN-07
    {
        var r = TerrainGenerator.Generate(NewWorld(), seed);
        Assert.InRange(r.TreeBases.Count, 120, 150);
        Assert.Equal(24, r.BushBases.Count);
    }

    [Fact]
    public void HubOnSpawnFlat_BushesNearRiverAndHub() // GEN-05, GEN-07
    {
        var w = NewWorld();
        var r = TerrainGenerator.Generate(w, 1);
        Assert.Equal((40, 60), (r.SpawnX, r.SpawnZ)); // river is far enough on seed 1: no shift
        int flat = r.Heights[r.SpawnX + r.SpawnZ * w.SizeX];
        for (int z = r.SpawnZ - 4; z <= r.SpawnZ + 4; z++)
            for (int x = r.SpawnX - 4; x <= r.SpawnX + 4; x++)
                Assert.Equal(flat, r.Heights[x + z * w.SizeX]);
        Assert.Equal(flat, r.SpawnSurfaceY);
        Assert.Equal(new Int3(39, flat + 1, 59), r.HubOrigin(TestContent.Db.Building("hub").Footprint));
        var water = r.InitialWater.ToHashSet();
        foreach (var b in r.BushBases)
        {
            Assert.Equal(BlockId.Grass, w.GetBlock(b + Int3.Down));
            Assert.True(b.ChebyshevXZ(new Int3(r.SpawnX, 0, r.SpawnZ)) <= 30, "bush too far from hub");
            Assert.DoesNotContain(b, water);
        }
        Assert.All(r.InitialWater, c => Assert.Equal(BlockId.Air, w.GetBlock(c)));
    }
}
