using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M4-T7: dig and chop designations end to end (DSG-01..08, JOB-09, ECO-09).</summary>
[Trait("Category", "Scenario")]
public class DigScenarioTests
{
    private static Agent AgentN(Simulation sim, int n) => sim.Agents.All.ElementAt(n);

    private static void RunUntil(Simulation sim, Func<bool> done, int max, Action? eachTick = null)
    {
        for (int i = 0; i < max && !done(); i++) { sim.Tick(); eachTick?.Invoke(); }
        Assert.True(done(), $"condition not reached within {max} ticks");
    }

    private static IEnumerable<Int3> Box(Int3 min, Int3 max)
    {
        for (int y = min.Y; y <= max.Y; y++)
            for (int z = min.Z; z <= max.Z; z++)
                for (int x = min.X; x <= max.X; x++)
                    yield return new Int3(x, y, z);
    }

    private static bool Exposed(Simulation sim, Int3 c)
    {
        foreach (var d in new[] { Int3.Up, Int3.Down, new Int3(1, 0, 0), new Int3(-1, 0, 0), new Int3(0, 0, 1), new Int3(0, 0, -1) })
            if (!sim.World.IsSolid(c + d)) return true;
        return false;
    }

    /// <summary>jobs-agents.md scenario 1, up to the pile (hauling is M4-T8).</summary>
    [Fact]
    public void DigStone_LeavesStonePile()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(new Int3(5, 5, 5)).Build();
        var target = new Int3(8, 4, 5);
        sim.Enqueue(new DesignateDig(target, target));

        RunUntil(sim, () => sim.World.GetBlock(target) == BlockId.Air, 400);
        sim.Tick();
        Assert.Equal(new Items.ItemStack(TestContent.Db.Item("stone"), 1), sim.Piles.At(target));
        Assert.Equal(DesignationMark.None, sim.Designations.Get(target));   // DSG-07
        Assert.Equal(0, sim.Jobs.Count);
        Assert.Equal(1, sim.Counters.JobsCompleted);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>jobs-agents.md scenario 1 in full. Needs HaulSystem (M4-T8), see ADR-029.</summary>
    [Fact(Skip = "M4-T8")]
    public void DigStone_EndsInHubStorage()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(new Int3(20, 5, 20)).Agent(new Int3(5, 5, 5)).Build();
        var hub = sim.Buildings.All.First();
        var stone = TestContent.Db.Item("stone");
        var target = new Int3(8, 4, 5);
        sim.Enqueue(new DesignateDig(target, target));

