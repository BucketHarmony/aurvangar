using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Paths;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>PTH-15..17 path following and the seed-1 colonist spawn (M4-T4).</summary>
public class AgentMovementTests
{
    private static readonly Int3 Start = new(5, 5, 5);

    private static (Simulation Sim, Agent Agent) Flat(Action<ScenarioBuilder>? extra = null)
    {
        var b = new ScenarioBuilder().Ground(4).Agent(Start);
        extra?.Invoke(b);
        var sim = b.Build();
        return (sim, sim.Agents.All.First());
    }

    /// <summary>Ticks until the agent stops moving (arrived or failed), at most <paramref name="max"/>.</summary>
    private static int RunUntilStopped(Simulation sim, Agent a, int max = 500)
    {
        int n = 0;
        while (a.Move == MoveStatus.Moving && n < max) { sim.Tick(); n++; }
        return n;
    }

    /// <summary>Asserts that a one-step move takes exactly <paramref name="ticks"/> ticks: the agent is still on its
    /// cell (lerping toward the target) one tick before, and on the target after.</summary>
    private static void AssertOneStep(Simulation sim, Agent a, Int3 to, int ticks)
    {
        var from = a.Cell;
        Assert.Equal(PathStatus.Found, sim.Agents.MoveTo(sim, a, to));
        Assert.Equal(MoveStatus.Moving, a.Move);
        sim.RunTicks(ticks - 1);
        Assert.Equal(from, a.Cell);
        Assert.Equal(to, a.NextCell);
        Assert.Equal(ticks, a.MoveTotal);
        Assert.Equal(ticks - 1, a.MoveProgress);
        sim.Tick();
        Assert.Equal(to, a.Cell);
        Assert.Equal(MoveStatus.Arrived, a.Move);
        Assert.Equal(0, a.MoveProgress);
    }

    [Fact]
    public void MoveTicks_OrthogonalDiagonalStepUpWade()
    {
        var (sim, a) = Flat(b => b.FillBox(new Int3(8, 5, 6), new Int3(8, 5, 6), BlockId.Stone));
        AssertOneStep(sim, a, new Int3(6, 5, 5), 4);           // orthogonal
        AssertOneStep(sim, a, new Int3(7, 5, 6), 6);           // diagonal
        AssertOneStep(sim, a, new Int3(8, 6, 6), 4 + 2);       // orthogonal step up
        AssertOneStep(sim, a, new Int3(9, 5, 6), 4);           // orthogonal step down: no extra (PTH-15)

        // Wadeable: a closed two-cell corridor holding shallow water. +3 on entering the wet cell.
        var sim2 = new ScenarioBuilder().Ground(4)
            .Layer(5, "SSSS", "S..S", "SSSS")
            .FillBox(new Int3(0, 6, 0), new Int3(3, 7, 2), BlockId.Air)
            .Water(new Int3(2, 5, 1), WaterGrid.Full / 4)
            .Agent(new Int3(1, 5, 1)).Build();
        var w = sim2.Agents.All.First();
        sim2.Tick();                                           // let the water settle into both cells
        Assert.True(sim2.PathGrid.IsWet(new Int3(2, 5, 1)) && sim2.PathGrid.IsWalkable(new Int3(2, 5, 1)));
        AssertOneStep(sim2, w, new Int3(2, 5, 1), 4 + 3);
    }

    [Fact]
    public void Path_FollowsEveryCellInOrder_AndTotalTicksMatchSegments()
    {
        var (sim, a) = Flat();
        var goal = new Int3(12, 5, 9);
        Assert.Equal(PathStatus.Found, sim.Agents.MoveTo(sim, a, goal));
        var path = a.Path.ToArray();
        int expected = 0;
        for (int i = 0; i + 1 < path.Length; i++) expected += AgentMovement.MoveTicks(sim.PathGrid, path[i], path[i + 1]);

        var visited = new List<Int3> { a.Cell };
        int ticks = 0;
        while (a.Move == MoveStatus.Moving)
        {
            sim.Tick(); ticks++;
            if (a.Cell != visited[^1]) visited.Add(a.Cell);
        }
        Assert.Equal(MoveStatus.Arrived, a.Move);
        Assert.Equal(path, visited.ToArray());
        Assert.Equal(expected, ticks);
        Assert.Empty(a.Path);
    }

