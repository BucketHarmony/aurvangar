using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Meshing;
using Xunit;

namespace Aurvangar.Sim.Tests.Meshing;

public class ChunkMesherTests
{
    private static readonly BlockColors Colors = new(TestContent.Db);

    private static VoxelWorld World(int sx = 32) => new(sx, 32, 32, TestContent.Db.SolidTable);

    [Fact(Skip = "M3-T1")]
    public void SingleBlock_SixQuads()
    {
        var w = World();
        w.SetBlock(new Int3(5, 5, 5), BlockId.Stone);
        var m = ChunkMesher.Build(w, 0, 0, 0, sliceY: 31, Colors);
        Assert.Equal(6, m.QuadCount);
        Assert.Equal(24, m.Positions.Count);
        Assert.Equal(36, m.Indices.Count);
    }

    [Fact(Skip = "M3-T1")]
    public void Slab_MergesToSixQuads()
    {
        var w = World();
        for (int z = 2; z < 6; z++) for (int x = 2; x < 6; x++) w.SetBlock(new Int3(x, 5, z), BlockId.Stone);
        Assert.Equal(6, ChunkMesher.Build(w, 0, 0, 0, 31, Colors).QuadCount);
    }

    [Fact(Skip = "M3-T1")]
    public void DifferentTypes_DoNotMerge()
    {
        var w = World();
        w.SetBlock(new Int3(1, 1, 1), BlockId.Stone);
        w.SetBlock(new Int3(2, 1, 1), BlockId.Dirt);
        // top 2, bottom 2, north 2, south 2, west 1, east 1; shared face culled
        Assert.Equal(10, ChunkMesher.Build(w, 0, 0, 0, 31, Colors).QuadCount);
    }

    [Fact(Skip = "M3-T1")]
    public void FacesCulledAcrossChunkBorder()
    {
        var w = World(64);
        w.SetBlock(new Int3(31, 5, 5), BlockId.Stone);
        w.SetBlock(new Int3(32, 5, 5), BlockId.Stone);
        Assert.Equal(5, ChunkMesher.Build(w, 0, 0, 0, 31, Colors).QuadCount);
        Assert.Equal(5, ChunkMesher.Build(w, 1, 0, 0, 31, Colors).QuadCount);
    }

    [Fact(Skip = "M3-T1")]
    public void BottomFacesAtWorldFloorCulled() // WLD-04: below the world is Bedrock
    {
        var w = World();
        w.SetBlock(new Int3(3, 0, 3), BlockId.Stone);
        Assert.Equal(5, ChunkMesher.Build(w, 0, 0, 0, 31, Colors).QuadCount);
    }

    [Fact(Skip = "M3-T1")]
    public void NormalsPointOutward()
    {
        var w = World();
        w.SetBlock(new Int3(5, 5, 5), BlockId.Stone);
        var m = ChunkMesher.Build(w, 0, 0, 0, 31, Colors);
        var center = new System.Numerics.Vector3(5.5f, 5.5f, 5.5f);
        for (int i = 0; i < m.Positions.Count; i++)
            Assert.True(System.Numerics.Vector3.Dot(m.Positions[i] - center, m.Normals[i]) > 0);
    }
}

public class SliceTests
{
    private static readonly BlockColors Colors = new(TestContent.Db);

    private static VoxelWorld Column()
    {
        var w = new VoxelWorld(32, 32, 32, TestContent.Db.SolidTable);
        for (int y = 0; y <= 10; y++) w.SetBlock(new Int3(5, y, 5), BlockId.Stone);
        return w;
    }

    [Fact(Skip = "M3-T2")]
    public void NoSlice_ColumnHasNoCutFaces()
    {
        var m = ChunkMesher.Build(Column(), 0, 0, 0, sliceY: 31, Colors);
        Assert.Equal(5, m.QuadCount);   // top + 4 merged sides; bottom culled
        Assert.Equal(0, m.CutQuadCount);
    }

    [Fact(Skip = "M3-T2")]
    public void Slice_CutsColumnWithOneCutFace() // VIEW-04
    {
        var m = ChunkMesher.Build(Column(), 0, 0, 0, sliceY: 5, Colors);
        Assert.Equal(5, m.QuadCount);
        Assert.Equal(1, m.CutQuadCount);
        Assert.All(m.Positions, p => Assert.True(p.Y <= 6f));
    }

    [Fact(Skip = "M3-T2")]
    public void BlocksAboveSlice_Hidden()
    {
        var w = new VoxelWorld(32, 32, 32, TestContent.Db.SolidTable);
        w.SetBlock(new Int3(5, 20, 5), BlockId.Stone);
        Assert.Equal(0, ChunkMesher.Build(w, 0, 0, 0, sliceY: 10, Colors).QuadCount);
    }
}

public class WaterMesherTests
{
    private const int Full = WaterGrid.Full;

    [Fact(Skip = "M3-T3")]
    public void EnclosedPool_TopQuadsOnly_AtLevelHeight()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Layer(5,
                "SSSSS",
                "SwwwS",
                "SwwwS",
                "SwwwS",
                "SSSSS")
            .Build();
        var m = WaterMesher.Build(sim.World, sim.Water, 0, 0, 0, sliceY: 31);
        Assert.Equal(9, m.QuadCount);
        Assert.All(m.Positions, p => Assert.Equal(5.5f, p.Y, 3));
    }

    [Fact(Skip = "M3-T3")]
    public void OpenPool_HasSideQuadsOnPerimeter()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Layer(new Int3(1, 5, 1),
                "www",
                "www",
                "www")
            .Build();
        var m = WaterMesher.Build(sim.World, sim.Water, 0, 0, 0, sliceY: 31);
        Assert.Equal(9 + 12, m.QuadCount);
    }

    [Fact(Skip = "M3-T3")]
    public void StackedWater_OnlyTopCellIsSurface()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Layer(5, "SSSSS", "SWWWS", "SWWWS", "SWWWS", "SSSSS")
            .Layer(6, "SSSSS", "SwwwS", "SwwwS", "SwwwS", "SSSSS")
            .Build();
        var m = WaterMesher.Build(sim.World, sim.Water, 0, 0, 0, sliceY: 31);
        Assert.Equal(9, m.QuadCount);
        Assert.All(m.Positions, p => Assert.Equal(6.5f, p.Y, 3));
    }
}
