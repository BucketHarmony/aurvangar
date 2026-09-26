using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Xunit;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M4-T8: loose piles hauled to storage (JOB-10), storage reservations and caps (BLD-10..12), ECO-08 drops.</summary>
[Trait("Category", "Scenario")]
public class HaulScenarioTests
{
    private static ItemId Item(string key) => TestContent.Db.Item(key);

    private static void RunUntil(Simulation sim, Func<bool> done, int max, Action? eachTick = null)
    {
        for (int i = 0; i < max && !done(); i++) { sim.Tick(); eachTick?.Invoke(); }
        Assert.True(done(), $"condition not reached within {max} ticks");
    }

    private static int Stored(Buildings.Building b, string item) => b.Stored.GetValueOrDefault(Item(item).Value);

    private static IEnumerable<Job> Hauls(Simulation sim) => sim.Jobs.All.Where(j => j.Kind == JobKind.Haul);

    /// <summary>The storage a haul job delivers to.</summary>
    private static int DeliversTo(Job j) => j.Steps.Single(s => s.Kind == StepKind.DeliverTo).Target;

    /// <summary>Each storage's contents never exceed its caps, counting what claimed hauls are bringing.</summary>
    private static void AssertCapsHold(Simulation sim)
    {
        foreach (var b in sim.Buildings.All)
        {
            var s = b.Def.Storage!;
            foreach (var (item, n) in b.Stored)
                if (s.PerItemCapacity > 0) Assert.True(n <= s.PerItemCapacity, $"{b.Def.Id} holds {n} of item {item}");
            if (s.Capacity > 0) Assert.True(b.Stored.Values.Sum() <= s.Capacity);
        }
    }

    /// <summary>JOB-10: one haul job per pile, to the nearest storage (Manhattan to its entrance) that accepts the item,
    /// ties by building id; the piles end up in those storages.</summary>
    [Fact]
    public void LoosePiles_HauledToNearestStorage()
    {
        // Warehouse first (id 1), entrance (24,5,24). Hub (id 2) footprint x 2..4, z 2..4, entrance (3,5,1).
        var sim = new ScenarioBuilder().Ground(4)
            .Storage("warehouse", new Int3(24, 5, 25))
            .Hub(new Int3(2, 5, 2))
            .Pile(new Int3(6, 5, 6), "stone", 3)      // near the hub
            .Pile(new Int3(22, 5, 20), "log", 5)      // near the warehouse
            .Pile(new Int3(13, 5, 13), "potato", 2)   // 22 from both: tie → warehouse (lower id)
            .Pile(new Int3(20, 5, 20), "water", 1)    // nearer the warehouse, which rejects water (BLD-11)
            .Agent(new Int3(14, 5, 14)).Build();
        var wh = sim.Buildings.All.First(b => b.Def.Id == "warehouse");
        var hub = sim.Buildings.All.First(b => b.Def.Id == "hub");

        sim.Tick();
        var hauls = Hauls(sim).ToList();
        Assert.Equal(4, hauls.Count);   // one per pile
        Assert.Equal(hub.Id.Value, DeliversTo(hauls.Single(j => j.Target == new Int3(6, 5, 6))));
        Assert.Equal(wh.Id.Value, DeliversTo(hauls.Single(j => j.Target == new Int3(22, 5, 20))));
        Assert.Equal(wh.Id.Value, DeliversTo(hauls.Single(j => j.Target == new Int3(13, 5, 13))));
        Assert.Equal(hub.Id.Value, DeliversTo(hauls.Single(j => j.Target == new Int3(20, 5, 20))));

        RunUntil(sim, () => sim.Piles.Count == 0 && sim.Jobs.Count == 0, 3000);
        Assert.Equal(3, Stored(hub, "stone"));
        Assert.Equal(1, Stored(hub, "water"));
        Assert.Equal(5, Stored(wh, "log"));
        Assert.Equal(2, Stored(wh, "potato"));
        Assert.Equal(4, sim.Counters.JobsCompleted);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>JOB-10, BLD-10: no haul is posted for an item with no room anywhere; a part that fits is hauled and the
    /// rest stays.</summary>
    [Fact]
    public void StorageFull_PilesStay()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(new Int3(20, 5, 20)).Stock("stone", 95).Stock("log", 100)
            .Pile(new Int3(8, 5, 8), "stone", 10)
            .Pile(new Int3(8, 5, 12), "log", 4)
            .Agent(new Int3(5, 5, 5)).Agent(new Int3(5, 5, 7)).Build();
        var hub = sim.Buildings.All.Single();

        sim.Tick();
        var haul = Hauls(sim).Single();   // logs: the hub is full (per-item cap 100)
        Assert.Equal(new Int3(8, 5, 8), haul.Target);
        Assert.Equal(5, haul.Steps.Single(s => s.Kind == StepKind.PickUp).Count);

        RunUntil(sim, () => Stored(hub, "stone") == 100, 1000, () => AssertCapsHold(sim));
        sim.RunTicks(300);
        Assert.Equal(new ItemStack(Item("stone"), 5), sim.Piles.At(new Int3(8, 5, 8)));
        Assert.Equal(new ItemStack(Item("log"), 4), sim.Piles.At(new Int3(8, 5, 12)));
        Assert.Empty(Hauls(sim));
        Assert.Equal(0, sim.Counters.JobsFailed);

        hub.Stored[Item("log").Value] = 90;   // room again: the logs are hauled
        RunUntil(sim, () => Stored(hub, "log") == 94, 1000);
        Assert.Equal(1, sim.Piles.Count);
    }

