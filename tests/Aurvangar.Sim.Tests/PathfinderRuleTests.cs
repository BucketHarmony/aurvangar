using Aurvangar.Sim.Core;
using Aurvangar.Sim.Paths;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>M4-T2 rules beyond the written acceptance tests (PTH-05, 07, 09, 10, 11, WAT-14 flee start).</summary>
public class PathfinderRuleTests
{
    [Fact]
    public void StepDown_CostsTwoExtra() // PTH-07
    {
        var sim = new ScenarioBuilder().Ground(4).FillBox(new Int3(0, 5, 0), new Int3(4, 5, 31), BlockId.Stone).Build();
        var r = sim.Pathfinder.FindPath(new Int3(3, 6, 2), new Int3(6, 5, 2));
        Assert.Equal(PathStatus.Found, r.Status);
        Assert.Equal(30 + Pathfinder.CostStepDown, r.Cost);
    }

    [Fact]
    public void StepUp_NeedsHeadroomAboveStart() // PTH-05
    {
        var sim = new ScenarioBuilder().Ground(4).FillBox(new Int3(5, 5, 0), new Int3(31, 5, 31), BlockId.Stone)
            .FillBox(new Int3(4, 7, 0), new Int3(4, 7, 31), BlockId.Stone)   // low ceiling over the last lower cell
            .Build();
        Assert.Equal(PathStatus.NoPath, sim.Pathfinder.FindPath(new Int3(2, 5, 2), new Int3(7, 6, 2)).Status);
    }

    [Fact]
    public void StepDown_NeedsHeadroomAboveTarget() // PTH-05
    {
        var sim = new ScenarioBuilder().Ground(4).FillBox(new Int3(0, 5, 0), new Int3(4, 5, 31), BlockId.Stone)
            .FillBox(new Int3(5, 7, 0), new Int3(5, 7, 31), BlockId.Stone)   // ceiling at b+up+up for the first lower cell
            .Build();
        Assert.Equal(PathStatus.NoPath, sim.Pathfinder.FindPath(new Int3(3, 6, 2), new Int3(8, 5, 2)).Status);
    }

    [Fact]
    public void StartEqualsGoal_ZeroCost()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var r = sim.Pathfinder.FindPath(new Int3(3, 5, 3), new Int3(3, 5, 3));
        Assert.Equal(PathStatus.Found, r.Status);
        Assert.Equal(new[] { new Int3(3, 5, 3) }, r.Path);
        Assert.Equal(0, r.Cost);
    }

    [Fact]
    public void NonStandableStart_Invalid()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        Assert.Equal(PathStatus.InvalidStart, sim.Pathfinder.FindPath(new Int3(3, 8, 3), new Int3(6, 5, 3)).Status);
        Assert.Equal(PathStatus.InvalidStart, sim.Pathfinder.FindPath(new Int3(-1, 5, 3), new Int3(6, 5, 3)).Status);
    }

    [Fact]
    public void DeepStart_CanPathOut() // WAT-14 flee
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var start = new Int3(3, 5, 3);
        sim.Water.SetLevel(start, WaterGrid.Full);
        var r = sim.Pathfinder.FindPath(start, new Int3(6, 5, 3));
        Assert.Equal(PathStatus.Found, r.Status);
        Assert.Equal(30, r.Cost);
    }

    [Fact]
    public void UnwalkableGoalsIgnored_AllUnwalkable_NoPath() // PTH-11
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var r = sim.Pathfinder.FindPath(new Int3(5, 5, 5), new[] { new Int3(6, 4, 5), new Int3(9, 5, 5) });
        Assert.Equal(new Int3(9, 5, 5), r.Path[^1]);
        Assert.Equal(PathStatus.NoPath, sim.Pathfinder.FindPath(new Int3(5, 5, 5), new[] { new Int3(6, 4, 5) }).Status);
    }

    [Fact]
    public void UnreachableGoalInHugeArea_TooFar() // PTH-10
    {
        var sim = new ScenarioBuilder(160, 32, 160).Ground(4)
            .FillBox(new Int3(150, 5, 150), new Int3(154, 6, 154), BlockId.Stone)
            .FillBox(new Int3(151, 5, 151), new Int3(153, 6, 153), BlockId.Air)
            .Build();
        var r = sim.Pathfinder.FindPath(new Int3(2, 5, 2), new Int3(152, 5, 152));
        Assert.Equal(PathStatus.TooFar, r.Status);
        Assert.Equal(Pathfinder.MaxExpanded, sim.Pathfinder.LastExpanded);
    }

    [Fact]
    public void PathIsContiguous_AndCostMatchesSteps() // PTH-12
    {
        var sim = new ScenarioBuilder().Ground(4)
            .FillBox(new Int3(8, 5, 0), new Int3(8, 6, 25), BlockId.Stone)
            .FillBox(new Int3(12, 5, 6), new Int3(20, 5, 20), BlockId.Stone)
            .Build();
        var r = sim.Pathfinder.FindPath(new Int3(2, 5, 2), new Int3(16, 6, 12));
        Assert.Equal(PathStatus.Found, r.Status);
        int sum = 0;
        Span<PathMove> moves = stackalloc PathMove[PathMoves.MaxMoves];
        for (int i = 1; i < r.Path.Length; i++)
        {
            int n = PathMoves.From(sim.PathGrid, r.Path[i - 1], moves);
            int step = -1;
            for (int m = 0; m < n; m++) if (moves[m].To == r.Path[i]) step = moves[m].Cost;
            Assert.True(step > 0, $"step {r.Path[i - 1]} -> {r.Path[i]} is not a legal move");
            sum += step;
        }
        Assert.Equal(r.Cost, sum);
    }
}
