using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>M5-T5: hunger/thirst decay, need thresholds, consuming, health (ECO-02..06, JOB-07).</summary>
public class NeedsTests
{
    // Hub footprint x 2..4, z 2..4 on ground y=4; entrance (3,5,1). The agent stands next to it.
    private static readonly Int3 HubOrigin = new(2, 5, 2);
    private static readonly Int3 Near = new(6, 5, 3);

    private static ItemId Item(string key) => TestContent.Db.Item(key);

    private static int Stored(Simulation sim, string item) =>
        sim.Buildings.All.First().Stored.GetValueOrDefault(Item(item).Value);

    private static Job? JobOf(Simulation sim, Agent a) =>
        a.CurrentJob.IsValid ? sim.Jobs.Get(a.CurrentJob) : null;

    private static void RunUntil(Simulation sim, Func<bool> done, int max)
    {
        for (int i = 0; i < max && !done(); i++) sim.Tick();
        Assert.True(done(), $"condition not reached within {max} ticks");
    }

    /// <summary>ECO-03: hunger −1 and thirst −2 per tick, never below 0; health is untouched while both are above 0.</summary>
    [Fact]
    public void Decay_Rates()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(Near).Build();
        var a = sim.Agents.All.Single();
        Assert.Equal(Agent.NeedMax, a.Hunger);
        Assert.Equal(Agent.NeedMax, a.Thirst);

        sim.RunTicks(100);
        Assert.Equal(9_900, a.Hunger);
        Assert.Equal(9_800, a.Thirst);
        Assert.Equal(Agent.HealthMax, a.Health);