    /// <summary>ECO-08: a stack dropped on a cell holding another item lands on the first free spiral cell; both piles
    /// are then hauled.</summary>
    [Fact]
    public void DropOnOccupiedPile_SpiralsToFreeCell()
    {
        var at = new Int3(10, 5, 10);
        var sim = new ScenarioBuilder().Ground(4).Hub(new Int3(20, 5, 20)).Pile(at, "log", 4).Agent(at).Build();
        var a = sim.Agents.All.Single();
        var hub = sim.Buildings.All.Single();
        a.Carried = new ItemStack(Item("stone"), 3);

        sim.Tick();   // an idle agent drops a leftover stack on its cell before it looks for work
        Assert.True(a.Carried.IsEmpty);
        Assert.Equal(new ItemStack(Item("log"), 4), sim.Piles.At(at));
        Assert.Equal(new ItemStack(Item("stone"), 3), sim.Piles.At(at + new Int3(0, 0, -1)));   // first spiral cell

        RunUntil(sim, () => Stored(hub, "stone") == 3 && Stored(hub, "log") == 4, 2000);
        Assert.Equal(0, sim.Piles.Count);
        Assert.Equal(0, sim.Jobs.Count);
    }

    /// <summary>JOB-01 carry cap: a pile larger than 10 is hauled in several trips, one job at a time.</summary>
    [Fact]
    public void BigPile_HauledInTripsOfTen()
    {
        var pile = new Int3(8, 5, 8);
        var sim = new ScenarioBuilder().Ground(4).Hub(new Int3(20, 5, 20)).Pile(pile, "stone", 25)
            .Agent(new Int3(5, 5, 5)).Agent(new Int3(5, 5, 7)).Build();
        var hub = sim.Buildings.All.Single();
        var counts = new List<int>();

        RunUntil(sim, () => Stored(hub, "stone") == 25, 3000, () =>
        {
            Assert.True(Hauls(sim).Count() <= 1, "more than one haul job for one pile");
            foreach (var j in Hauls(sim))
                if (j.IsClaimed && !counts.Contains((int)j.Id.Value))
                {
                    counts.Add((int)j.Id.Value);
                    Assert.True(j.Steps.Single(s => s.Kind == StepKind.PickUp).Count <= 10);
                }
        });
        Assert.Equal(3, sim.Counters.JobsCompleted);
        Assert.Equal(0, sim.Piles.Count);
    }

