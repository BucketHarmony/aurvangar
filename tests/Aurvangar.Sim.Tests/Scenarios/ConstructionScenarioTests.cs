using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Paths;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M5-T2: construction flow, BLD-05..09 (buildings.md acceptance scenarios 2 and 5). Worlds are flat stone
/// to y = 4, so agents stand on y = 5. A warehouse at (10,5,10), rotation 0, covers x 10..11, z 10..11, y 5..6 and
/// has its entrance at (10,5,9).</summary>
[Trait("Category", "Scenario")]
public class ConstructionScenarioTests
{
    internal const int G = 5;
    internal static readonly Int3 WhOrigin = new(10, G, 10);
    internal static readonly Int3 WhEntrance = new(10, G, 9);

    internal static ItemId Log => TestContent.Db.Item("log");

    internal static void RunUntil(Simulation sim, Func<bool> done, int max, Action? eachTick = null)
    {
        for (int i = 0; i < max && !done(); i++) { sim.Tick(); eachTick?.Invoke(); }
        Assert.True(done(), $"condition not reached within {max} ticks");
    }

    internal static Building Site(Simulation sim, string def = "warehouse") => sim.Buildings.All.Single(b => b.Def.Id == def);

    internal static Building Hub(Simulation sim) => sim.Buildings.All.Single(b => b.Def.Id == "hub");

    internal static int Stored(Building b, string item) => b.Stored.GetValueOrDefault(TestContent.Db.Item(item).Value);

    internal static int Delivered(Building b, string item) => b.Delivered.GetValueOrDefault(TestContent.Db.Item(item).Value);

    internal static List<Job> Delivers(Simulation sim) => sim.Jobs.All.Where(j => j.Kind == JobKind.Deliver).ToList();

    internal static int DeliverCount(Job j) => j.Steps.Single(s => s.Kind == StepKind.PickUpFromStorage).Count;

    internal static int PileTotal(Simulation sim, string item) =>
        sim.Piles.All.Where(p => p.Stack.Item == TestContent.Db.Item(item)).Sum(p => p.Stack.Count);

    internal static int CarriedTotal(Simulation sim, string item) =>
        sim.Agents.All.Where(a => a.Carried.Item == TestContent.Db.Item(item)).Sum(a => a.Carried.Count);

