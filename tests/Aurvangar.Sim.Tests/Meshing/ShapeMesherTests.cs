using System.Numerics;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Meshing;
using Xunit;

namespace Aurvangar.Sim.Tests.Meshing;

/// <summary>M11-T11 (VIEW-27, CON-19): fine shapes drawn from 0.25 m sub-cells.</summary>
[Trait("Category", "Unit")]
public class ShapeMesherTests
{
    private static readonly BlockColors Colors = new(TestContent.Db);
    private static readonly BlockForm Slab = new(BlockShape.Slab, 0);
    private static readonly BlockForm Pillar = new(BlockShape.Pillar, 0);
    private static BlockForm Stair(byte rot) => new(BlockShape.Stair, rot);

    private static VoxelWorld World(int sx = 32) => new(sx, 32, 32, TestContent.Db.SolidTable);

    private static VoxelWorld One(Int3 c, BlockForm form)
    {
        var w = World();
        w.SetBlock(c, BlockId.Masonry);
        w.SetForm(c, form);
        return w;
    }

    private static float Area(MeshData m)
    {
        float a = 0;
        for (int q = 0; q < m.QuadCount; q++)
            a += Vector3.Cross(m.Positions[q * 4 + 1] - m.Positions[q * 4], m.Positions[q * 4 + 3] - m.Positions[q * 4]).Length();
        return a;
    }

    private static (Vector3 Min, Vector3 Max) Bounds(MeshData m) =>
        (m.Positions.Aggregate(Vector3.Min), m.Positions.Aggregate(Vector3.Max));

    [Fact]
    public void Patterns_MatchTheSpecGeometry()
    {
        Assert.Equal(64, ShapePattern.Count(ShapePattern.Of(BlockForm.Full)));
        var slab = ShapePattern.Of(Slab);
        Assert.Equal(32, ShapePattern.Count(slab));
        Assert.True(ShapePattern.Has(slab, 3, 1, 3));
        Assert.False(ShapePattern.Has(slab, 0, 2, 0));
        var pillar = ShapePattern.Of(Pillar);
        Assert.Equal(16, ShapePattern.Count(pillar));
        Assert.True(ShapePattern.Has(pillar, 1, 3, 2));
        Assert.False(ShapePattern.Has(pillar, 0, 0, 1));
        // Stairs: the whole lower half, and the upper half on the side it climbs toward.
        for (byte r = 0; r < 4; r++)
        {
            var p = ShapePattern.Of(Stair(r));
            Assert.Equal(48, ShapePattern.Count(p));
            Assert.True(ShapePattern.Has(p, 2, 1, 1));
            var hs = r switch { 0 => (1, 3), 1 => (3, 1), 2 => (1, 0), _ => (0, 1) };
            var ls = r switch { 0 => (1, 0), 1 => (0, 1), 2 => (1, 3), _ => (3, 1) };
            Assert.True(ShapePattern.Has(p, hs.Item1, 3, hs.Item2), $"rot {r} high side");
            Assert.False(ShapePattern.Has(p, ls.Item1, 3, ls.Item2), $"rot {r} low side");
        }
    }

    [Fact]
    public void Slab_IsSixQuads_HalfHigh()
    {
        var c = new Int3(5, 5, 5);
        var m = ChunkMesher.Build(One(c, Slab), 0, 0, 0, 31, Colors);
        Assert.Equal(6, m.QuadCount);
        var (min, max) = Bounds(m);
        Assert.Equal(new Vector3(5, 5, 5), min);
        Assert.Equal(new Vector3(6, 5.5f, 6), max);
        Assert.Equal(4f, Area(m), 3);
        Assert.All(m.Colors, col => Assert.Equal(Colors.Get(BlockId.Masonry), col));
    }

