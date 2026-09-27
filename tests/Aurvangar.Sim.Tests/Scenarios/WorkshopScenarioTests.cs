using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M11-T4: crafting.md acceptance scenarios 1..5 and 9 (Craft job half), CRF-10..14. Worlds are flat stone to
/// y = 4. The workshop at (10,5,10) covers x 10..11, z 10..11, y 5..6; its entrance and stand cell is (10,5,9). The hall
/// is at (20,5,20).</summary>
[Trait("Category", "Scenario")]
public class WorkshopScenarioTests
{
    private static readonly Int3 WsOrigin = new(10, G, 10);
    private static readonly Int3 HallOrigin = new(20, G, 20);

    private static Building Place(Simulation sim, string def, Int3? at = null) =>
        sim.Buildings.PlacePrebuilt(sim.Content.Building(def), at ?? WsOrigin, 0);

    private static int Stock(Simulation sim, string item) => Economy.Stock(sim, sim.Content.Item(item));

    private static bool NoJobsFor(Simulation sim, Building ws) =>
        !sim.Jobs.All.Any(j => j.Kind is JobKind.Craft or JobKind.Unload && Workshops.WorkshopOf(j) == ws.Id);

    private static bool Settled(Simulation sim) =>
        sim.Piles.Count == 0 && sim.Agents.All.All(a => a.Carried.IsEmpty);

