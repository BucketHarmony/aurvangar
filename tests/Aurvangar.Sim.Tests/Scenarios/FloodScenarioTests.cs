using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M4-T9: WAT-14 flee from deep water and drowning; pathfinding.md scenario 7.</summary>
[Trait("Category", "Scenario")]
public class FloodScenarioTests
{
    private static readonly Int3 Center = new(7, 4, 7);

    /// <summary>Stone ground to y=3; at y=4 a one-block stone ring (x, z 4..10) around a 5×5 basin (x, z 5..9).
    /// The ring top (y=5) is dry standable ground one step up from the basin floor. <paramref name="wallHeight"/>
    /// stacks the ring higher (3 = no way out).</summary>
    private static ScenarioBuilder Basin(int wallHeight = 1)
    {
        var b = new ScenarioBuilder().Ground(3);
        for (int y = 4; y < 4 + wallHeight; y++)
            b.Layer(new Int3(4, y, 4),
                "SSSSSSS",
                "S.....S",
                "S.....S",
                "S.....S",
                "S.....S",
                "S.....S",
                "SSSSSSS");
        return b;
    }

    private static bool InBasin(Int3 c) => c.X is >= 5 and <= 9 && c.Z is >= 5 and <= 9;

    private static void Flood(Simulation sim)
    {
        for (int z = 5; z <= 9; z++)
            for (int x = 5; x <= 9; x++)
                sim.Water.SetLevel(new Int3(x, 4, z), WaterGrid.Full);
    }

    private static Job? FleeJob(Simulation sim) => sim.Jobs.All.FirstOrDefault(j => j.Kind == JobKind.Flee);

    private static void RunUntil(Simulation sim, Func<bool> done, int max)
    {
        for (int i = 0; i < max && !done(); i++) sim.Tick();
        Assert.True(done(), $"condition not reached within {max} ticks");
    }

    /// <summary>WAT-14 / pathfinding.md scenario 7: the basin floods to deep around a standing agent; it claims a Flee
    /// job (priority 200) at once, swims through the deep cells and climbs onto the dry ring without losing health.</summary>
    [Fact]
    public void AgentFleesRisingWater()
    {
        var sim = Basin().Agent(Center).Build();
        var a = sim.Agents.All.Single();
        sim.RunTicks(10);
        Assert.Equal(Center, a.Cell);
        Assert.Null(FleeJob(sim));

        Flood(sim);
        Assert.True(sim.Water.IsDeep(Center));
        sim.Tick();
        var flee = FleeJob(sim);
        Assert.NotNull(flee);
        Assert.Equal(a.Id, flee!.ClaimedBy);
        Assert.Equal(200, flee.Priority);
        Assert.Equal(flee.Id, a.CurrentJob);
        Assert.Equal(MoveStatus.Moving, a.Move);
        Assert.True(sim.PathGrid.IsWalkable(flee.Target));
        Assert.False(InBasin(flee.Target));

        RunUntil(sim, () => FleeJob(sim) is null, 200);
        Assert.True(a.IsAlive);
        Assert.Equal(Agent.HealthMax, a.Health);
        Assert.Equal(5, a.Cell.Y);                     // on the ring top
        Assert.False(InBasin(a.Cell));
        Assert.True(sim.PathGrid.IsWalkable(a.Cell));
        Assert.False(sim.Water.IsDeep(a.Cell));
        Assert.Equal(AgentState.Idle, a.State);
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.Equal(1, sim.Counters.JobsCompleted);

        // It stays out: nothing makes it go back in.
        sim.RunTicks(50);
        Assert.False(InBasin(a.Cell));
        Assert.Equal(Agent.HealthMax, a.Health);
    }

    /// <summary>WAT-14: no dry cell within 64 steps (the ring is 3 blocks high) → 1 damage per tick until the agent
    /// dies with <see cref="DeathCause.Drowned"/> after exactly 1000 ticks; one AgentDied event; it leaves the hash.</summary>
    [Fact]
    public void TrappedAgent_Drowns()
    {
        var sim = Basin(wallHeight: 3).Agent(Center).Build();
        Flood(sim);
        var a = sim.Agents.All.Single();

        sim.RunTicks(10);
        Assert.Equal(Agent.HealthMax - 10, a.Health);
        Assert.True(a.IsAlive);
        Assert.Null(FleeJob(sim));
        Assert.Equal(Center, a.Cell);

        var died = new List<AgentDied>();
        for (int i = 10; i < Agent.HealthMax; i++)
        {
            Assert.True(a.IsAlive, $"died early at tick {i}");
            sim.Tick();
            died.AddRange(sim.Events.Drain().OfType<AgentDied>());
        }
        Assert.False(a.IsAlive);
        Assert.Equal(0, a.Health);
        Assert.Equal(AgentState.Dead, a.State);
        Assert.Equal(DeathCause.Drowned, a.Death);
        var e = Assert.Single(died);
        Assert.Equal(a.Id, e.Agent);
        Assert.Equal("Drowned", e.Cause);
        Assert.False(a.CurrentJob.IsValid);

        // Dead agents take no more damage and emit nothing more.
        sim.RunTicks(20);
        Assert.Equal(0, a.Health);
        Assert.Empty(sim.Events.Drain().OfType<AgentDied>());
    }

