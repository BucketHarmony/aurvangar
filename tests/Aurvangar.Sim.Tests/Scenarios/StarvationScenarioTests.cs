using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Xunit;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M5-T5: death from hunger (needs-economy.md scenario 2), ColonyLost (ECO-07), need preemption (JOB-07,
/// jobs-agents.md scenario 4).</summary>
[Trait("Category", "Scenario")]
public class StarvationScenarioTests
{
    private static readonly Int3 HubOrigin = new(2, 5, 2);

    private static ItemId Item(string key) => TestContent.Db.Item(key);

    private static void RunUntil(Simulation sim, Func<bool> done, int max)
    {
        for (int i = 0; i < max && !done(); i++) sim.Tick();
        Assert.True(done(), $"condition not reached within {max} ticks");
    }

    /// <summary>needs-economy.md scenario 2: no food in storage, hunger 0: health runs down 1 per tick and the agent
    /// dies after exactly 1000 ticks, cause Starved; no Eat job is ever posted.</summary>
    [Fact]
    public void NoFood_DiesStarvedAfter1000TicksAtZero()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(HubOrigin).Stock("water", 10).Agent(new Int3(8, 5, 8)).Build();
        var a = sim.Agents.All.Single();
        a.Hunger = 0;
        sim.Events.Drain();

        sim.RunTicks(999);
        Assert.True(a.IsAlive);
        Assert.Equal(1, a.Health);
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Eat);
        Assert.True(NeedsSystem.NoFood(sim));

        sim.Tick();
        Assert.False(a.IsAlive);
        Assert.Equal(AgentState.Dead, a.State);
        Assert.Equal(DeathCause.Starved, a.Death);
        var events = sim.Events.Drain();
        Assert.Contains(events, e => e is AgentDied d && d.Agent == a.Id && d.Cause == nameof(DeathCause.Starved));
        Assert.Contains(events, e => e is ColonyLost);   // the only colonist

        // The dead stay put: no more decay, no jobs.
        int hunger = a.Hunger, thirst = a.Thirst;
        sim.RunTicks(50);
        Assert.Equal(hunger, a.Hunger);
        Assert.Equal(thirst, a.Thirst);
        Assert.Empty(sim.Jobs.All);
    }

    /// <summary>ECO-07: ColonyLost is emitted once, when the last agent dies (not before), is kept in the saved state,
    /// and the sim keeps running afterwards (water still flows).</summary>
    [Fact]
    public void AllDead_ColonyLostOnce()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Agent(new Int3(8, 5, 8)).Agent(new Int3(10, 5, 8))
            .Water(new Int3(20, 5, 20), WaterGrid.Full).Build();
        var agents = sim.Agents.All.ToList();
        foreach (var a in agents) a.Thirst = 0;
        agents[0].Health = 5;
        agents[1].Health = 10;

        var lost = new List<long>();
        var died = new List<long>();
        for (int t = 0; t < 200; t++)
        {
            sim.Tick();
            foreach (var e in sim.Events.Drain())
            {
                if (e is ColonyLost) lost.Add(sim.Clock.Tick);
                if (e is AgentDied) died.Add(sim.Clock.Tick);
            }
            if (t == 20)
            {
                // Save/load after the loss keeps the flag: nothing re-emits it.
                using var ms = new MemoryStream();
                SaveGame.Save(sim, ms);
                ms.Position = 0;
                var loaded = SaveGame.Load(ms, TestContent.Db);
                Assert.True(loaded.Agents.ColonyLost);
                Assert.Equal(sim.StateHash(), loaded.StateHash());
                loaded.RunTicks(50);
                Assert.DoesNotContain(loaded.Events.Drain(), e => e is ColonyLost);
            }
        }

        Assert.Equal(new long[] { 5, 10 }, died);
        Assert.Equal(new long[] { 10 }, lost);
        Assert.True(sim.Agents.ColonyLost);
        Assert.All(agents, a => Assert.Equal(DeathCause.Dehydrated, a.Death));
        // The world goes on: the lone water cell spread and evaporated meanwhile.
        Assert.NotEqual(WaterGrid.Full, sim.Water.GetLevel(new Int3(20, 5, 20)));
        Assert.Equal(200, sim.Clock.Tick);
    }

    /// <summary>JOB-07 / jobs-agents.md scenario 4: an agent carrying logs on a haul becomes thirsty: it drops the logs
    /// on its cell, the haul goes back to the board, it drinks, and the logs are hauled to storage afterwards.</summary>
    [Fact]
    public void ThirstyAgent_PreemptsHaul_DropsCargo()
    {
        var pile = new Int3(24, 5, 24);
        var sim = new ScenarioBuilder().Ground(4).Hub(HubOrigin).Stock("water", 10)
            .Pile(pile, "log", 8).Agent(new Int3(22, 5, 24)).Build();
        var a = sim.Agents.All.Single();
        var hub = sim.Buildings.All.Single();

        RunUntil(sim, () => a.Carried is { IsEmpty: false } && a.Cell.X < 20, 300);   // on the way back with the logs
        var haul = sim.Jobs.Get(a.CurrentJob)!;
        Assert.Equal(JobKind.Haul, haul.Kind);
        Assert.Equal(8, a.Carried.Count);
        var at = a.Cell;

        a.Thirst = 4_000;
        sim.Tick();
        var drink = sim.Jobs.Get(a.CurrentJob)!;
        Assert.Equal(JobKind.Drink, drink.Kind);
        Assert.Equal(a.Id, drink.ClaimedBy);
        Assert.True(a.Carried.IsEmpty);
        var dropped = sim.Piles.All.Single();
        Assert.Equal(Item("log"), dropped.Stack.Item);
        Assert.Equal(8, dropped.Stack.Count);
        Assert.True(Math.Abs(dropped.Cell.X - at.X) <= 1 && Math.Abs(dropped.Cell.Z - at.Z) <= 1,
            $"logs dropped at {dropped.Cell}, agent was at {at}");
        Assert.False(sim.Jobs.Get(haul.Id)?.IsClaimed ?? false);   // the old haul is not held any more

        RunUntil(sim, () => hub.Stored.GetValueOrDefault(Item("log").Value) == 8, 600);
        Assert.True(a.Thirst >= NeedsSystem.Sated - 200);
        Assert.Equal(8, hub.Stored.GetValueOrDefault(Item("water").Value));
        Assert.Equal(0, sim.Piles.Count);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }
}