    /// <summary>JOB-10 "capacity is reserved on claim": two agents, 15 units of room, two piles of 10. The second haul
    /// shrinks to what is left instead of failing at delivery.</summary>
    [Fact]
    public void Reservations_TwoHaulers_NeverOverfill()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(new Int3(20, 5, 20)).Stock("stone", 85)
            .Pile(new Int3(8, 5, 8), "stone", 10)
            .Pile(new Int3(8, 5, 10), "stone", 10)
            .Agent(new Int3(5, 5, 5)).Agent(new Int3(5, 5, 7)).Build();
        var hub = sim.Buildings.All.Single();

        bool both = false;   // the second haul is sized to the room left, so both agents haul at once
        RunUntil(sim, () => Stored(hub, "stone") == 100, 1500, () =>
        {
            AssertCapsHold(sim);
            if (Hauls(sim).Count(j => j.IsClaimed) == 2) both = true;
            Assert.True(sim.Jobs.ReservedIn(hub.Id, Item("stone")) + Stored(hub, "stone") <= 100);
        });
        Assert.True(both, "the two hauls never ran at the same time");
        sim.RunTicks(200);
        Assert.Equal(5, sim.Piles.All.Sum(p => p.Stack.Count));
        Assert.Equal(0, sim.Jobs.Count);
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.Equal(0, sim.Jobs.ReservedIn(hub.Id, Item("stone")));
    }

    /// <summary>ADR-028 follow-up: a haul that fails after its pick-up drops the stack; the stale job is withdrawn and
    /// the new pile gets a fresh haul once there is room.</summary>
    [Fact]
    public void HaulFailsAfterPickUp_NewPileHauledLater()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(new Int3(20, 5, 20)).Pile(new Int3(8, 5, 8), "stone", 6)
            .Agent(new Int3(5, 5, 5)).Build();
        var hub = sim.Buildings.All.Single();
        var a = sim.Agents.All.Single();

        RunUntil(sim, () => !a.Carried.IsEmpty, 300);
        hub.Stored[Item("stone").Value] = 100;   // behind the job's back: DeliverTo will be StorageFull
        RunUntil(sim, () => sim.Counters.JobsFailed == 1, 500);
        sim.Tick();
        Assert.True(a.Carried.IsEmpty);
        Assert.Equal(6, sim.Piles.All.Single().Stack.Count);
        Assert.Empty(Hauls(sim));   // no room: the stale job is gone and no new one is posted

        hub.Stored[Item("stone").Value] = 0;
        RunUntil(sim, () => Stored(hub, "stone") == 6, 1500);
        Assert.Equal(0, sim.Piles.Count);
        Assert.Equal(0, sim.Jobs.Count);
        Assert.Equal(1, sim.Counters.JobsFailed);
    }

    /// <summary>BLD-12: Buildings.Totals sums every storage building, recomputed each tick.</summary>
    [Fact]
    public void StorageTotals_SumAllStorage()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Hub(new Int3(2, 5, 2)).Stock("stone", 7).Stock("water", 3)
            .Storage("warehouse", new Int3(20, 5, 20)).Stock("stone", 5).Stock("log", 2)
            .Build();
        sim.Tick();
        Assert.Equal(new[] { (Item("log").Value, 2), (Item("stone").Value, 12), (Item("water").Value, 3) }
            .OrderBy(p => p.Item1), sim.Buildings.Totals.Select(p => (p.Key, p.Value)));

        sim.Buildings.All.First().Stored.Remove(Item("water").Value);
        sim.Tick();
        Assert.False(sim.Buildings.Totals.ContainsKey(Item("water").Value));
    }
}