    /// <summary>Buildings.md scenario 2: two agents build a warehouse from hub logs. Both take part; the logs come out
    /// of the hub; the footprint ends BuildingSolid.</summary>
    [Fact]
    public void Warehouse_BuiltByTwoAgents()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(new Int3(20, G, 20)).Stock("log", 40)
            .Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15)).Build();
        sim.Enqueue(new PlaceBuilding("warehouse", WhOrigin, 0));
        sim.Tick();
        var events = sim.Events.Drain();
        var wh = Site(sim);
        Assert.Contains(events, e => e is BuildingPlaced p && p.Building == wh.Id);
        Assert.Equal(BuildingState.Blueprint, wh.State);

        var workers = new SortedSet<int>();
        bool completedEvent = false;
        RunUntil(sim, () => wh.State == BuildingState.Complete, 3000, () =>
        {
            foreach (var j in sim.Jobs.All)
                if (j.IsClaimed && j.Kind is JobKind.Deliver or JobKind.Construct) workers.Add(j.ClaimedBy.Value);
            completedEvent |= sim.Events.Drain().Any(e => e is BuildingCompleted c && c.Building == wh.Id);
        });

        Assert.True(completedEvent);
        Assert.Equal(2, workers.Count);
        foreach (var c in wh.FootprintCells()) Assert.Equal(BlockId.BuildingSolid, sim.World.GetBlock(c));
        Assert.Equal(20, Stored(Hub(sim), "log"));
        Assert.Equal(wh.Def.BuildTicks, wh.Progress);
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind is JobKind.Deliver or JobKind.Construct);
        Assert.Equal(0, sim.Counters.JobsFailed);

        // The complete warehouse is storage: loose logs are hauled into it (nearer than the hub).
        sim.Piles.Add(new Int3(10, G, 7), Log, 3);
        RunUntil(sim, () => sim.Piles.Count == 0 && CarriedTotal(sim, "log") == 0, 1000);
        Assert.Equal(3, Stored(wh, "log"));
        Assert.Equal(20, Stored(Hub(sim), "log"));
    }

    /// <summary>BLD-06: ceil(remaining / 10) Deliver jobs per site and item, fewer as material is delivered; none
    /// once everything is delivered, then exactly one Construct job.</summary>
    [Fact]
    public void DeliverJobs_MatchRemaining()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(new Int3(20, G, 20)).Build();
        sim.Enqueue(new PlaceBuilding("warehouse", WhOrigin, 0));   // 20 logs
        sim.Enqueue(new PlaceBuilding("levee", new Int3(5, G, 20), 0));   // 2 logs
        sim.RunTicks(3);
        var wh = Site(sim);
        var levee = Site(sim, "levee");

        // No stock and no agents: the jobs are posted anyway (they cannot be claimed yet), and stay stable.
        var whJobs = Delivers(sim).Where(j => j.Target == wh.EntranceCell).ToList();
        Assert.Equal(new[] { 10, 10 }, whJobs.Select(DeliverCount));
        Assert.Equal(new[] { 2 }, Delivers(sim).Where(j => j.Target == levee.EntranceCell).Select(DeliverCount));
        Assert.All(Delivers(sim), j => Assert.Equal(Log, j.Steps[1].Item));

        // Stock appears and an agent works: at every tick the open (unclaimed) delivers cover what is neither
        // delivered nor being carried by claimed delivers, in jobs of at most 10.
        Hub(sim).Stored[Log.Value] = 30;
        sim.Agents.Spawn(new Int3(15, G, 15), "A");
        bool sawConstruct = false;
        RunUntil(sim, () => wh.State == BuildingState.Complete && levee.State == BuildingState.Complete, 3000, () =>
        {
            foreach (var site in new[] { wh, levee })
            {
                if (site.State != BuildingState.Blueprint && site.State != BuildingState.UnderConstruction) continue;
                var jobs = Delivers(sim).Where(j => j.Target == site.EntranceCell).ToList();
                int remaining = site.Def.Cost["log"] - Delivered(site, "log");
                int claimed = jobs.Where(j => j.IsClaimed).Sum(DeliverCount);
                int open = remaining - claimed;
                var unclaimed = jobs.Where(j => !j.IsClaimed).ToList();
                Assert.Equal((open + 9) / 10, unclaimed.Count);
                Assert.Equal(open, unclaimed.Sum(DeliverCount));
                Assert.All(jobs, j => Assert.InRange(DeliverCount(j), 1, 10));
                var constructs = sim.Jobs.All.Count(j => j.Kind == JobKind.Construct && j.Target == site.EntranceCell);
                if (remaining > 0) Assert.Equal(0, constructs);
                else { Assert.Equal(1, constructs); sawConstruct = true; }
            }
        });
        Assert.True(sawConstruct);
        Assert.Equal(8, Stored(Hub(sim), "log"));
    }

    /// <summary>BLD-07: the first delivery turns the blueprint into a construction site. Its footprint stops being
    /// walkable at once (also for fleeing agents and loose piles), and an agent standing inside is moved to the
    /// entrance cell.</summary>
    [Fact]
    public void FirstDelivery_BlocksFootprint_MovesAgents()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Build();
        var carrier = sim.Agents.Spawn(new Int3(9, G, 10), "Carrier");
        var inside = sim.Agents.Spawn(new Int3(11, G, 11), "Inside");
        sim.Piles.Add(new Int3(10, G, 11), TestContent.Db.Item("stone"), 3);
        Assert.Equal(PlacementResult.Ok, sim.Buildings.TryPlaceBlueprint(TestContent.Db.Building("warehouse"), WhOrigin, 0, out var wh));
        sim.RunTicks(2);
        foreach (var c in wh!.FootprintCells().Where(c => c.Y == G)) Assert.True(sim.PathGrid.IsWalkable(c));

        carrier.Carried = new ItemStack(Log, 10);
        Assert.Equal(ActionResult.Ok, sim.Actions.DeliverTo(carrier.Id, wh.Id));

        Assert.Equal(BuildingState.UnderConstruction, wh.State);
        Assert.Equal(10, Delivered(wh, "log"));
        Assert.True(carrier.Carried.IsEmpty);
        foreach (var c in wh.FootprintCells())
        {
            Assert.False(sim.PathGrid.IsWalkable(c), $"{c} is walkable");
            Assert.False(sim.PathGrid.IsStandable(c), $"{c} is standable");
        }
        Assert.Equal(WhEntrance, inside.Cell);
        Assert.Equal(WhEntrance, inside.NextCell);
        // The stone pile in the footprint went to the nearest free cell outside it.
        Assert.True(sim.Piles.At(new Int3(10, G, 11)).IsEmpty);
        Assert.Equal(3, PileTotal(sim, "stone"));
        Assert.DoesNotContain(sim.Piles.All, p => wh.Covers(p.Cell));

        // Paths and flee moves go around the site.
        var path = sim.Pathfinder.FindPath(new Int3(10, G, 8), new Int3(11, G, 13));
        Assert.True(path.Found);
        Assert.DoesNotContain(path.Path, c => wh.Covers(c));
        Span<PathMove> moves = stackalloc PathMove[PathMoves.MaxMoves];
        int n = PathMoves.From(sim.PathGrid, new Int3(9, G, 10), moves, swim: true);
        for (int i = 0; i < n; i++) Assert.False(wh.Covers(moves[i].To));
        sim.Tick();
        Assert.Equal(Regions.None, sim.Regions.RegionOf(new Int3(10, G, 10)));
    }

    /// <summary>BLD-09: cancelling a construction site returns every delivered log (a pile at the entrance, then
    /// hauled home); logs being carried to it are dropped, not lost.</summary>
    [Fact]
    public void CancelBlueprint_FullRefund()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(new Int3(20, G, 20)).Stock("log", 20)
            .Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15)).Build();
        sim.Enqueue(new PlaceBuilding("warehouse", WhOrigin, 0));
        sim.Tick();
        var wh = Site(sim);
        RunUntil(sim, () => Delivered(wh, "log") >= 10, 2000);
        int delivered = Delivered(wh, "log");
        Assert.Equal(BuildingState.UnderConstruction, wh.State);
        int inHub = Stored(Hub(sim), "log"), carried = CarriedTotal(sim, "log");
        Assert.Equal(20, inHub + carried + delivered + PileTotal(sim, "log"));

        sim.Events.Drain();
        sim.Enqueue(new Deconstruct(wh.Id));
        sim.Tick();
        Assert.Null(sim.Buildings.Get(wh.Id));
        Assert.Contains(sim.Events.Drain(), e => e is BuildingRemoved r && r.Building == wh.Id);
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind is JobKind.Deliver or JobKind.Construct);
        Assert.True(PileTotal(sim, "log") >= delivered);
        Assert.Equal(delivered, sim.Piles.All.Where(p => Math.Abs(p.Cell.X - WhEntrance.X) <= 1
            && Math.Abs(p.Cell.Z - WhEntrance.Z) <= 1 && p.Stack.Item == Log).Sum(p => p.Stack.Count));
        foreach (var c in wh.FootprintCells().Where(c => c.Y == G)) Assert.True(sim.PathGrid.IsWalkable(c));
        foreach (var c in wh.FootprintCells()) Assert.Equal(BlockId.Air, sim.World.GetBlock(c));

        RunUntil(sim, () => sim.Piles.Count == 0 && sim.Jobs.Count == 0 && CarriedTotal(sim, "log") == 0, 2000);
        Assert.Equal(20, Stored(Hub(sim), "log"));

        // A blueprint with nothing delivered is simply removed: no pile.
        sim.Enqueue(new PlaceBuilding("levee", new Int3(5, G, 5), 0));
        sim.Tick();
        var levee = Site(sim, "levee");
        sim.Enqueue(new Deconstruct(levee.Id));
        sim.Tick();
        Assert.Null(sim.Buildings.Get(levee.Id));
        Assert.Equal(0, sim.Piles.Count);
    }

    /// <summary>BLD-09 / buildings.md scenario 5: deconstructing a complete building takes a Deconstruct job of half
    /// its build ticks, turns the footprint back to air and drops half its cost (rounded down), plus anything it
    /// stored, as piles at the entrance.</summary>
    [Fact]
    public void Deconstruct_HalfRefund()
    {
        // No hub: nothing hauls the refund piles away.
        var sim = new ScenarioBuilder().Ground(G - 1).Storage("warehouse", WhOrigin).Stock("stone", 5)
            .Agent(new Int3(15, G, 15)).Build();
        var wh = Site(sim);
        var levee = sim.Buildings.PlacePrebuilt(TestContent.Db.Building("levee"), new Int3(20, G, 10), 0);
        sim.Enqueue(new Deconstruct(wh.Id));
        sim.Enqueue(new Deconstruct(levee.Id));
        sim.Tick();
        Assert.Equal(BuildingState.Deconstructing, wh.State);
        Assert.Equal(BuildingState.Deconstructing, levee.State);
        Assert.Equal(2, sim.Jobs.All.Count(j => j.Kind == JobKind.Deconstruct));

        long start = sim.Clock.Tick;
        int workTicks = 0;
        RunUntil(sim, () => sim.Buildings.Get(wh.Id) is null && sim.Buildings.Get(levee.Id) is null, 2000, () =>
        {
            if (sim.Agents.All.Single().CurrentJob is var id && sim.Jobs.Get(id) is { Kind: JobKind.Deconstruct }
                && sim.Agents.All.Single().StepIndex == 1) workTicks++;
        });
        Assert.True(workTicks >= (300 + 40) / 2, $"work ticks {workTicks}");
        foreach (var c in wh.FootprintCells()) Assert.Equal(BlockId.Air, sim.World.GetBlock(c));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(new Int3(20, G, 10)));
        Assert.Equal(10 + 1, PileTotal(sim, "log"));
        Assert.Equal(5, PileTotal(sim, "stone"));
        Assert.Empty(sim.Jobs.All);
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.True(sim.Clock.Tick - start >= 170);
    }
}
