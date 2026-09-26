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
