using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Meshing;
using Xunit;

namespace Aurvangar.Sim.Tests.Meshing;

public class WaterMesherTests
{
    private const int Full = WaterGrid.Full;

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void QuadsWoundCounterClockwiseFromNormalSide()
    {
        var sim = new ScenarioBuilder().Ground(4).Layer(new Int3(1, 5, 1), "www", "www", "www").Build();
        var m = WaterMesher.Build(sim.World, sim.Water, 0, 0, 0, sliceY: 31);
        for (int q = 0; q < m.QuadCount; q++)
        {
            var a = m.Positions[m.Indices[q * 6]];
            var b = m.Positions[m.Indices[q * 6 + 1]];
            var c = m.Positions[m.Indices[q * 6 + 2]];
            var n = System.Numerics.Vector3.Cross(b - a, c - a);
            Assert.True(System.Numerics.Vector3.Dot(n, m.Normals[q * 4]) > 0, $"quad {q} winds clockwise");
        }
    }

    [Fact]
    public void SideQuad_SpansLevelDifferenceToLowerNeighbor()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Layer(5, "SSSS", "SWwS", "SSSS")
            .Build();
        var m = WaterMesher.Build(sim.World, sim.Water, 0, 0, 0, sliceY: 31);
        Assert.Equal(3, m.QuadCount); // 2 tops + 1 side from the full cell down to the half cell
        var sideYs = Enumerable.Range(0, m.QuadCount).Where(q => m.Normals[q * 4].Y == 0)
            .SelectMany(q => Enumerable.Range(q * 4, 4)).Select(i => m.Positions[i].Y).ToList();
        Assert.Equal(4, sideYs.Count);
        Assert.Equal(5.5f, sideYs.Min(), 3);
        Assert.Equal(6f, sideYs.Max(), 3);
    }

    [Fact]
    public void FreeStandingColumn_SidesCoverWholeHeight()
    {
        // A 1x1 column, full at y=5 with half at y=6, standing in open air: no gaps on the sides.
        var sim = new ScenarioBuilder().Ground(4)
            .Water(new Int3(3, 5, 3), Full).Water(new Int3(3, 6, 3), Full / 2).Build();
        var m = WaterMesher.Build(sim.World, sim.Water, 0, 0, 0, sliceY: 31);
        Assert.Equal(1 + 8, m.QuadCount);
        float area = 0;
        for (int q = 0; q < m.QuadCount; q++)
            if (m.Normals[q * 4].Y == 0) area += m.Positions[q * 4 + 1].Y - m.Positions[q * 4].Y + m.Positions[q * 4 + 3].Y - m.Positions[q * 4].Y;
        Assert.Equal(4 * 1.5f, area, 3);
    }

    [Fact]
    public void Slice_HidesWaterAbove_AndCellAtSliceBecomesSurface()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Layer(5, "SSSSS", "SWWWS", "SWWWS", "SWWWS", "SSSSS")
            .Layer(6, "SSSSS", "SwwwS", "SwwwS", "SwwwS", "SSSSS")
            .Build();
        var m = WaterMesher.Build(sim.World, sim.Water, 0, 0, 0, sliceY: 5);
        Assert.Equal(9, m.QuadCount);
        Assert.All(m.Positions, p => Assert.Equal(6f, p.Y, 3));
        Assert.True(WaterMesher.Build(sim.World, sim.Water, 0, 0, 0, sliceY: 4).IsEmpty);
    }

    [Fact]
    public void NeighborInOtherChunk_IsReadFromWorld()
    {
        // Water at x=31 (chunk 0) next to water at x=32 (chunk 1), same level: no side quad on the chunk border.
        var sim = new ScenarioBuilder(sizeX: 64).Ground(4)
            .Water(new Int3(31, 5, 3), Full / 2).Water(new Int3(32, 5, 3), Full / 2).Build();
        var left = WaterMesher.Build(sim.World, sim.Water, 0, 0, 0, sliceY: 31);
        var right = WaterMesher.Build(sim.World, sim.Water, 1, 0, 0, sliceY: 31);
        Assert.Equal(1 + 3, left.QuadCount);
        Assert.Equal(1 + 3, right.QuadCount);
        Assert.All(left.Positions, p => Assert.True(p.X <= 32));
        Assert.All(right.Positions, p => Assert.True(p.X >= 32));
    }

    [Fact]
    public void Color_DarkerWithDepth_AndSemiTransparent()
    {
        var colors = new WaterColors(TestContent.Db);
        var shallow = new ScenarioBuilder().Ground(4).Layer(5, "SSS", "SwS", "SSS").Build();
        var deep = new ScenarioBuilder().Ground(2)
            .Layer(3, "SSS", "SWS", "SSS").Layer(4, "SSS", "SWS", "SSS").Layer(5, "SSS", "SwS", "SSS").Build();
        var cs = WaterMesher.Build(shallow.World, shallow.Water, 0, 0, 0, 31, colors).Colors[0];
        var cd = WaterMesher.Build(deep.World, deep.Water, 0, 0, 0, 31, colors).Colors[0];
        Assert.True(cd.X + cd.Y + cd.Z < cs.X + cs.Y + cs.Z);
        Assert.Equal(TestContent.Db.Palette.Water.Alpha, cs.W, 3);
        Assert.Equal(colors.Deep, colors.ForDepth(10 * Full));
        Assert.Equal(colors.Shallow, colors.ForDepth(0));
    }
}