        a.Thirst = 1;
        a.Hunger = 1;
        sim.RunTicks(3);
        Assert.Equal(0, a.Thirst);
        Assert.Equal(0, a.Hunger);
    }

    /// <summary>ECO-04 / JOB-05: a need job is posted the tick the need drops below 4000 (thirst: after tick 3000 of a
    /// fresh agent). With both needs low, Drink (100) is taken first, then Eat (90) once the drink is done. Need jobs
    /// are claimed at once by their agent.</summary>
    [Fact]
    public void Threshold_PostsDrinkBeforeEat()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(HubOrigin).Stock("water", 10).Stock("berries", 10)
            .Agent(Near).Build();
        var a = sim.Agents.All.Single();

        sim.RunTicks(3000);
        Assert.Equal(4_000, a.Thirst);           // not yet below the threshold
        Assert.Null(JobOf(sim, a));
        sim.Tick();
        var drink = JobOf(sim, a);
        Assert.NotNull(drink);
        Assert.Equal(JobKind.Drink, drink!.Kind);
        Assert.Equal(100, drink.Priority);
        Assert.Equal(a.Id, drink.ClaimedBy);
        RunUntil(sim, () => JobOf(sim, a) is null, 200);
        Assert.True(a.Thirst >= NeedsSystem.Sated);

        // Both needs below the threshold at once: Drink first.
        a.Thirst = 4_001;
        a.Hunger = 4_000;
        sim.Tick();
        Assert.Equal(JobKind.Drink, JobOf(sim, a)!.Kind);
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Eat);
        RunUntil(sim, () => JobOf(sim, a) is { Kind: JobKind.Eat }, 200);
        Assert.True(a.Thirst >= NeedsSystem.Sated);
        Assert.Equal(90, JobOf(sim, a)!.Priority);
        RunUntil(sim, () => JobOf(sim, a) is null, 200);
        Assert.True(a.Hunger >= NeedsSystem.Sated);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>ECO-05: one unit at a time until the need is at least 9000 (3 berries from 3999, 2 water from 3999),
    /// or until storage runs out (1 water left: the job still ends cleanly).</summary>
    [Fact]
    public void Consume_RestoresUntil9000()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(HubOrigin).Stock("berries", 10).Stock("water", 3)
            .Agent(Near).Build();
        var a = sim.Agents.All.Single();
        sim.Tick();   // regions are built at the end of the first tick; before that nothing is reachable

        a.Hunger = 4_000;
        sim.Tick();
        var eat = JobOf(sim, a)!;
        Assert.Equal(JobKind.Eat, eat.Kind);
        Assert.Contains(eat.Reservations, r => r.Kind == ReservationKind.StorageOut && r.Item == Item("berries") && r.Count == 3);
        RunUntil(sim, () => JobOf(sim, a) is null, 200);
        Assert.Equal(7, Stored(sim, "berries"));
        Assert.True(a.Hunger >= NeedsSystem.Sated);

        a.Thirst = 4_001;
        sim.Tick();
        RunUntil(sim, () => JobOf(sim, a) is null, 200);
        Assert.Equal(1, Stored(sim, "water"));
        Assert.True(a.Thirst >= NeedsSystem.Sated);

        // Only one water left: drink it and stop; nothing fails.
        a.Thirst = 2_001;
        sim.Tick();
        Assert.Equal(JobKind.Drink, JobOf(sim, a)!.Kind);
        RunUntil(sim, () => JobOf(sim, a) is null, 200);
        Assert.Equal(0, Stored(sim, "water"));
        Assert.InRange(a.Thirst, 6_900, 7_000);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>ECO-04 (M7-T4, G3 answer 6): the Eat job takes the food item with the most unpromised units in reachable
    /// storage, ties by lower item id (berries before potato). With 6 berries and 12 potatoes stored, potatoes are
    /// eaten while they outnumber the berries, berries at a tie, and the choice flips back as the counts change.</summary>
    [Fact]
    public void Eat_PicksMostPlentifulFood()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(HubOrigin).Stock("berries", 6).Stock("potato", 12)
            .Agent(Near).Build();
        var a = sim.Agents.All.Single();
        sim.Tick();

        var eaten = new List<string>();
        while (Stored(sim, "berries") + Stored(sim, "potato") > 0)
        {
            int berries = Stored(sim, "berries"), potatoes = Stored(sim, "potato");
            a.Hunger = 3_999;
            sim.Tick();
            var eat = JobOf(sim, a)!;
            Assert.Equal(JobKind.Eat, eat.Kind);
            var item = eat.Reservations.Single(r => r.Kind == ReservationKind.StorageOut).Item;
            Assert.Equal(potatoes > berries ? Item("potato") : Item("berries"), item);
            eaten.Add(item == Item("potato") ? "P" : "B");
            RunUntil(sim, () => JobOf(sim, a) is null, 200);
        }
        // 12P/6B -> P P P (6/6 tie) B (6P/3B) P P (2P/3B) B P
        Assert.Equal("PPPBPPBP", string.Concat(eaten));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>ECO-04 (M7-T4): the counts are summed over every reachable storage, then the nearest storage holding the
    /// chosen item serves it. Hub: 10 berries + 2 potatoes; a farther warehouse: 10 potatoes. 12 potatoes beat 10
    /// berries, so the hub's 2 potatoes go first; then 10/10 is a tie and the hub's berries win; then 10 potatoes beat
    /// 7 berries and the agent walks to the warehouse for them.</summary>
    [Fact]
    public void Eat_CountsFoodAcrossReachableStorages()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(HubOrigin).Stock("berries", 10).Stock("potato", 2)
            .Storage("warehouse", new Int3(20, 5, 20)).Stock("potato", 10)
            .Agent(Near).Build();
        var a = sim.Agents.All.Single();
        var hub = sim.Buildings.All.First();
        var warehouse = sim.Buildings.All.Last();
        sim.Tick();

        var expected = new[] { ("potato", hub.Id), ("berries", hub.Id), ("potato", warehouse.Id) };
        foreach (var (item, building) in expected)
        {
            a.Hunger = 3_999;
            sim.Tick();
            var res = JobOf(sim, a)!.Reservations.Single(r => r.Kind == ReservationKind.StorageOut);
            Assert.Equal(Item(item), res.Item);
            Assert.Equal(building, res.Building);
            RunUntil(sim, () => JobOf(sim, a) is null, 400);
        }
        Assert.Equal(8, warehouse.Stored.GetValueOrDefault(Item("potato").Value));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>ECO-06: health regenerates 1 per 10 ticks while both needs are above 0, drops 1 per tick at 0 hunger or
    /// thirst (no regeneration then), and never exceeds 1000.</summary>
    [Fact]
    public void HealthRegen_WhenFed()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(Near).Build();
        var a = sim.Agents.All.Single();
        a.Health = 500;
        sim.RunTicks(100);
        Assert.Equal(510, a.Health);

        a.Hunger = 0;
        sim.RunTicks(100);
        Assert.Equal(410, a.Health);
        Assert.True(a.IsAlive);

        a.Hunger = Agent.NeedMax;
        a.Health = Agent.HealthMax - 1;
        sim.RunTicks(50);
        Assert.Equal(Agent.HealthMax, a.Health);
    }

    /// <summary>ECO-06 tie: both needs at 0 when health runs out → Dehydrated.</summary>
    [Fact]
    public void BothNeedsZero_ThirstWinsTie()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(Near).Build();
        var a = sim.Agents.All.Single();
        a.Hunger = 0; a.Thirst = 0; a.Health = 3;
        sim.RunTicks(3);
        Assert.False(a.IsAlive);
        Assert.Equal(DeathCause.Dehydrated, a.Death);
    }

    /// <summary>ECO-04: with no water in any storage the agent posts nothing and looks again every 100 ticks; the HUD
    /// query reports "No water" meanwhile. Water that arrives is only noticed at the next try.</summary>
    [Fact]
    public void NoStock_RetriesEvery100Ticks_AndReportsShortage()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(HubOrigin).Stock("berries", 5).Agent(Near).Build();
        var a = sim.Agents.All.Single();
        var hub = sim.Buildings.All.First();
        Assert.False(NeedsSystem.NoWater(sim));

        sim.Tick();
        a.Thirst = 3_000;
        sim.Tick();                               // the try at tick 1 finds no water
        Assert.Null(JobOf(sim, a));
        Assert.True(NeedsSystem.NoWater(sim));
        Assert.False(NeedsSystem.NoFood(sim));   // nobody is hungry

        hub.Stored[Item("water").Value] = 5;
        sim.RunTicks(98);                         // ticks 2..99
        Assert.Null(JobOf(sim, a));
        Assert.Empty(sim.Jobs.All);
        sim.RunTicks(2);                          // tick 101 tries again
        Assert.Equal(JobKind.Drink, JobOf(sim, a)!.Kind);
        Assert.False(NeedsSystem.NoWater(sim));
    }

    /// <summary>SAV-03 with the new need state: a save taken while an agent waits for water and another eats loads
    /// with an equal hash and ticks on identically.</summary>
    [Fact]
    public void NeedState_SurvivesSaveLoad()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(HubOrigin).Stock("berries", 5)
            .Agent(Near).Agent(new Int3(8, 5, 8)).Build();
        var agents = sim.Agents.All.ToList();
        sim.Tick();
        agents[0].Thirst = 3_000;
        agents[1].Hunger = 3_000;
        sim.RunTicks(3);
        Assert.Contains(sim.Jobs.All, j => j.Kind == JobKind.Eat);

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        for (int i = 0; i < 3; i++)
        {
            sim.RunTicks(100);
            loaded.RunTicks(100);
            Assert.Equal(sim.StateHash(), loaded.StateHash());
        }
    }

    /// <summary>docs/00-overview.md: the Great Hall starts with 40 berries and 30 water. Since M11-T2 (ADR-077) the
    /// building supplies (logs, stone) start in the wagon beside it; the stock is data (<c>startStock</c>).</summary>
    [Fact]
    public void Seed1_HubStartingStock()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var hub = sim.Buildings.All.Single(b => b.Def.Id == "hub");
        Assert.Equal(40, hub.Stored.GetValueOrDefault(Item("berries").Value));
        Assert.Equal(30, hub.Stored.GetValueOrDefault(Item("water").Value));
        Assert.Equal(0, hub.Stored.GetValueOrDefault(Item("log").Value));
        var wagon = sim.Buildings.All.Single(b => b.Def.Id == "wagon");
        Assert.Equal(40, wagon.Stored.GetValueOrDefault(Item("log").Value));
        Assert.Equal(60, wagon.Stored.GetValueOrDefault(Item("stone").Value));
    }
}
