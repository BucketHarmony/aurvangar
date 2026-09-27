using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M8-T3: deconstructing built blocks (construction.md CON-10, CON-17, CON-18, scenario 7). Worlds are stone
/// to y = 8, so dwarves walk on y = 9; the hub is at (20,9,20) with its entrance at (21,9,19). Built blocks are set
/// straight into the world; how they got there is M8-T2's business.</summary>
[Trait("Category", "Scenario")]
public class BlockDeconstructScenarioTests
{
    private const int GY = 9;
    private static readonly Int3 HubOrigin = new(20, GY, 20);

    private static List<string> Rejections(Simulation sim, string tag) =>
        sim.Events.Drain().OfType<CommandRejected>().Where(r => r.Command == tag).Select(r => r.Reason).ToList();

    private static List<Int3> MarkedCells(Simulation sim) => sim.Designations.All.Select(m => m.Item1).ToList();

    [Fact]
    public void DigBuiltBlock_RefundsFullCost()
    {
        var masonry = new Int3(8, GY, 8);
        var planks = new Int3(12, GY, 8);
        var polished = new Int3(16, GY, 8);
        var sim = new ScenarioBuilder().Ground(GY - 1)
            .FillBox(masonry, masonry, BlockId.Masonry)
            .FillBox(planks, planks, BlockId.Planks)
            .FillBox(polished, polished, BlockId.PolishedStone)
            .Hub(HubOrigin)
            .Agent(new Int3(12, GY, 14)).Agent(new Int3(13, GY, 14)).Build();

        sim.Enqueue(new DesignateDeconstructBlocks(new Int3(6, GY - 2, 6), new Int3(18, GY + 2, 10)));
        sim.Tick();
        var cells = new[] { masonry, planks, polished };
        Assert.Equal(cells.OrderBy(c => sim.World.Index(c)), MarkedCells(sim).OrderBy(c => sim.World.Index(c)));

        // The dig takes the block's hardness (Masonry 40, Planks 20, PolishedStone 60) in Work ticks.
        var hardness = new Dictionary<Int3, int> { [masonry] = 40, [planks] = 20, [polished] = 60 };
        foreach (var c in cells)
        {
            var job = sim.Jobs.All.Single(j => j.Kind == JobKind.Dig && j.Target == c);
            Assert.Equal(hardness[c], job.Steps.Single(s => s.Kind == StepKind.Work).Ticks);
        }

        var worked = cells.ToDictionary(c => c, _ => 0);
        var dropped = new Dictionary<Int3, (string Item, int Count)>();
        RunUntil(sim, () => dropped.Count == 3, 3000, () =>
        {
            foreach (var a in sim.Agents.All)
                if (sim.Jobs.Get(a.CurrentJob) is { Kind: JobKind.Dig } j && j.Steps[a.StepIndex].Kind == StepKind.Work)
                    worked[j.Target]++;
            foreach (var c in cells)
                if (!dropped.ContainsKey(c) && sim.World.GetBlock(c) == BlockId.Air)
                {
                    var pile = sim.Piles.At(c);
                    dropped[c] = (sim.Content.ItemDef(pile.Item).Id, pile.Count);
                }
        });
        Assert.Equal(("stone", 1), dropped[masonry]);
        Assert.Equal(("planks", 1), dropped[planks]);      // M11-T4: refined costs (CRF-02)
        Assert.Equal(("cutstone", 2), dropped[polished]);
        foreach (var c in cells) Assert.True(worked[c] >= hardness[c], $"{c}: {worked[c]} work ticks");

        // The refund piles are hauled to storage like any other.
        RunUntil(sim, () => sim.Piles.Count == 0
            && CarriedTotal(sim, "stone") + CarriedTotal(sim, "planks") + CarriedTotal(sim, "cutstone") == 0, 2000);
        Assert.Equal(1, Stored(Hub(sim), "stone"));
        Assert.Equal(1, Stored(Hub(sim), "planks"));
        Assert.Equal(2, Stored(Hub(sim), "cutstone"));
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.Empty(sim.Designations.All);
    }

