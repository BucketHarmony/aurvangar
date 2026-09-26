using Aurvangar.Sim.Core;
using Aurvangar.Sim.Paths;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>M4-T9: WAT-14 flee search (ADR-031): fewest-step path from a deep cell through deep standable cells to the
/// nearest walkable cell, at most <see cref="Pathfinder.FleeMaxSteps"/> steps.</summary>
public class FleeSearchTests
{
    /// <summary>A 1-wide corridor along x at z=5, y=4, walled 2 high on both sides; cells x 1..<paramref name="deepTo"/>
    /// hold deep water, x = deepTo+1 is dry.</summary>
    private static Simulation Corridor(int sizeX, int deepTo)
    {
        var b = new ScenarioBuilder(sizeX, 32, 32).Ground(3)
            .FillBox(new Int3(0, 4, 4), new Int3(sizeX - 1, 5, 4), BlockId.Stone)
            .FillBox(new Int3(0, 4, 6), new Int3(sizeX - 1, 5, 6), BlockId.Stone)
            .FillBox(new Int3(0, 4, 5), new Int3(0, 5, 5), BlockId.Stone);
        for (int x = 1; x <= deepTo; x++) b.Water(new Int3(x, 4, 5), WaterGrid.Full);
        return b.Build();
    }

    [Fact]
    public void SwimsThroughDeepCells_ToFirstDryCell()
    {
        var sim = Corridor(32, 11);
        var start = new Int3(2, 4, 5);
        Assert.Equal(PathStatus.NoPath, sim.Pathfinder.FindPath(start, new Int3(12, 4, 5)).Status); // A* never swims
        var r = sim.Pathfinder.FindFlee(start);
        Assert.Equal(PathStatus.Found, r.Status);
        Assert.Equal(start, r.Path[0]);
        Assert.Equal(new Int3(12, 4, 5), r.Path[^1]);
        Assert.Equal(11, r.Path.Length);
        for (int i = 1; i < r.Path.Length - 1; i++) Assert.True(sim.Water.IsDeep(r.Path[i]));
    }

    [Fact]
    public void ExactlyMaxSteps_Found_OneMore_NoPath()
    {
        var sim = Corridor(96, 70);   // dry at x = 71
        Assert.Equal(64, Pathfinder.FleeMaxSteps);
        var ok = sim.Pathfinder.FindFlee(new Int3(71 - 64, 4, 5));
        Assert.Equal(PathStatus.Found, ok.Status);
        Assert.Equal(65, ok.Path.Length);
        Assert.Equal(PathStatus.NoPath, sim.Pathfinder.FindFlee(new Int3(71 - 65, 4, 5)).Status);
    }

    [Fact]
    public void PicksNearestByStepCount()
    {
        // Deep x 4..11, dry at x = 3 and x = 12: each start flees to the end with fewer steps.
        var sim = new ScenarioBuilder(32, 32, 32).Ground(3)
            .FillBox(new Int3(0, 4, 4), new Int3(31, 5, 4), BlockId.Stone)
            .FillBox(new Int3(0, 4, 6), new Int3(31, 5, 6), BlockId.Stone)
            .Water(new Int3(4, 4, 5), WaterGrid.Full).Water(new Int3(5, 4, 5), WaterGrid.Full)
            .Water(new Int3(6, 4, 5), WaterGrid.Full).Water(new Int3(7, 4, 5), WaterGrid.Full)
            .Water(new Int3(8, 4, 5), WaterGrid.Full).Water(new Int3(9, 4, 5), WaterGrid.Full)
            .Water(new Int3(10, 4, 5), WaterGrid.Full).Water(new Int3(11, 4, 5), WaterGrid.Full)
            .Build();
        var r = sim.Pathfinder.FindFlee(new Int3(9, 4, 5));
        Assert.Equal(new Int3(12, 4, 5), r.Path[^1]);
        r = sim.Pathfinder.FindFlee(new Int3(5, 4, 5));
        Assert.Equal(new Int3(3, 4, 5), r.Path[^1]);
    }

    [Fact]
    public void WalkableStart_IsItsOwnGoal_NotStandable_Invalid()
    {
        var sim = Corridor(32, 11);
        var r = sim.Pathfinder.FindFlee(new Int3(14, 4, 5));
        Assert.Equal(PathStatus.Found, r.Status);
        Assert.Single(r.Path);
        Assert.Equal(PathStatus.InvalidStart, sim.Pathfinder.FindFlee(new Int3(14, 6, 5)).Status);
    }
}
