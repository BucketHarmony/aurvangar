using Aurvangar.Sim.Blocks;
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

/// <summary>M8-T4: the plan layer (construction.md CON-04, CON-05 Planned, CON-06, CON-07 ReleasePlan, scenario 8).
/// Worlds are stone to y = 8, so dwarves walk on y = 9; the hub is at (20,9,20). Plans are one course on the ground
/// along z = 12, so every entry is supported and reachable.</summary>
[Trait("Category", "Scenario")]
public class BlockPlanScenarioTests
{
    private const int G = 9;
    private static readonly Int3 HubOrigin = new(20, G, 20);

    private static Simulation World(int stone, int log = 0)
    {
        var b = new ScenarioBuilder().Ground(G - 1).Hub(HubOrigin).Stock("stone", stone);
        if (log > 0) b.Stock("log", log);
        return b.Agent(new Int3(12, G, 17)).Agent(new Int3(13, G, 17)).Build();
    }

    private static void Line(Simulation sim, int x0, int x1, BlockId block, bool plan)
    {
        sim.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(x0, G, 12), new Int3(x1, G, 12), 1, block, plan));
        sim.Tick();
    }

    private static List<Int3> Cells(int x0, int x1) =>
        Enumerable.Range(x0, x1 - x0 + 1).Select(x => new Int3(x, G, 12)).ToList();

    private static List<Job> Builds(Simulation sim) => sim.Jobs.All.Where(j => j.Kind == JobKind.Build).ToList();

    private static List<CommandRejected> Rejections(Simulation sim, string tag) =>
        sim.Events.Drain().OfType<CommandRejected>().Where(r => r.Command == tag).ToList();

    private static void Release(Simulation sim, Int3 a, Int3 b)
    {
        sim.Enqueue(new ReleasePlan(a, b));
        sim.Tick();
    }

    private static Int3 WorldMax(Simulation sim) => new(sim.World.SizeX - 1, sim.World.SizeY - 1, sim.World.SizeZ - 1);

    private static int NeededOf(IReadOnlyList<(ItemId Item, int Count)> needed, string item) =>
        needed.Where(n => n.Item == TestContent.Db.Item(item)).Sum(n => n.Count);

    [Fact]
    public void PlannedEntries_NotBuilt()
    {
        var sim = World(30);
        Line(sim, 10, 15, BlockId.Masonry, plan: true);
        Assert.Equal(6, sim.Plans.Count);
        for (int t = 0; t < 2000; t++)
        {
            sim.Tick();
            Assert.Empty(Builds(sim));
        }
        Assert.All(Cells(10, 15), c =>
        {
            Assert.Equal(BlockId.Air, sim.World.GetBlock(c));
            Assert.Equal(new PlanEntry(BlockId.Masonry, PlanState.Planned), sim.Plans.Get(c));
            Assert.Equal(BuildStatus.Planned, sim.Plans.StatusOf(sim, c));
        });
        Assert.Equal(30, Stored(Hub(sim), "stone"));
    }

    [Fact]
    public void ReleasePlan_Box_ReleasesOnlyInside()
    {
        var sim = World(30);
        Line(sim, 10, 15, BlockId.Masonry, plan: true);
        sim.Events.Drain();

        Release(sim, new Int3(10, G, 12), new Int3(12, G + 3, 12));
        Assert.Empty(Rejections(sim, "ReleasePlan"));
        Assert.All(Cells(10, 12), c => Assert.Equal(PlanState.Released, sim.Plans.Get(c)!.Value.State));
        Assert.All(Cells(13, 15), c => Assert.Equal(PlanState.Planned, sim.Plans.Get(c)!.Value.State));

        RunUntil(sim, () => Cells(10, 12).All(c => sim.World.GetBlock(c) == BlockId.Masonry), 3000);
        for (int t = 0; t < 500; t++) sim.Tick();
        Assert.All(Cells(13, 15), c =>
        {
            Assert.Equal(BlockId.Air, sim.World.GetBlock(c));
            Assert.Equal(BuildStatus.Planned, sim.Plans.StatusOf(sim, c));
        });
        Assert.Empty(Builds(sim));
        Assert.Equal(3, sim.Plans.Count);

        // A box with no Planned entry: one over the built half, and one far away.
        sim.Events.Drain();
        Release(sim, new Int3(10, G, 12), new Int3(12, G, 12));
        Assert.Contains(Rejections(sim, "ReleasePlan"), r => r.Reason == "NothingToRelease");
        Release(sim, new Int3(0, G, 0), new Int3(3, G, 3));
        Assert.Contains(Rejections(sim, "ReleasePlan"), r => r.Reason == "NothingToRelease");
        Assert.All(Cells(13, 15), c => Assert.Equal(PlanState.Planned, sim.Plans.Get(c)!.Value.State));

        // A world-sized box (corners given in either order) releases the rest.
        Release(sim, WorldMax(sim), new Int3(0, 0, 0));
        Assert.Empty(Rejections(sim, "ReleasePlan"));
        RunUntil(sim, () => sim.Plans.Count == 0, 3000);
        Assert.All(Cells(10, 15), c => Assert.Equal(BlockId.Masonry, sim.World.GetBlock(c)));
    }

    [Fact]
    public void MaterialTotals_NeededVersusStored()
    {
        var sim = World(30);
        Assert.Empty(sim.Plans.Needed(null));
        Line(sim, 8, 11, BlockId.Masonry, plan: true);          // 4 x 1 stone
        Line(sim, 12, 13, BlockId.PolishedStone, plan: true);   // 2 x 2 stone
        Line(sim, 14, 15, BlockId.Planks, plan: true);          // 2 x 1 log (none stored)
        Assert.Equal(8, sim.Plans.Count);

        var planned = sim.Plans.Needed(PlanState.Planned);
        Assert.Equal(8, NeededOf(planned, "stone"));
        Assert.Equal(2, NeededOf(planned, "log"));
        Assert.Equal(2, planned.Count);
        Assert.True(planned[0].Item.Value < planned[1].Item.Value, "Needed is not in ascending item id order");
        Assert.Empty(sim.Plans.Needed(PlanState.Released));
        Assert.Equal(planned, sim.Plans.Needed(null));

        Release(sim, new Int3(8, G, 12), new Int3(13, G, 12));
        Assert.Equal(new[] { (TestContent.Db.Item("stone"), 8) }, sim.Plans.Needed(PlanState.Released));
        Assert.Equal(new[] { (TestContent.Db.Item("log"), 2) }, sim.Plans.Needed(PlanState.Planned));
        Assert.Equal(planned, sim.Plans.Needed(null));

        int lastNeeded = 8;
        RunUntil(sim, () => sim.Plans.Count == 2, 4000, () =>
        {
            int masonry = Cells(8, 11).Count(c => sim.World.GetBlock(c) == BlockId.Masonry);
            int polished = Cells(12, 13).Count(c => sim.World.GetBlock(c) == BlockId.PolishedStone);
            int needed = NeededOf(sim.Plans.Needed(PlanState.Released), "stone");
            Assert.Equal(8, needed + masonry + 2 * polished);
            Assert.True(needed <= lastNeeded);
            lastNeeded = needed;
            Assert.Equal(needed, NeededOf(sim.Plans.Needed(null), "stone"));
            Assert.Equal(2, NeededOf(sim.Plans.Needed(null), "log"));
        });
        Assert.Empty(sim.Plans.Needed(PlanState.Released));
        Assert.Equal(new[] { (TestContent.Db.Item("log"), 2) }, sim.Plans.Needed(null));
    }

    [Fact]
    public void PlannedState_SavedAndHashed()
    {
        var a = World(30);
        var b = World(30);
        foreach (var c in Cells(10, 15))
        {
            a.Plans.Set(c, new PlanEntry(BlockId.Masonry, PlanState.Planned));
            b.Plans.Set(c, new PlanEntry(BlockId.Masonry, PlanState.Released));
        }
        Assert.NotEqual(a.StateHash(), b.StateHash());

        var sim = World(30);
        Line(sim, 10, 15, BlockId.Masonry, plan: true);
        Line(sim, 10, 11, BlockId.PolishedStone, plan: false);   // a Released part, repainted
        for (int t = 0; t < 200; t++) sim.Tick();

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.Equal(sim.Plans.All.ToList(), loaded.Plans.All.ToList());
        Assert.Contains(loaded.Plans.All, p => p.Entry.State == PlanState.Planned);

        sim.Enqueue(new ReleasePlan(new Int3(0, 0, 0), WorldMax(sim)));
        loaded.Enqueue(new ReleasePlan(new Int3(0, 0, 0), WorldMax(loaded)));
        for (int t = 1; t <= 1000; t++)
        {
            sim.Tick();
            loaded.Tick();
            if (t % 100 == 0) Assert.True(sim.StateHash() == loaded.StateHash(), $"hash differs {t} ticks after load");
        }
        Assert.Equal(0, loaded.Plans.Count);

        // The ReleasePlan command survives the command log (CommandCodec).
        using var ms2 = new MemoryStream();
        SaveGame.Save(loaded, ms2);
        ms2.Position = 0;
        Assert.Equal(loaded.StateHash(), SaveGame.Load(ms2, TestContent.Db).StateHash());
    }
}