    [Fact]
    public void DeconstructBlocks_MarksOnlyBuiltBlocks()
    {
        var sim = new ScenarioBuilder().Ground(GY - 1)
            .FillBox(new Int3(10, GY, 12), new Int3(14, GY + 1, 12), BlockId.Masonry)
            .FillBox(new Int3(4, GY, 4), new Int3(5, GY + 1, 4), BlockId.Stone)   // a natural post, not built
            .Build();
        var wall = new List<Int3>();
        for (int y = GY; y <= GY + 1; y++)
            for (int x = 10; x <= 14; x++) wall.Add(new Int3(x, y, 12));

        // The box takes in the ground under and around the wall, and the stone post: only the wall is marked.
        sim.Enqueue(new DesignateDeconstructBlocks(new Int3(2, GY - 3, 2), new Int3(16, GY + 4, 14)));
        sim.Tick();
        Assert.Empty(Rejections(sim, "DesignateDeconstructBlocks"));
        Assert.Equal(wall.OrderBy(c => sim.World.Index(c)), MarkedCells(sim).OrderBy(c => sim.World.Index(c)));
        Assert.All(sim.Designations.All, m => Assert.Equal(DesignationMark.Dig, m.Item2));

        // A box of ground and a box outside the world hold no built block.
        sim.Enqueue(new DesignateDeconstructBlocks(new Int3(20, 1, 20), new Int3(25, GY + 3, 25)));
        sim.Enqueue(new DesignateDeconstructBlocks(new Int3(100, 1, 100), new Int3(120, 3, 120)));
        sim.Tick();
        Assert.Equal(new[] { "NothingToDeconstruct", "NothingToDeconstruct" }, Rejections(sim, "DesignateDeconstructBlocks"));
        Assert.Equal(wall.Count, sim.Designations.Count);

        // DesignateDig marks built blocks too (they are diggable), along with the ground.
        sim.Enqueue(new DesignateDig(new Int3(14, GY - 1, 12), new Int3(14, GY + 1, 12)));
        sim.Tick();
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(new Int3(14, GY - 1, 12)));