    [Fact]
    public void MoveTo_Unreachable_FailsWithoutMoving()
    {
        var (sim, a) = Flat(b => b.FillBox(new Int3(20, 5, 0), new Int3(20, 7, 31), BlockId.Stone));
        Assert.Equal(PathStatus.NoPath, sim.Agents.MoveTo(sim, a, new Int3(25, 5, 5)));
        Assert.Equal(MoveStatus.Failed, a.Move);
        sim.RunTicks(10);
        Assert.Equal(Start, a.Cell);
    }

    [Fact]
    public void BlockedNextCell_RepathsOnceThenFails()
    {
        // (1) Blocked in open ground: one repath around the new block, then arrival.
        {
            var (sim, a) = Flat();
            var goal = new Int3(12, 5, 5);
            sim.Agents.MoveTo(sim, a, goal);
            Assert.Equal(new Int3(6, 5, 5), a.Path[1]);
            sim.RunTicks(4);                                    // standing on (6,5,5)
            Assert.Equal(new Int3(6, 5, 5), a.Cell);
            long searches = sim.Pathfinder.Searches;
            sim.World.SetBlock(new Int3(7, 5, 5), BlockId.Stone);   // next cell no longer walkable
            sim.World.SetBlock(new Int3(7, 6, 5), BlockId.Stone);
            RunUntilStopped(sim, a);
            Assert.Equal(MoveStatus.Arrived, a.Move);
            Assert.Equal(goal, a.Cell);
            Assert.Equal(searches + 1, sim.Pathfinder.Searches);
            Assert.True(a.Repathed);
        }
        // (2) The repath itself fails: a corridor sealed ahead. The step fails, the agent stays put.
        {
            var (sim, a) = Flat(b => b
                .FillBox(new Int3(0, 5, 4), new Int3(31, 7, 4), BlockId.Stone)
                .FillBox(new Int3(0, 5, 6), new Int3(31, 7, 6), BlockId.Stone));
            sim.Agents.MoveTo(sim, a, new Int3(12, 5, 5));
            sim.RunTicks(4);
            Assert.Equal(new Int3(6, 5, 5), a.Cell);
            long searches = sim.Pathfinder.Searches;
            sim.World.SetBlock(new Int3(9, 5, 5), BlockId.Stone);
            sim.World.SetBlock(new Int3(9, 6, 5), BlockId.Stone);
            RunUntilStopped(sim, a);
            Assert.Equal(MoveStatus.Failed, a.Move);
            Assert.Equal(new Int3(8, 5, 5), a.Cell);            // walked up to the block, then failed
            Assert.Equal(searches + 1, sim.Pathfinder.Searches);
            Assert.Empty(a.Path);
            Assert.Equal(a.Cell, a.NextCell);
            Assert.Equal(0, a.MoveProgress);
        }
        // (3) Blocked again after a successful repath: no second repath, the move fails.
        {
            var (sim, a) = Flat();
            sim.Agents.MoveTo(sim, a, new Int3(12, 5, 5));
            sim.RunTicks(4);
            long searches = sim.Pathfinder.Searches;
            sim.World.SetBlock(new Int3(7, 5, 5), BlockId.Stone);
            sim.World.SetBlock(new Int3(7, 6, 5), BlockId.Stone);
            sim.Tick();                                          // repath happens here
            Assert.Equal(searches + 1, sim.Pathfinder.Searches);
            var next = a.Path[a.PathPos + 1];
            sim.World.SetBlock(next, BlockId.Stone);
            sim.World.SetBlock(next + Int3.Up, BlockId.Stone);
            RunUntilStopped(sim, a);
            Assert.Equal(MoveStatus.Failed, a.Move);
            Assert.Equal(searches + 1, sim.Pathfinder.Searches);
        }
    }

