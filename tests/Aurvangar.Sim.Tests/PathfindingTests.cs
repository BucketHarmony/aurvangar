using Aurvangar.Sim.Core;
using Aurvangar.Sim.Paths;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

public class PathGridTests
{
    // These pass with the scaffold's uncached PathGrid. M4-T1 adds caching; keep them green.

    [Fact]
    public void Standable_RequiresFloorAndHeadroom() // PTH-01
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        Assert.True(sim.PathGrid.IsStandable(new Int3(3, 5, 3)));
        Assert.False(sim.PathGrid.IsStandable(new Int3(3, 6, 3)));   // no floor
        Assert.False(sim.PathGrid.IsStandable(new Int3(3, 4, 3)));   // solid
        sim.World.SetBlock(new Int3(3, 6, 3), BlockId.Stone);
        Assert.False(sim.PathGrid.IsStandable(new Int3(3, 5, 3)));   // no headroom
    }

    [Fact]
    public void Standable_FalseOnTreeTrunk() // WLD-07
    {
        var sim = new ScenarioBuilder().Ground(4).Layer(5, "..T").Build();
        Assert.False(sim.PathGrid.IsStandable(new Int3(2, 5, 0)));
        Assert.True(sim.PathGrid.IsStandable(new Int3(1, 5, 0)));
    }

    [Fact]
    public void Walkable_FalseInDeepWater() // PTH-02, WAT-14
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var c = new Int3(3, 5, 3);
        sim.Water.SetLevel(c, WaterGrid.Full / 2 - 1);
        Assert.True(sim.PathGrid.IsWalkable(c));
        sim.Water.SetLevel(c, WaterGrid.Full / 2);
        Assert.False(sim.PathGrid.IsWalkable(c));
    }
}

public class PathfinderTests
{
    /// <summary>Solid to y=6, 2-high corridor carved at y=5..6 along z=5 from x=1..10.</summary>
    private static ScenarioBuilder Corridor() =>
        new ScenarioBuilder().Ground(6).FillBox(new Int3(1, 5, 5), new Int3(10, 6, 5), BlockId.Air);

    [Fact]
    public void OpenFloor_StraightLineCost() // PTH-07, PTH-12
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var r = sim.Pathfinder.FindPath(new Int3(1, 5, 1), new Int3(6, 5, 1));
        Assert.Equal(PathStatus.Found, r.Status);
        Assert.Equal(6, r.Path.Length);
        Assert.Equal(50, r.Cost);
        Assert.Equal(new Int3(1, 5, 1), r.Path[0]);
        Assert.Equal(new Int3(6, 5, 1), r.Path[^1]);
    }

    [Fact]
    public void OpenFloor_UsesDiagonals()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var r = sim.Pathfinder.FindPath(new Int3(1, 5, 1), new Int3(4, 5, 4));
        Assert.Equal(42, r.Cost);
    }

    [Fact]
    public void StepUpOne_Found_StepUpTwo_NoPath() // PTH-04, PTH-05
    {
        var one = new ScenarioBuilder().Ground(4).FillBox(new Int3(5, 5, 0), new Int3(31, 5, 31), BlockId.Stone).Build();
        var r1 = one.Pathfinder.FindPath(new Int3(2, 5, 2), new Int3(7, 6, 2));
        Assert.Equal(PathStatus.Found, r1.Status);
        Assert.Equal(56, r1.Cost);

        var two = new ScenarioBuilder().Ground(4).FillBox(new Int3(5, 5, 0), new Int3(31, 6, 31), BlockId.Stone).Build();
        Assert.Equal(PathStatus.NoPath, two.Pathfinder.FindPath(new Int3(2, 5, 2), new Int3(7, 7, 2)).Status);
    }

    [Fact]
    public void NoCornerCutting() // PTH-06
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        sim.World.SetBlock(new Int3(2, 5, 1), BlockId.Stone);
        var r = sim.Pathfinder.FindPath(new Int3(1, 5, 1), new Int3(2, 5, 2));
        Assert.Equal(PathStatus.Found, r.Status);
        Assert.Equal(20, r.Cost);
        Assert.Equal(3, r.Path.Length);
    }

    [Fact]
    public void DropOfTwo_NotTraversed() // PTH-08
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        sim.World.SetBlock(new Int3(10, 4, 10), BlockId.Air);
        sim.World.SetBlock(new Int3(10, 3, 10), BlockId.Air);
        Assert.Equal(PathStatus.NoPath, sim.Pathfinder.FindPath(new Int3(8, 5, 10), new Int3(10, 3, 10)).Status);
    }

    [Fact]
    public void DeepWater_Blocks_WadeableCostsExtra() // PTH-02, PTH-07
    {
        var sim = Corridor().Build();
        var start = new Int3(1, 5, 5); var goal = new Int3(10, 5, 5);
        sim.Water.SetLevel(new Int3(5, 5, 5), WaterGrid.Full / 2);
        Assert.Equal(PathStatus.NoPath, sim.Pathfinder.FindPath(start, goal).Status);
        sim.Water.SetLevel(new Int3(5, 5, 5), WaterGrid.Full / 4);
        var r = sim.Pathfinder.FindPath(start, goal);
        Assert.Equal(PathStatus.Found, r.Status);
        Assert.Equal(90 + Pathfinder.CostWade, r.Cost);
    }

    [Fact]
    public void MultiGoal_PicksCheapest() // PTH-11
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var r = sim.Pathfinder.FindPath(new Int3(5, 5, 5), new[] { new Int3(20, 5, 5), new Int3(8, 5, 5) });
        Assert.Equal(new Int3(8, 5, 5), r.Path[^1]);
    }

    [Fact]
    public void SameInputs_SamePath() // PTH-09 determinism
    {
        var a = new ScenarioBuilder().Ground(4).Build();
        var b = new ScenarioBuilder().Ground(4).Build();
        Assert.Equal(a.Pathfinder.FindPath(new Int3(1, 5, 1), new Int3(20, 5, 13)).Path,
                     b.Pathfinder.FindPath(new Int3(1, 5, 1), new Int3(20, 5, 13)).Path);
    }
}

public class RegionTests
{
    [Fact]
    public void WallSeparates_DigMerges() // PTH-13
    {
        var sim = new ScenarioBuilder().Ground(4)
            .FillBox(new Int3(10, 5, 0), new Int3(10, 6, 31), BlockId.Stone)
            .Build();
        sim.Tick();
        var left = new Int3(5, 5, 5); var right = new Int3(15, 5, 5);
        Assert.NotEqual(sim.Regions.RegionOf(left), sim.Regions.RegionOf(right));

        sim.World.SetBlock(new Int3(10, 5, 5), BlockId.Air);
        sim.World.SetBlock(new Int3(10, 6, 5), BlockId.Air);
        sim.Tick();
        Assert.Equal(sim.Regions.RegionOf(left), sim.Regions.RegionOf(right));
    }

    [Fact]
    public void NonWalkableCell_HasNoRegion()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        sim.Tick();
        Assert.Equal(Regions.None, sim.Regions.RegionOf(new Int3(5, 4, 5)));
        Assert.NotEqual(Regions.None, sim.Regions.RegionOf(new Int3(5, 5, 5)));
    }
}