        // The command is logged and survives a save (SAV-01).
        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.Commands.Log, loaded.Commands.Log);
        Assert.Contains(loaded.Commands.Log, e => e.Command is DesignateDeconstructBlocks);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
    }

    /// <summary>Scenario 7. A 4-high Masonry pillar at (10,9..12,10) stands on the ground alone; a stone mound whose top
    /// is walkable at y = 11 touches it only diagonally, so its top is in reach. A 4-long Masonry span at y = 10 juts out
    /// of a 2-high stone post at (20,9..10,5), with air under it.</summary>
    [Fact]
    public void Tower_ComesDownTopFirst_NoBlockEverUngrounded()
    {
        var sim = new ScenarioBuilder().Ground(GY - 1)
            .FillBox(new Int3(10, GY, 10), new Int3(10, GY + 3, 10), BlockId.Masonry)
            .FillBox(new Int3(11, GY, 11), new Int3(13, GY + 1, 13), BlockId.Stone)
            .FillBox(new Int3(14, GY, 12), new Int3(14, GY, 12), BlockId.Stone)   // a step onto the mound
            .FillBox(new Int3(20, GY, 5), new Int3(20, GY + 1, 5), BlockId.Stone)
            .FillBox(new Int3(21, GY + 1, 5), new Int3(24, GY + 1, 5), BlockId.Masonry)
            .Hub(HubOrigin)
            .Agent(new Int3(15, GY, 15)).Agent(new Int3(16, GY, 15)).Agent(new Int3(17, GY, 15)).Build();
        var pillar = Enumerable.Range(GY, 4).Select(y => new Int3(10, y, 10)).ToList();
        var span = Enumerable.Range(21, 4).Select(x => new Int3(x, GY + 1, 5)).ToList();
        Assert.Empty(Grounding.Floating(sim));

        sim.Enqueue(new DesignateDeconstructBlocks(new Int3(8, GY - 2, 3), new Int3(26, GY + 6, 12)));
        sim.Tick();
        Assert.Equal(8, sim.Designations.Count);

        var removedAt = new Dictionary<Int3, long>();
        RunUntil(sim, () => removedAt.Count == 8, 6000, () =>
        {
            foreach (var c in pillar.Concat(span))
                if (!removedAt.ContainsKey(c) && sim.World.GetBlock(c) == BlockId.Air) removedAt[c] = sim.Clock.Tick;
            Assert.Empty(Grounding.Floating(sim));   // CON-09 after every tick
            Assert.DoesNotContain(sim.Designations.All, m => m.Item2 == DesignationMark.DigUnreachable);
            // A block others rest on is never offered as a job: everything above it on the pillar, or beyond it on
            // the span, is gone first.
            foreach (var j in sim.Jobs.All.Where(j => j.Kind == JobKind.Dig))
                foreach (var line in new[] { pillar, span })
                    if (line.IndexOf(j.Target) is var i && i >= 0)
                        Assert.True(line.Skip(i + 1).All(c => sim.World.GetBlock(c) == BlockId.Air),
                            $"dig job posted for {j.Target} while a block rests on it");
        });

        // The pillar comes down top-first, the span from its free end.
        for (int i = 1; i < pillar.Count; i++) Assert.True(removedAt[pillar[i]] < removedAt[pillar[i - 1]]);
        for (int i = 1; i < span.Count; i++) Assert.True(removedAt[span[i]] < removedAt[span[i - 1]]);
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.Empty(sim.Designations.All);
        Assert.Equal(BlockId.Stone, sim.World.GetBlock(new Int3(20, GY + 1, 5)));   // ground is never marked
    }

    /// <summary>A 2-high Masonry pillar at (10,9..10,10); the ground under it and the cell in front of that are marked
    /// for digging.</summary>
    [Fact]
    public void DigUnderBuiltBlock_WaitsAndActionBlocked()
    {
        var under = new Int3(10, GY - 1, 10);
        var front = new Int3(10, GY - 1, 11);
        var sim = new ScenarioBuilder().Ground(GY - 1)
            .FillBox(new Int3(10, GY, 10), new Int3(10, GY + 1, 10), BlockId.Masonry)
            .Hub(HubOrigin)
            .Agent(new Int3(14, GY, 14)).Agent(new Int3(15, GY, 14)).Build();
        sim.Enqueue(new DesignateDig(under, front));
        sim.Tick();

        for (int t = 0; t < 800; t++)
        {
            sim.Tick();
            Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Dig && j.Target == under);
        }
        Assert.Equal(BlockId.Air, sim.World.GetBlock(front));   // the cell in front was dug, which exposed `under`
        Assert.True(DesignationSystem.Exposed(sim.World, under));
        Assert.True(sim.World.IsSolid(under));
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(under));   // waiting, never red
        Assert.Equal(0, sim.Counters.JobsFailed);

        // The action API keeps the invariant too: a dwarf in reach gets Blocked and nothing changes.
        var probe = new ScenarioBuilder().Ground(GY - 1)
            .FillBox(new Int3(10, GY, 10), new Int3(10, GY, 10), BlockId.Masonry)
            .Agent(new Int3(11, GY, 10)).Build();
        var agent = probe.Agents.All.Single();
        Assert.Equal(ActionResult.Blocked, probe.Actions.Dig(agent.Id, new Int3(10, GY - 1, 10)));
        Assert.True(probe.World.IsSolid(new Int3(10, GY - 1, 10)));
        Assert.Equal(ActionResult.Ok, probe.Actions.Dig(agent.Id, new Int3(11, GY - 1, 11)));   // not under the block

        // Once the pillar is deconstructed, the dig goes ahead.
        sim.Enqueue(new DesignateDeconstructBlocks(new Int3(10, GY, 10), new Int3(10, GY + 1, 10)));
        RunUntil(sim, () => !sim.World.IsSolid(under), 3000, () => Assert.Empty(Grounding.Floating(sim)));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(new Int3(10, GY, 10)));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    [Fact]
    public void DeconstructLeveeUnderBlocks_RejectedSupportsBlocks()
    {
        var levee = TestContent.Db.Building("levee");
        var at = new Int3(15, GY, 15);

        // A Masonry block on top of a complete levee.
        var sim = new ScenarioBuilder().Ground(GY - 1).Agent(new Int3(5, GY, 5)).Build();
        var b = sim.Buildings.PlacePrebuilt(levee, at, 0);
        sim.World.SetBlock(at + Int3.Up, BlockId.Masonry);
        sim.Enqueue(new Deconstruct(b.Id));
        sim.Tick();
        Assert.Equal(new[] { "SupportsBlocks" }, Rejections(sim, "Deconstruct"));
        Assert.Equal(BuildingState.Complete, b.State);

        // BuildingOnTop is checked first: a second levee on top, and a block hanging off the side of the lower one.
        var stacked = new ScenarioBuilder().Ground(GY - 1).Agent(new Int3(5, GY, 5)).Build();
        var lower = stacked.Buildings.PlacePrebuilt(levee, at, 0);
        stacked.Buildings.PlacePrebuilt(levee, at + Int3.Up, 0);
        var side = at + new Int3(-1, 0, 0);
        stacked.World.SetBlock(side + Int3.Down, BlockId.Air);
        stacked.World.SetBlock(side, BlockId.Masonry);   // rests only on the lower levee
        Assert.True(Construction.SupportsBlocks(stacked, lower));
        stacked.Enqueue(new Deconstruct(lower.Id));
        stacked.Tick();
        Assert.Equal(new[] { "BuildingOnTop" }, Rejections(stacked, "Deconstruct"));

        // A block resting on its own ground does not stop the deconstruction.
        var free = new ScenarioBuilder().Ground(GY - 1).Agent(new Int3(5, GY, 5)).Build();
        var alone = free.Buildings.PlacePrebuilt(levee, at, 0);
        free.World.SetBlock(at + new Int3(-1, 0, 0), BlockId.Masonry);
        free.Enqueue(new Deconstruct(alone.Id));
        free.Tick();
        Assert.Empty(Rejections(free, "Deconstruct"));
        Assert.Equal(BuildingState.Deconstructing, alone.State);
    }

    /// <summary>ADR-063: a block placed against a levee after its Deconstruct command was accepted holds the teardown
    /// back (the worker stands down, no failure); once that block is gone the levee comes down.</summary>
    [Fact]
    public void DeconstructingLevee_WaitsForBlocksLeaningOnIt()
    {
        var at = new Int3(15, GY, 15);
        var side = at + new Int3(-1, 0, 0);
        var sim = new ScenarioBuilder().Ground(GY - 1).Hub(HubOrigin).Agent(new Int3(12, GY, 12)).Build();
        var b = sim.Buildings.PlacePrebuilt(TestContent.Db.Building("levee"), at, 0);
        sim.Enqueue(new Deconstruct(b.Id));
        sim.Tick();
        Assert.Equal(BuildingState.Deconstructing, b.State);
        sim.World.SetBlock(side + Int3.Down, BlockId.Air);
        sim.World.SetBlock(side, BlockId.Masonry);   // rests only on the levee

        for (int t = 0; t < 600; t++)
        {
            sim.Tick();
            Assert.Empty(Grounding.Floating(sim));
        }
        Assert.NotNull(sim.Buildings.Get(b.Id));
        Assert.Equal(0, sim.Counters.JobsFailed);

        sim.Enqueue(new DesignateDeconstructBlocks(side, side));
        RunUntil(sim, () => sim.Buildings.Get(b.Id) is null, 3000, () => Assert.Empty(Grounding.Floating(sim)));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(side));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }
}