    /// <summary>WAT-14: the water drains away before the agent drowns → the damage stops and the agent lives.</summary>
    [Fact]
    public void TrappedAgent_WaterRecedes_StopsDamage()
    {
        var sim = Basin(wallHeight: 3).Agent(Center).Build();
        Flood(sim);
        var a = sim.Agents.All.Single();
        sim.RunTicks(30);
        Assert.Equal(Agent.HealthMax - 30, a.Health);

        for (int z = 5; z <= 9; z++)
            for (int x = 5; x <= 9; x++)
                sim.Water.SetLevel(new Int3(x, 4, z), WaterGrid.Full / 4);
        sim.RunTicks(30);
        Assert.True(a.IsAlive);
        Assert.Equal(Agent.HealthMax - 30, a.Health);
    }

    /// <summary>JOB-07 / WAT-14: Flee preempts the current job, which returns to the board unclaimed with no failure
    /// counted, and the carried stack is dropped on the agent's cell.</summary>
    [Fact]
    public void Flee_PreemptsJob_DropsCargo()
    {
        var sim = Basin().Agent(Center).Build();
        var a = sim.Agents.All.Single();
        var target = new Int3(20, 3, 20);
        var dig = sim.Jobs.Post(JobKind.Dig, target,
            new[] { JobStep.GoTo(target), JobStep.Work(target, 50), JobStep.Dig(target) }, new[] { Reservation.OnCell(target) });
        JobRunner.Claim(sim, a, dig);
        var stone = TestContent.Db.Item("stone");
        a.Carried = new ItemStack(stone, 5);

        Flood(sim);
        sim.Tick();

        Assert.False(dig.IsClaimed);
        Assert.Equal(0, dig.Failures);
        Assert.False(sim.Jobs.IsCellReserved(target));
        Assert.True(a.Carried.IsEmpty);
        Assert.Equal(new ItemStack(stone, 5), sim.Piles.At(Center));
        Assert.Equal(JobKind.Flee, sim.Jobs.Get(a.CurrentJob)!.Kind);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>JOB-06: a deep-water agent without a job never picks board work while it flees.</summary>
    [Fact]
    public void FleeingAgent_IgnoresBoard()
    {
        var sim = Basin().Agent(Center).Build();
        var a = sim.Agents.All.Single();
        var cell = new Int3(20, 4, 20);
        sim.Jobs.Post(JobKind.Construct, cell, new[] { JobStep.GoTo(cell), JobStep.Work(cell, 10) });
        Flood(sim);
        sim.Tick();
        Assert.Equal(JobKind.Flee, sim.Jobs.Get(a.CurrentJob)!.Kind);
        RunUntil(sim, () => FleeJob(sim) is null, 200);
        Assert.False(InBasin(a.Cell));
    }

    /// <summary>ADR-031: a flee path blocked on the way fails the Flee job, which is removed (never left on the board for
    /// retry); the next tick the agent searches again and gets out another way.</summary>
    [Fact]
    public void FleePathBlocked_SearchesAgain()
    {
        var sim = Basin().Agent(Center).Build();
        var a = sim.Agents.All.Single();
        Flood(sim);
        sim.Tick();
        var first = FleeJob(sim)!;
        sim.World.SetBlock(first.Target, BlockId.Stone);   // the dry cell it heads for is walled up

        RunUntil(sim, () => sim.Counters.JobsFailed == 1, 100);
        Assert.Null(sim.Jobs.Get(first.Id));
        RunUntil(sim, () => !InBasin(a.Cell) && FleeJob(sim) is null, 300);
        Assert.Equal(5, a.Cell.Y);
        Assert.Equal(1, sim.Counters.JobsFailed);
        Assert.Equal(Agent.HealthMax, a.Health);
        Assert.Empty(sim.Jobs.All);
    }

    /// <summary>WAT-14 "immediately": the idle job-search throttle (JOB-06, 5 ticks) never delays the first flee
    /// search, whichever tick the water arrives on.</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    public void Flee_NotDelayedByJobSearchThrottle(int floodTick)
    {
        var sim = Basin().Agent(Center).Build();
        var a = sim.Agents.All.Single();
        sim.RunTicks(floodTick);
        Flood(sim);
        sim.Tick();
        Assert.NotNull(FleeJob(sim));
        Assert.Equal(AgentState.Working, a.State);
        RunUntil(sim, () => FleeJob(sim) is null, 200);
        Assert.Equal(Agent.HealthMax, a.Health);
        Assert.False(InBasin(a.Cell));
    }

    /// <summary>ADR-031: a trapped agent is <see cref="AgentState.Trapped"/> and goes back to Idle when the water drops.</summary>
    [Fact]
    public void Trapped_State_ClearsWhenWaterDrops()
    {
        var sim = Basin(wallHeight: 3).Agent(Center).Build();
        Flood(sim);
        var a = sim.Agents.All.Single();
        sim.Tick();
        Assert.Equal(AgentState.Trapped, a.State);
        for (int z = 5; z <= 9; z++)
            for (int x = 5; x <= 9; x++)
                sim.Water.SetLevel(new Int3(x, 4, z), 0);
        sim.Tick();
        Assert.Equal(AgentState.Idle, a.State);
    }
}