    [Fact]
    public void Pillar_IsSixQuads_InTheMiddle()
    {
        var m = ChunkMesher.Build(One(new Int3(5, 5, 5), Pillar), 0, 0, 0, 31, Colors);
        Assert.Equal(6, m.QuadCount);
        var (min, max) = Bounds(m);
        Assert.Equal(new Vector3(5.25f, 5, 5.25f), min);
        Assert.Equal(new Vector3(5.75f, 6, 5.75f), max);
        Assert.Equal(2.5f, Area(m), 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Stair_ClimbsTowardItsRotation(byte rot)
    {
        var m = ChunkMesher.Build(One(new Int3(5, 5, 5), Stair(rot)), 0, 0, 0, 31, Colors);
        Assert.Equal(5.5f, Area(m), 3);   // bottom 1, treads 0.5+0.5, back 1, front 0.5, riser 0.5, sides 0.75+0.75
        // The top tread (y = 6) lies on the high half.
        var dir = rot switch { 0 => Vector3.UnitZ, 1 => Vector3.UnitX, 2 => -Vector3.UnitZ, _ => -Vector3.UnitX };
        var center = new Vector3(5.5f, 0, 5.5f);
        bool sawTop = false, sawStep = false;
        for (int q = 0; q < m.QuadCount; q++)
        {
            if (m.Normals[q * 4] != Vector3.UnitY) continue;
            var mid = (m.Positions[q * 4] + m.Positions[q * 4 + 2]) / 2;
            float along = Vector3.Dot(mid - center, dir);
            if (mid.Y == 6f) { sawTop = true; Assert.True(along > 0, $"rot {rot}: top tread on the high side"); }
            if (mid.Y == 5.5f) { sawStep = true; Assert.True(along < 0, $"rot {rot}: step on the low side"); }
        }
        Assert.True(sawTop && sawStep);
        AssertWound(m);
    }

    [Fact]
    public void FullNeighbour_DrawsTheFaceTheShapeNoLongerCovers()
    {
        var w = World();
        w.SetBlock(new Int3(5, 5, 5), BlockId.Stone);
        w.SetBlock(new Int3(6, 5, 5), BlockId.Masonry);
        Assert.Equal(10, ChunkMesher.Build(w, 0, 0, 0, 31, Colors).QuadCount);   // two full blocks, shared face culled
        w.SetForm(new Int3(6, 5, 5), Slab);
        var m = ChunkMesher.Build(w, 0, 0, 0, 31, Colors);
        // The stone draws all 6 faces (its +X face shows above the slab); the slab hides its -X face against the stone.
        Assert.Equal(11, m.QuadCount);
        Assert.Equal(6f + 3.5f, Area(m), 3);
        Assert.Contains(Enumerable.Range(0, m.QuadCount), q => m.Normals[q * 4] == Vector3.UnitX && m.Positions[q * 4].X == 6f
            && m.Colors[q * 4] == Colors.Get(BlockId.Stone));
    }

    [Fact]
    public void ShapeOnFullBlock_HidesItsBottom_AndTheBlockKeepsItsTop()
    {
        var w = World();
        w.SetBlock(new Int3(5, 5, 5), BlockId.Stone);
        w.SetBlock(new Int3(5, 6, 5), BlockId.Masonry);
        w.SetForm(new Int3(5, 6, 5), Pillar);
        var m = ChunkMesher.Build(w, 0, 0, 0, 31, Colors);
        Assert.Equal(6 + 5, m.QuadCount);
        Assert.Equal(6f + 2.5f - 0.25f, Area(m), 3);
    }

    [Fact]
    public void AdjacentSlabs_CullTheirSharedSides()
    {
        var w = World();
        foreach (var x in new[] { 5, 6 })
        {
            w.SetBlock(new Int3(x, 5, 5), BlockId.Masonry);
            w.SetForm(new Int3(x, 5, 5), Slab);
        }
        var m = ChunkMesher.Build(w, 0, 0, 0, 31, Colors);
        Assert.Equal(10, m.QuadCount);
        Assert.Equal(2 * 4f - 2 * 0.5f, Area(m), 3);
        // A stair beside a slab: the slab's side is covered by the stair's lower half and the other way round.
        w.SetForm(new Int3(6, 5, 5), Stair(1));
        Assert.Equal(4f + 5.5f - 2 * 0.5f, Area(ChunkMesher.Build(w, 0, 0, 0, 31, Colors)), 3);
    }

    [Fact]
    public void ShapeAcrossChunkBorder_TheFullBlockDrawsItsFace()
    {
        var w = World(64);
        w.SetBlock(new Int3(31, 5, 5), BlockId.Stone);
        w.SetBlock(new Int3(32, 5, 5), BlockId.Masonry);
        w.SetForm(new Int3(32, 5, 5), Slab);
        var left = ChunkMesher.Build(w, 0, 0, 0, 31, Colors);
        var right = ChunkMesher.Build(w, 1, 0, 0, 31, Colors);
        Assert.Equal(6, left.QuadCount);
        Assert.Equal(5, right.QuadCount);
        Assert.All(right.Positions, p => Assert.InRange(p.X, 32f, 33f));
    }

    [Fact]
    public void Slice_HidesShapesAbove_AndCutsTheTopAtTheSlice()
    {
        var w = World();
        w.SetBlock(new Int3(5, 5, 5), BlockId.Masonry);
        w.SetForm(new Int3(5, 5, 5), Pillar);
        w.SetBlock(new Int3(5, 6, 5), BlockId.Masonry);
        w.SetForm(new Int3(5, 6, 5), Slab);
        Assert.True(ChunkMesher.Build(w, 0, 0, 0, 4, Colors).IsEmpty);
        var cut = ChunkMesher.Build(w, 0, 0, 0, 5, Colors);   // the slab above is cut away; the pillar's top is a cut face
        Assert.Equal(6, cut.QuadCount);
        Assert.Equal(1, cut.CutQuadCount);
        Assert.Contains(cut.Colors, c => c == Colors.GetCut(BlockId.Masonry));
        var all = ChunkMesher.Build(w, 0, 0, 0, 31, Colors);
        Assert.Equal(0, all.CutQuadCount);
        Assert.Equal(2.5f - 0.25f + 4f - 0.25f, Area(all), 3);
    }

    [Fact]
    public void EveryShape_NormalsOutward_AndWoundCounterClockwise()
    {
        foreach (var form in new[] { Slab, Pillar, Stair(0), Stair(1), Stair(2), Stair(3) })
            AssertWound(ChunkMesher.Build(One(new Int3(3, 3, 3), form), 0, 0, 0, 31, Colors));
    }

    [Fact]
    public void Inflated_AloneShape_ScalesAroundTheCell()
    {
        var m = new MeshData();
        ShapeMesher.EmitAlone(m, new Vector3(-0.1f, -0.1f, -0.1f), 1.2f, ShapePattern.Of(Slab), Vector4.One);
        var (min, max) = Bounds(m);
        Assert.Equal(-0.1f, min.Y, 4);
        Assert.Equal(0.5f, max.Y, 4);
        Assert.Equal(1.1f, max.X, 4);
    }

    private static void AssertWound(MeshData m)
    {
        for (int q = 0; q < m.QuadCount; q++)
        {
            var a = m.Positions[q * 4]; var b = m.Positions[q * 4 + 1]; var c = m.Positions[q * 4 + 2];
            Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), m.Normals[q * 4]) > 0);
        }
    }
}
