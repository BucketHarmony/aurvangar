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

    [Fact]
    public void SingleBlock_SixQuads()
    {
        var w = World();
        w.SetBlock(new Int3(5, 5, 5), BlockId.Stone);
        var m = ChunkMesher.Build(w, 0, 0, 0, sliceY: 31, Colors);
        Assert.Equal(6, m.QuadCount);
        Assert.Equal(24, m.Positions.Count);
        Assert.Equal(36, m.Indices.Count);
    }

    [Fact]
    public void Slab_MergesToSixQuads()
    {
        var w = World();
        for (int z = 2; z < 6; z++) for (int x = 2; x < 6; x++) w.SetBlock(new Int3(x, 5, z), BlockId.Stone);
        Assert.Equal(6, ChunkMesher.Build(w, 0, 0, 0, 31, Colors).QuadCount);
    }

    [Fact]
    public void DifferentTypes_DoNotMerge()
    {
        var w = World();
        w.SetBlock(new Int3(1, 1, 1), BlockId.Stone);
        w.SetBlock(new Int3(2, 1, 1), BlockId.Dirt);
        // top 2, bottom 2, north 2, south 2, west 1, east 1; shared face culled
        Assert.Equal(10, ChunkMesher.Build(w, 0, 0, 0, 31, Colors).QuadCount);
    }

    [Fact]
    public void FacesCulledAcrossChunkBorder()
    {
        var w = World(64);
        w.SetBlock(new Int3(31, 5, 5), BlockId.Stone);
        w.SetBlock(new Int3(32, 5, 5), BlockId.Stone);
        Assert.Equal(5, ChunkMesher.Build(w, 0, 0, 0, 31, Colors).QuadCount);
        Assert.Equal(5, ChunkMesher.Build(w, 1, 0, 0, 31, Colors).QuadCount);
    }

    [Fact]
    public void BottomFacesAtWorldFloorCulled() // WLD-04: below the world is Bedrock
    {
        var w = World();
        w.SetBlock(new Int3(3, 0, 3), BlockId.Stone);
        Assert.Equal(5, ChunkMesher.Build(w, 0, 0, 0, 31, Colors).QuadCount);
    }

    [Fact]
    public void NormalsPointOutward()
    {
        var w = World();
        w.SetBlock(new Int3(5, 5, 5), BlockId.Stone);
        var m = ChunkMesher.Build(w, 0, 0, 0, 31, Colors);
        var center = new System.Numerics.Vector3(5.5f, 5.5f, 5.5f);
        for (int i = 0; i < m.Positions.Count; i++)
            Assert.True(System.Numerics.Vector3.Dot(m.Positions[i] - center, m.Normals[i]) > 0);
    }

    [Fact]
    public void QuadsWoundCounterClockwiseFromNormalSide() // MeshData.AddQuad contract
    {
        var w = World();
        for (int x = 2; x < 5; x++) for (int y = 2; y < 4; y++) w.SetBlock(new Int3(x, y, 7), BlockId.Dirt);
        var m = ChunkMesher.Build(w, 0, 0, 0, 31, Colors);
        Assert.Equal(6, m.QuadCount);
        for (int q = 0; q < m.QuadCount; q++)
        {
            var a = m.Positions[q * 4]; var b = m.Positions[q * 4 + 1]; var c = m.Positions[q * 4 + 2];
            var n = System.Numerics.Vector3.Cross(b - a, c - a);
            Assert.True(System.Numerics.Vector3.Dot(n, m.Normals[q * 4]) > 0);
        }
    }

    [Fact]
    public void FacesCulledAcrossVerticalChunkBorder_AndColorsFromPalette()
    {
        var w = new VoxelWorld(32, 64, 32, TestContent.Db.SolidTable);
        w.SetBlock(new Int3(4, 31, 4), BlockId.Stone);
        w.SetBlock(new Int3(4, 32, 4), BlockId.Grass);
        var lower = ChunkMesher.Build(w, 0, 0, 0, 63, Colors);
        var upper = ChunkMesher.Build(w, 0, 1, 0, 63, Colors);
        Assert.Equal(5, lower.QuadCount);
        Assert.Equal(5, upper.QuadCount);
        Assert.All(lower.Colors, c => Assert.Equal(Colors.Get(BlockId.Stone), c));
        Assert.All(upper.Colors, c => Assert.Equal(Colors.Get(BlockId.Grass), c));
        Assert.All(upper.Positions, p => Assert.InRange(p.Y, 32f, 33f));
    }

    [Fact]
    public void EmptyChunk_IsEmpty()
    {
        var w = World(64);
        w.SetBlock(new Int3(40, 5, 5), BlockId.Stone);
        Assert.True(ChunkMesher.Build(w, 0, 0, 0, 31, Colors).IsEmpty);
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

    [Fact]
    public void NoSlice_ColumnHasNoCutFaces()
    {
        var m = ChunkMesher.Build(Column(), 0, 0, 0, sliceY: 31, Colors);
        Assert.Equal(5, m.QuadCount);   // top + 4 merged sides; bottom culled
        Assert.Equal(0, m.CutQuadCount);
    }

    [Fact]
    public void Slice_CutsColumnWithOneCutFace() // VIEW-04
    {
        var m = ChunkMesher.Build(Column(), 0, 0, 0, sliceY: 5, Colors);
        Assert.Equal(5, m.QuadCount);
        Assert.Equal(1, m.CutQuadCount);
        Assert.All(m.Positions, p => Assert.True(p.Y <= 6f));
    }

    [Fact]
    public void BlocksAboveSlice_Hidden()
    {
        var w = new VoxelWorld(32, 32, 32, TestContent.Db.SolidTable);
        w.SetBlock(new Int3(5, 20, 5), BlockId.Stone);
        Assert.Equal(0, ChunkMesher.Build(w, 0, 0, 0, sliceY: 10, Colors).QuadCount);
    }

    [Fact] // ADR-016: a top face at the slice with Air above in the real world is a natural surface, not a cut
    public void Slice_AtNaturalSurface_NoCutFace()
    {
        var m = ChunkMesher.Build(Column(), 0, 0, 0, sliceY: 10, Colors);
        Assert.Equal(5, m.QuadCount);
        Assert.Equal(0, m.CutQuadCount);
    }

    [Fact]
    public void CutAndUncutTops_DoNotMerge_AndCutIsDarkened()
    {
        var w = new VoxelWorld(32, 32, 32, TestContent.Db.SolidTable);
        for (int x = 2; x < 6; x++) w.SetBlock(new Int3(x, 5, 2), BlockId.Stone);
        w.SetBlock(new Int3(2, 6, 2), BlockId.Stone);
        w.SetBlock(new Int3(3, 6, 2), BlockId.Stone);
        var m = ChunkMesher.Build(w, 0, 0, 0, sliceY: 5, Colors);
        // top: 1 cut (x 2..3) + 1 uncut (x 4..5); bottom, north, south, west, east: 1 each
        Assert.Equal(7, m.QuadCount);
        Assert.Equal(1, m.CutQuadCount);
        int cutTops = 0;
        for (int q = 0; q < m.QuadCount; q++)
        {
            if (m.Colors[q * 4] != Colors.GetCut(BlockId.Stone)) continue;
            cutTops++;
            Assert.Equal(System.Numerics.Vector3.UnitY, m.Normals[q * 4]);
            Assert.All(m.Positions.GetRange(q * 4, 4), p => { Assert.Equal(6f, p.Y); Assert.InRange(p.X, 2f, 4f); });
        }
        Assert.Equal(1, cutTops);
    }

    [Fact]
    public void Slice_AtChunkTop_CutUsesCellInChunkAbove()
    {
        var w = new VoxelWorld(32, 64, 32, TestContent.Db.SolidTable);
        w.SetBlock(new Int3(4, 31, 4), BlockId.Stone);
        w.SetBlock(new Int3(4, 32, 4), BlockId.Stone);
        var lower = ChunkMesher.Build(w, 0, 0, 0, sliceY: 31, Colors);
        Assert.Equal(6, lower.QuadCount);
        Assert.Equal(1, lower.CutQuadCount);
        Assert.True(ChunkMesher.Build(w, 0, 1, 0, sliceY: 31, Colors).IsEmpty);
    }
}