    /// <summary>Scenario 1: Make 20 planks from hall logs: exactly 20 are made, all reach storage through Unload jobs,
    /// 10 logs are taken, the order is removed with WorkshopOrderDone, and no job fails.</summary>
    [Fact]
    public void Sawmill_Make20_Exactly20PlanksToStorage_OrderRemoved()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HallOrigin).Stock("log", 20).Stock("water", 10)
            .Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15)).Build();
        var mill = Place(sim, "sawmill");
        sim.Enqueue(new SetWorkshopOrder(mill.Id, 0, OrderMode.Make, 20));

        bool doneEvent = false, sawUnload = false, sawCraft = false;
        int maxPlanks = 0;
        RunUntil(sim, () => doneEvent && Stored(Hub(sim), "planks") == 20 && Settled(sim) && NoJobsFor(sim, mill), 4000, () =>
        {
            doneEvent |= sim.Events.Drain().Any(e => e is WorkshopOrderDone d && d.Building == mill.Id && d.Recipe == 0);
            sawUnload |= sim.Jobs.All.Any(j => j.Kind == JobKind.Unload && j.IsClaimed);
            sawCraft |= sim.Jobs.All.Any(j => j.Kind == JobKind.Craft && j.IsClaimed);
            maxPlanks = Math.Max(maxPlanks, Stock(sim, "planks") + PileTotal(sim, "planks"));
        });
        Assert.True(sawCraft && sawUnload);
        Assert.Equal(20, maxPlanks);   // never more than 20 made
        Assert.Empty(mill.Orders);
        Assert.Empty(mill.Stored);
        Assert.Equal(10, Stored(Hub(sim), "log"));
        Assert.Equal(WorkshopStatus.NoOrders, Workshops.StatusOf(sim, mill).Status);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>Scenario 2: Keep 10 cut stone crafts until the colony holds 10 (status Done); building Polished stone
    /// uses 4 and crafting starts again until 10 are back.</summary>
    [Fact]
    public void Stonecutter_Keep10_RefillsAfterUse()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HallOrigin).Stock("stone", 40).Stock("water", 10)
            .Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15)).Build();
        var cutter = Place(sim, "stonecutter");
        sim.Enqueue(new SetWorkshopOrder(cutter.Id, 0, OrderMode.Keep, 10));

        int max = 0;
        void Watch() => max = Math.Max(max, Stock(sim, "cutstone") + PileTotal(sim, "cutstone"));
        RunUntil(sim, () => Workshops.StatusOf(sim, cutter).Status == WorkshopStatus.Done && Stored(Hub(sim), "cutstone") == 10
            && Settled(sim) && NoJobsFor(sim, cutter), 4000, Watch);
        Assert.Equal(10, Stock(sim, "cutstone"));
        Assert.Equal(10, Stored(Hub(sim), "cutstone"));
        Assert.Equal(30, Stored(Hub(sim), "stone"));
        Assert.Equal(10, max);   // CRF-09: the cycle count stops at the target (at most one batch over)
        Assert.Single(cutter.Orders);   // a Keep order stays

        // Two Polished stone blocks cost 2 cut stone each (CRF-02).
        var a = new Int3(4, G, 4);
        var b = new Int3(5, G, 4);
        sim.Enqueue(new DesignateBuild(BuildShape.Line, a, b, 1, BlockId.PolishedStone, false));
        bool recrafted = false;
        RunUntil(sim, () => sim.World.GetBlock(a) == BlockId.PolishedStone && sim.World.GetBlock(b) == BlockId.PolishedStone
            && Stored(Hub(sim), "cutstone") == 10 && Settled(sim) && NoJobsFor(sim, cutter), 4000, () =>
        {
            Watch();
            recrafted |= sim.Jobs.All.Any(j => j.Kind == JobKind.Craft && j.IsClaimed);
        });
        Assert.True(recrafted);
        Assert.Equal(10, Stored(Hub(sim), "cutstone"));
        Assert.Equal(26, Stored(Hub(sim), "stone"));
        Assert.Equal(WorkshopStatus.Done, Workshops.StatusOf(sim, cutter).Status);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>Scenario 3, CRF-13: no stone anywhere reads NoInput (naming stone) and posts no Craft job; stone in the
    /// hall starts it. A sawmill with no orders reads NoOrders and a blueprint NotBuilt.</summary>
    [Fact]
    public void NoInput_StatusAndNoJob_ThenStarts()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HallOrigin).Stock("water", 10)
            .Agent(new Int3(15, G, 15)).Build();
        var cutter = Place(sim, "stonecutter");
        var mill = Place(sim, "sawmill", new Int3(4, G, 4));
        sim.Enqueue(new SetWorkshopOrder(cutter.Id, 0, OrderMode.Make, 5));
        sim.Enqueue(new PlaceBuilding("sawmill", new Int3(4, G, 14), 0));
        for (int t = 0; t < 20; t++) sim.Tick();

        Assert.Equal(new WorkshopState(WorkshopStatus.NoInput, sim.Content.Item("stone")), Workshops.StatusOf(sim, cutter));
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Craft);
        Assert.Equal(WorkshopStatus.NoOrders, Workshops.StatusOf(sim, mill).Status);
        var bp = sim.Buildings.All.Single(b => b.Def.Id == "sawmill" && b.State != BuildingState.Complete);
        Assert.Equal(WorkshopStatus.NotBuilt, Workshops.StatusOf(sim, bp).Status);

        var hall = Hub(sim);
        hall.Stored[sim.Content.Item("stone").Value] = 10;
        sim.Tick();
        Assert.Contains(sim.Jobs.All, j => j.Kind == JobKind.Craft && Workshops.WorkshopOf(j) == cutter.Id);
        bool working = false;
        RunUntil(sim, () => cutter.Orders.Count == 0 && Stored(hall, "cutstone") == 5 && Settled(sim), 3000,
            () => working |= Workshops.StatusOf(sim, cutter).Status == WorkshopStatus.Working);
        Assert.True(working);
        Assert.Equal(5, Stored(hall, "stone"));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>Scenario 4, CRF-14/JOB-07: a crafter made thirsty mid-job drops its logs as a pile and drinks; the logs
    /// are hauled back and the Make order still completes with 20 planks from 10 logs.</summary>
    [Fact]
    public void Crafter_PreemptedByThirst_InputHauledBack_OrderCompletes()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HallOrigin).Stock("log", 20).Stock("water", 10)
            .Agent(new Int3(15, G, 15)).Build();
        var a = sim.Agents.All.Single();
        var mill = Place(sim, "sawmill");
        sim.Enqueue(new SetWorkshopOrder(mill.Id, 0, OrderMode.Make, 20));

        RunUntil(sim, () => sim.Jobs.Get(a.CurrentJob) is { Kind: JobKind.Craft } && a.StepIndex >= 3 && !a.Carried.IsEmpty, 1000);
        for (int t = 0; t < 45 && !a.Carried.IsEmpty; t++) sim.Tick();   // one cycle in: some logs used, some carried
        Assert.Equal(JobKind.Craft, sim.Jobs.Get(a.CurrentJob)!.Kind);
        int carried = a.Carried.Count;
        Assert.InRange(carried, 1, 9);
        int made = mill.Orders.Single().Done;
        Assert.True(made > 0);

        a.Thirst = 4_000;
        sim.Tick();
        Assert.Equal(JobKind.Drink, sim.Jobs.Get(a.CurrentJob)!.Kind);
        Assert.True(a.Carried.IsEmpty);
        Assert.Equal(carried, PileTotal(sim, "log"));

        RunUntil(sim, () => mill.Orders.Count == 0 && Stored(Hub(sim), "planks") == 20 && Settled(sim) && NoJobsFor(sim, mill), 4000);
        Assert.Equal(10, Stored(Hub(sim), "log"));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>CRF-09/10, JOB-07 (ADR-083): a Flee can preempt a crafter during the agents step, after Workshops.Tick has
    /// run, so a released Craft job would sit on the board with its input already picked up and its steps back at 0. A
    /// second idle dwarf could take it in the same tick and craft all k cycles again. The released job is withdrawn at
    /// once instead, and next tick's plan posts a fresh one. The Make order ends at exactly 20.</summary>
    [Fact]
    public void Crafter_PreemptedByFlee_JobWithdrawn_SecondDwarfMakesExactCount()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HallOrigin).Stock("log", 20).Stock("water", 10)
            .Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15)).Build();
        var mill = Place(sim, "sawmill");
        sim.Enqueue(new SetWorkshopOrder(mill.Id, 0, OrderMode.Make, 20));

        Agent? crafter = null;
        RunUntil(sim, () => (crafter = sim.Agents.All.FirstOrDefault(x => sim.Jobs.Get(x.CurrentJob) is { Kind: JobKind.Craft }
            && x.StepIndex >= 3 && !x.Carried.IsEmpty)) is not null, 1000);
        for (int t = 0; t < 45; t++) sim.Tick();   // one cycle in
        var a = crafter!;
        var craftJob = sim.Jobs.Get(a.CurrentJob);
        Assert.Equal(JobKind.Craft, craftJob!.Kind);
        Assert.True(mill.Orders.Single().Done > 0);

        var dry = a.Cell;
        Assert.NotNull(JobRunner.AssignNeed(sim, a, JobKind.Flee, dry, new[] { JobStep.GoTo(dry, GoalMode.Exact) }));
        Assert.Null(sim.Jobs.Get(craftJob.Id));                            // withdrawn, not left for another dwarf
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Craft);

        int maxPlanks = 0;
        RunUntil(sim, () =>
        {
            maxPlanks = Math.Max(maxPlanks, Stock(sim, "planks"));
            return mill.Orders.Count == 0 && Stored(Hub(sim), "planks") == 20 && Settled(sim) && NoJobsFor(sim, mill);
        }, 4000);
        Assert.Equal(20, maxPlanks);
        Assert.Equal(20, Stock(sim, "planks"));
        Assert.Equal(10, Stored(Hub(sim), "log"));
    }

    /// <summary>Scenario 5, CRF-02/CON-17: Wood planks, Polished stone and Slate take planks and cut stone from storage
    /// (no log or stone); digging them refunds planks and cut stone, which are hauled back.</summary>
    [Fact]
    public void RefinedBlocks_BuiltFromRefinedItems_DigRefundsThem()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HallOrigin)
            .Stock("planks", 10).Stock("cutstone", 10).Stock("log", 10).Stock("stone", 10).Stock("water", 10)
            .Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15)).Build();
        var cells = new[] { (new Int3(4, G, 4), BlockId.Planks), (new Int3(6, G, 4), BlockId.PolishedStone), (new Int3(8, G, 4), BlockId.Slate) };
        foreach (var (c, block) in cells) sim.Enqueue(new DesignateBuild(BuildShape.Single, c, c, 1, block, false));
        RunUntil(sim, () => cells.All(x => sim.World.GetBlock(x.Item1) == x.Item2) && Settled(sim), 3000);
        var hall = Hub(sim);
        Assert.Equal(10 - 1, Stored(hall, "planks"));
        Assert.Equal(10 - 2 - 3, Stored(hall, "cutstone"));
        Assert.Equal(10, Stored(hall, "log"));
        Assert.Equal(10, Stored(hall, "stone"));

        sim.Enqueue(new DesignateDig(new Int3(4, G, 4), new Int3(8, G, 4)));
        bool refundPiles = false;
        RunUntil(sim, () => cells.All(x => sim.World.GetBlock(x.Item1) == BlockId.Air) && Settled(sim) && sim.Designations.Count == 0, 3000,
            () => refundPiles |= PileTotal(sim, "planks") > 0 || PileTotal(sim, "cutstone") > 0);
        Assert.True(refundPiles);
        Assert.Equal(10, Stored(hall, "planks"));
        Assert.Equal(10, Stored(hall, "cutstone"));
        Assert.Equal(10, Stored(hall, "log"));
        Assert.Equal(10, Stored(hall, "stone"));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>CRF-14/BLD-09/GRV-07: a deconstructed sawmill drops its 12 planks with the refund and its jobs are
    /// cancelled; a collapsed stonecutter drops its output as falling piles; nothing floats.</summary>
    [Fact]
    public void Workshop_TornDownOrCollapsed_DropsOutput_CancelsJobs()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HallOrigin).Stock("log", 10).Stock("water", 10)
            .Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15)).Build();
        var mill = Place(sim, "sawmill");
        mill.Stored[sim.Content.Item("planks").Value] = 12;
        sim.Enqueue(new SetWorkshopOrder(mill.Id, 0, OrderMode.Make, 4));
        sim.Tick();
        Assert.Contains(sim.Jobs.All, j => j.Kind == JobKind.Craft && Workshops.WorkshopOf(j) == mill.Id);
        Assert.Contains(sim.Jobs.All, j => j.Kind == JobKind.Unload && Workshops.WorkshopOf(j) == mill.Id);

        sim.Enqueue(new Deconstruct(mill.Id));
        sim.Tick();
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Craft);
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Unload && !j.IsClaimed);
        RunUntil(sim, () => sim.Buildings.Get(mill.Id) is null, 2000, () => Assert.Empty(Grounding.FloatingPiles(sim)));
        RunUntil(sim, () => Settled(sim) && sim.Jobs.All.All(j => j.Kind != JobKind.Unload), 3000);
        var hall = Hub(sim);
        Assert.Equal(12, Stored(hall, "planks"));
        Assert.Equal(10 + 16 / 2, Stored(hall, "log"));
        Assert.Equal(0, sim.Counters.JobsFailed);

        // Collapse: the stonecutter's floor is dug away under it.
        var cutter = Place(sim, "stonecutter");
        hall.Stored[sim.Content.Item("stone").Value] = 10;
        cutter.Stored[sim.Content.Item("cutstone").Value] = 5;
        cutter.Orders.Add(new WorkshopOrder { Recipe = 0, Mode = OrderMode.Make, Count = 3 });
        foreach (var c in cutter.FootprintCells().Where(c => c.Y == G)) sim.World.SetBlock(c + Int3.Down, BlockId.Air);
        sim.Tick();
        Assert.Null(sim.Buildings.Get(cutter.Id));
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind is JobKind.Craft or JobKind.Unload && Workshops.WorkshopOf(j) == cutter.Id);
        Assert.Equal(5, PileTotal(sim, "cutstone") + CarriedTotal(sim, "cutstone"));
        Assert.Contains(sim.Piles.All, p => p.Cell.Y == G - 1);   // fell into the pit
        Assert.Empty(Grounding.FloatingPiles(sim));
        Assert.Empty(Grounding.UnsupportedBuildings(sim));
        Assert.Empty(Grounding.Floating(sim));
    }

    /// <summary>Scenario 9 (Craft half), SAV-03: saved while a Craft job is claimed mid-Work with input carried, the
    /// loaded game matches the hash every 100 ticks for 1000 ticks.</summary>
    [Fact]
    public void SaveLoad_MidCraftCycle_ContinuesIdentically()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HallOrigin).Stock("log", 20).Stock("water", 10)
            .Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15)).Build();
        var mill = Place(sim, "sawmill");
        sim.Enqueue(new SetWorkshopOrder(mill.Id, 0, OrderMode.Make, 20));
        RunUntil(sim, () => sim.Agents.All.Any(a => sim.Jobs.Get(a.CurrentJob) is { Kind: JobKind.Craft } && a.StepIndex >= 3
            && !a.Carried.IsEmpty), 1000);
        for (int t = 0; t < 15; t++) sim.Tick();   // part way through a Work step
        var crafter = sim.Agents.All.Single(a => sim.Jobs.Get(a.CurrentJob) is { Kind: JobKind.Craft });
        Assert.False(crafter.Carried.IsEmpty);

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        for (int t = 1; t <= 1000; t++)
        {
            sim.Tick();
            loaded.Tick();
            if (t % 100 == 0) Assert.True(sim.StateHash() == loaded.StateHash(), $"hash differs {t} ticks after load");
        }
        var lmill = loaded.Buildings.Get(mill.Id)!;
        Assert.Empty(lmill.Orders);
        Assert.Equal(20, Stored(Hub(loaded), "planks"));
        Assert.Equal(0, loaded.Counters.JobsFailed);
    }
}