        RunUntil(sim, () => sim.World.GetBlock(target) == BlockId.Air, 400);
        RunUntil(sim, () => hub.Stored.GetValueOrDefault(stone.Value) == 1, 1000);
        Assert.Equal(0, sim.Piles.Count);
        Assert.Equal(0, sim.Jobs.Count);
    }

    /// <summary>DSG-03: only exposed cells get jobs, so a tunnel is dug from the open face inward.</summary>
    [Fact]
    public void Tunnel_DugFromExposedSideInward()
    {
        // Flat ground at y = 4, a hill x = 10..20, y = 5..8 across all z. Tunnel 2 high, 6 deep at z = 15.
        var sim = new ScenarioBuilder().Ground(4)
            .FillBox(new Int3(10, 5, 0), new Int3(20, 8, 31), BlockId.Stone)
            .Agent(new Int3(5, 5, 15)).Build();
        Int3 min = new(10, 5, 15), max = new(15, 6, 15);
        var cells = Box(min, max).ToList();
        sim.Enqueue(new DesignateDig(min, max));

        sim.Tick();
        var posted = sim.Jobs.All.Where(j => j.Kind == JobKind.Dig).Select(j => j.Target).OrderBy(c => c.Y).ToList();
        Assert.Equal(new[] { new Int3(10, 5, 15), new Int3(10, 6, 15) }, posted);   // only the open face

        var digOrder = new List<Int3>();
        RunUntil(sim, () => cells.All(c => sim.World.GetBlock(c) == BlockId.Air), 3000, () =>
        {
            foreach (var j in sim.Jobs.All)
                if (j.Kind == JobKind.Dig) Assert.True(Exposed(sim, j.Target), $"job for unexposed {j.Target}");
            foreach (var c in cells)
                if (sim.World.GetBlock(c) == BlockId.Air && !digOrder.Contains(c))
                {
                    // inward: something in the column nearer the face is already open
                    Assert.True(c.X == min.X || digOrder.Any(d => d.X == c.X - 1), $"{c} dug before its outer column");
                    digOrder.Add(c);
                }
        });
        Assert.Equal(min.X, digOrder[0].X);
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.Equal(0, sim.Designations.Count);
    }

    /// <summary>DSG-04: higher cells outrank lower ones, so one agent digs a pit layer by layer from the top.</summary>
    [Fact]
    public void Pit_DugTopDown()
    {
        var sim = new ScenarioBuilder().Ground(8).Agent(new Int3(5, 9, 5)).Build();
        Int3 min = new(10, 6, 10), max = new(12, 8, 12);
        var cells = Box(min, max).ToList();
        sim.Enqueue(new DesignateDig(min, max));

        var order = new List<Int3>();
        RunUntil(sim, () => cells.All(c => sim.World.GetBlock(c) == BlockId.Air), 8000, () =>
        {
            foreach (var c in cells)
                if (sim.World.GetBlock(c) == BlockId.Air && !order.Contains(c)) order.Add(c);
        });
        Assert.Equal(27, order.Count);
        for (int i = 1; i < order.Count; i++)
            Assert.True(order[i].Y <= order[i - 1].Y, $"dig {i} at y={order[i].Y} after y={order[i - 1].Y}");
        Assert.Equal(0, sim.Designations.Count);
    }

    /// <summary>JOB-09, DSG-08: no agent ever loses its floor to a dig, and every designated cell still gets dug.</summary>
    [Fact]
    public void AgentNeverDigsOwnFloor()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(new Int3(10, 5, 10)).Agent(new Int3(11, 5, 11)).Build();
        Int3 min = new(9, 4, 9), max = new(11, 4, 11);
        var cells = Box(min, max).ToList();
        sim.Enqueue(new DesignateDig(min, max));

        var claimers = new Dictionary<Int3, AgentId>();
        RunUntil(sim, () => cells.All(c => sim.World.GetBlock(c) == BlockId.Air), 4000, () =>
        {
            foreach (var a in sim.Agents.All)
            {
                Assert.True(sim.World.IsSolid(a.Cell + Int3.Down), $"{a.Id} at {a.Cell} lost its floor");
                Assert.True(sim.World.IsSolid(a.NextCell + Int3.Down), $"{a.Id} stepping to {a.NextCell} over air");
            }
            foreach (var c in cells)
            {
                if (sim.World.GetBlock(c) != BlockId.Air || !claimers.TryGetValue(c, out var who)) continue;
                var digger = sim.Agents.Get(who)!;
                Assert.NotEqual(c + Int3.Up, digger.Cell);   // JOB-09: not the cell below itself
                claimers.Remove(c);
            }
            foreach (var j in sim.Jobs.All)
                if (j.Kind == JobKind.Dig && j.IsClaimed) claimers[j.Target] = j.ClaimedBy;
        });
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>DSG-08: a dig under an agent waits (no claim, no failure) until the agent leaves.</summary>
    [Fact]
    public void Dig_DeferredWhileAgentStandsOnIt()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(new Int3(10, 5, 10)).Agent(new Int3(5, 5, 5)).Build();
        Agent sitter = AgentN(sim, 0), digger = AgentN(sim, 1);
        var target = new Int3(10, 4, 10);
        var spot = target + Int3.Up;
        // Keep the sitter busy on its spot with a higher-priority job for 150 ticks.
        var busy = sim.Jobs.Post(JobKind.Construct, spot,
            new[] { JobStep.GoTo(spot, GoalMode.Exact), JobStep.Work(spot, 150) });
        sim.Enqueue(new DesignateDig(target, target));

        RunUntil(sim, () => sim.Jobs.Get(busy.Id) is null, 300, () =>
        {
            Assert.Equal(spot, sitter.Cell);
            Assert.Equal(BlockId.Stone, sim.World.GetBlock(target));
            foreach (var j in sim.Jobs.All) if (j.Kind == JobKind.Dig) Assert.False(j.IsClaimed);
        });
        RunUntil(sim, () => sim.World.GetBlock(target) == BlockId.Air, 400,
            () => Assert.True(sim.World.IsSolid(sitter.Cell + Int3.Down)));
        Assert.NotEqual(spot, sitter.Cell);   // stepped aside
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.Equal(2, sim.Counters.JobsCompleted);
        Assert.Equal(AgentState.Idle, digger.State);
    }

    /// <summary>DSG-08 once claimed: the digger's Dig step waits while an agent stands on the block; that agent, idle
    /// with nothing to take, steps aside, and the dig completes without a failure.</summary>
    [Fact]
    public void Dig_WaitsForAgentOnTop_IdleAgentStepsAside()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(new Int3(14, 5, 10)).Agent(new Int3(5, 5, 10)).Build();
        Agent sitter = AgentN(sim, 0), digger = AgentN(sim, 1);
        var target = new Int3(10, 4, 10);
        var spot = target + Int3.Up;
        sim.Jobs.Post(JobKind.Construct, spot, new[] { JobStep.GoTo(spot, GoalMode.Exact), JobStep.Work(spot, 150) });
        sim.Enqueue(new DesignateDig(target, target));

        bool waited = false;
        RunUntil(sim, () => sim.World.GetBlock(target) == BlockId.Air, 600, () =>
        {
            foreach (var j in sim.Jobs.All)
                if (j.Kind == JobKind.Dig && j.IsClaimed) Assert.Equal(digger.Id, j.ClaimedBy);
            if (sitter.Cell == spot && digger.StepIndex == 2) waited = true;
            Assert.True(sim.World.IsSolid(sitter.Cell + Int3.Down));
        });
        Assert.True(waited, "the digger never had to wait");
        Assert.NotEqual(spot, sitter.Cell);
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.Equal(2, sim.Counters.JobsCompleted);
    }

    /// <summary>DSG-05, ECO-09 up to the logs (hauling is M4-T8).</summary>
    [Fact]
    public void Chop_MarkedTrees_DropLogs()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Layer(new Int3(10, 5, 10), "T.T")
            .Layer(new Int3(20, 5, 20), "T")
            .Agent(new Int3(5, 5, 5)).Build();
        var outside = sim.Plants.All.Last();
        sim.Enqueue(new DesignateChop(9, 9, 13, 11));

        RunUntil(sim, () => sim.Plants.Count == 1, 1000);
        Assert.Same(outside, sim.Plants.All.Single());
        Assert.False(outside.MarkedForChop);
        var log = TestContent.Db.Item("log");
        Assert.Equal(new Items.ItemStack(log, 4), sim.Piles.At(new Int3(10, 5, 10)));
        Assert.Equal(new Items.ItemStack(log, 4), sim.Piles.At(new Int3(12, 5, 10)));
        sim.Tick();
        Assert.Equal(0, sim.Jobs.Count);
        Assert.Equal(2, sim.Counters.JobsCompleted);
    }

    /// <summary>DSG-05, ECO-09 in full. Needs HaulSystem (M4-T8), see ADR-029.</summary>
    [Fact(Skip = "M4-T8")]
    public void Chop_MarkedTrees_LogsHauled()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(new Int3(20, 5, 20))
            .Layer(new Int3(10, 5, 10), "T.T")
            .Agent(new Int3(5, 5, 5)).Build();
        var hub = sim.Buildings.All.First();
        var log = TestContent.Db.Item("log");
        sim.Enqueue(new DesignateChop(9, 9, 13, 11));

        RunUntil(sim, () => hub.Stored.GetValueOrDefault(log.Value) == 8, 3000);
        Assert.Equal(0, sim.Plants.Count);
        Assert.Equal(0, sim.Piles.Count);
    }

    /// <summary>JOB-08 for chop: after five failures the tree gets an unreachable mark and no new job; designating
    /// it again retries.</summary>
    [Fact]
    public void ChopGiveUp_MarksTree_RedesignateRetries()
    {
        var sim = new ScenarioBuilder().Ground(4).Layer(new Int3(10, 5, 10), "T").Agent(new Int3(5, 5, 5)).Build();
        var tree = sim.Plants.All.Single();
        sim.Enqueue(new DesignateChop(10, 10, 10, 10));
        RunUntil(sim, () => sim.Jobs.All.Any(j => j.IsClaimed), 20);
        tree.MarkedForChop = false;   // behind the command's back: every Chop step now fails (InvalidTarget)

        RunUntil(sim, () => tree.ChopUnreachable, 3000);
        Assert.Equal(Job.MaxFailures, sim.Counters.JobsFailed);
        sim.RunTicks(100);
        Assert.Equal(0, sim.Jobs.Count);

        sim.Enqueue(new DesignateChop(10, 10, 10, 10));
        RunUntil(sim, () => sim.Plants.Count == 0, 400);
        Assert.Equal(1, sim.Counters.JobsCompleted);
    }

    /// <summary>DSG-06: cancel removes marks and jobs; a claimed job is released and its agent goes idle.</summary>
    [Fact]
    public void Cancel_ReleasesClaimedJob()
    {
        var sim = new ScenarioBuilder().Ground(4).Layer(new Int3(20, 5, 5), "T").Agent(new Int3(5, 5, 5)).Build();
        var a = AgentN(sim, 0);
        var tree = sim.Plants.All.Single();
        var target = new Int3(15, 4, 15);
        sim.Enqueue(new DesignateDig(target, target));
        sim.Enqueue(new DesignateChop(20, 5, 20, 5));
        RunUntil(sim, () => a.CurrentJob.IsValid && a.Move == MoveStatus.Moving, 20);
        var claimed = sim.Jobs.Get(a.CurrentJob)!;
        Assert.Equal(2, sim.Jobs.Count);

        sim.Enqueue(new CancelDesignation(new Int3(0, 0, 0), new Int3(31, 31, 31)));
        sim.Tick();
        Assert.Equal(0, sim.Jobs.Count);
        Assert.Null(sim.Jobs.Get(claimed.Id));
        Assert.False(a.CurrentJob.IsValid);
        Assert.Equal(AgentState.Idle, a.State);
        Assert.False(sim.Jobs.IsCellReserved(target));
        Assert.Equal(DesignationMark.None, sim.Designations.Get(target));
        Assert.False(tree.MarkedForChop);

        sim.RunTicks(300);
        Assert.Equal(0, sim.Jobs.Count);
        Assert.Equal(BlockId.Stone, sim.World.GetBlock(target));
        Assert.Equal(1, sim.Plants.Count);
        Assert.Equal(0, sim.Counters.JobsFailed + sim.Counters.JobsCompleted);
    }
}