    [Fact]
    public void BlockedMidStep_DoesNotEnter()
    {
        // The cell is filled while the agent is already moving toward it: it never enters it.
        var (sim, a) = Flat(b => b
            .FillBox(new Int3(0, 5, 4), new Int3(31, 7, 4), BlockId.Stone)
            .FillBox(new Int3(0, 5, 6), new Int3(31, 7, 6), BlockId.Stone));
        sim.Agents.MoveTo(sim, a, new Int3(8, 5, 5));
        sim.Tick();
        Assert.Equal(new Int3(6, 5, 5), a.NextCell);
        sim.World.SetBlock(new Int3(6, 5, 5), BlockId.Stone);
        sim.World.SetBlock(new Int3(6, 6, 5), BlockId.Stone);
        RunUntilStopped(sim, a);
        Assert.Equal(MoveStatus.Failed, a.Move);
        Assert.Equal(Start, a.Cell);
    }

    [Fact]
    public void AgentsDoNotCollide()
    {
        // Two agents on one cell, and two agents swapping ends of a one-wide corridor.
        var sim = new ScenarioBuilder().Ground(4)
            .FillBox(new Int3(0, 5, 4), new Int3(31, 7, 4), BlockId.Stone)
            .FillBox(new Int3(0, 5, 6), new Int3(31, 7, 6), BlockId.Stone)
            .Agent(new Int3(2, 5, 5)).Agent(new Int3(2, 5, 5)).Agent(new Int3(10, 5, 5))
            .Build();
        var ag = sim.Agents.All.ToArray();
        Assert.Equal(ag[0].Cell, ag[1].Cell);
        sim.Agents.MoveTo(sim, ag[0], new Int3(10, 5, 5));
        sim.Agents.MoveTo(sim, ag[2], new Int3(2, 5, 5));
        bool shared = false;
        for (int t = 0; t < 200 && (ag[0].Move == MoveStatus.Moving || ag[2].Move == MoveStatus.Moving); t++)
        {
            sim.Tick();
            shared |= ag[0].Cell == ag[2].Cell;
        }
        Assert.True(shared);
        Assert.Equal(MoveStatus.Arrived, ag[0].Move);
        Assert.Equal(MoveStatus.Arrived, ag[2].Move);
        Assert.Equal(new Int3(10, 5, 5), ag[0].Cell);
        Assert.Equal(new Int3(2, 5, 5), ag[2].Cell);
        Assert.Equal(new Int3(2, 5, 5), ag[1].Cell);
    }

    [Fact]
    public void Movement_IsInStateHash()
    {
        var (s1, a1) = Flat();
        var (s2, _) = Flat();
        Assert.Equal(s1.StateHash(), s2.StateHash());
        s1.Agents.MoveTo(s1, a1, new Int3(8, 5, 5));
        Assert.NotEqual(s1.StateHash(), s2.StateHash());
    }

    [Fact]
    public void Seed1_FiveColonistsSpawnStandable()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var agents = sim.Agents.All.ToArray();
        Assert.Equal(5, agents.Length);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, agents.Select(a => a.Id.Value).ToArray());
        Assert.Equal(5, agents.Select(a => a.Name).Distinct().Count());
        Assert.All(agents, a => Assert.False(string.IsNullOrWhiteSpace(a.Name)));
        Assert.Equal(5, agents.Select(a => a.Cell).Distinct().Count());

        var entrance = sim.Buildings.All.Single(b => b.Def.Id == "hub").EntranceCell;
        sim.Tick();                                             // regions are built at tick end
        int hubRegion = sim.Regions.RegionOf(entrance);
        Assert.NotEqual(Regions.None, hubRegion);
        foreach (var a in agents)
        {
            Assert.True(a.IsAlive);
            Assert.Equal(AgentState.Idle, a.State);
            Assert.Equal(a.Cell, a.NextCell);
            Assert.True(sim.PathGrid.IsStandable(a.Cell) && sim.PathGrid.IsWalkable(a.Cell), $"{a.Name} at {a.Cell}");
            Assert.False(sim.PathGrid.IsWet(a.Cell));
            Assert.True(Math.Max(Math.Abs(a.Cell.X - entrance.X), Math.Abs(a.Cell.Z - entrance.Z)) <= 3, $"{a.Name} at {a.Cell}");
            Assert.Equal(hubRegion, sim.Regions.RegionOf(a.Cell));
        }
    }
}
