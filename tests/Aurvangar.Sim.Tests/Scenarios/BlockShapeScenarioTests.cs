using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M11-T10: dwarves build and take down fine block shapes (construction.md CON-19..22, ADR-080). Worlds are
/// stone to y = 8, so dwarves walk on y = 9; the hub is at (20,9,20) with its entrance at (21,9,19).</summary>
[Trait("Category", "Scenario")]
public class BlockShapeScenarioTests
{
    private const int G = 9;
    private static readonly Int3 HubOrigin = new(20, G, 20);
    private static readonly BlockForm Slab = new(BlockShape.Slab, 0);
    private static readonly BlockForm StairEast = new(BlockShape.Stair, 1);
    private static readonly BlockForm Pillar = new(BlockShape.Pillar, 0);

    private static List<Job> Builds(Simulation sim) => sim.Jobs.All.Where(j => j.Kind == JobKind.Build).ToList();

    /// <summary>5 Slate slabs at z = 12 (x 10..14), 3 Slate stairs at z = 8 (x 10..12, rotation 1), 2 Masonry pillars
    /// at z = 15 (x 10 and 12). Cut stone (M11-T4: Slate costs cut stone): 5 x 2 + 3 x 3 = 19; stone: 2 x 1 = 2.</summary>
    private static Simulation ShapeWorld(int stone, out Dictionary<Int3, (BlockId Block, BlockForm Form)> want)
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HubOrigin).Stock("stone", stone).Stock("cutstone", stone)
            .Agent(new Int3(12, G, 17)).Agent(new Int3(13, G, 17)).Build();
        var cells = new Dictionary<Int3, (BlockId Block, BlockForm Form)>();
        void Paint(Int3 a, Int3 b, BlockId block, BlockForm form)
        {
            sim.Enqueue(new DesignateBuild(BuildShape.Line, a, b, 1, block, false, form));
            foreach (var c in BuildShapes.Cells(BuildShape.Line, a, b, 1)) cells[c] = (block, form);
        }
        Paint(new Int3(10, G, 12), new Int3(14, G, 12), BlockId.Slate, Slab);
        Paint(new Int3(10, G, 8), new Int3(12, G, 8), BlockId.Slate, StairEast);
        Paint(new Int3(10, G, 15), new Int3(10, G, 15), BlockId.Masonry, Pillar);
        Paint(new Int3(12, G, 15), new Int3(12, G, 15), BlockId.Masonry, Pillar);
        sim.Tick();
        Assert.Equal(10, sim.Plans.Count);
        want = cells;
        return sim;
    }

    [Fact]
    public void Shapes_BuiltWithTheirFormAndShapedCost_ThenRefunded()
    {
        var sim = ShapeWorld(30, out var want);
        Assert.Equal(new[] { (Stone, 2), (Cut, 19) }, sim.Plans.Needed(null));
        var shapesSeen = new HashSet<BlockShape>();
        RunUntil(sim, () => sim.Plans.Count == 0, 6000, () =>
        {
            foreach (var j in Builds(sim))
            {
                // A job places one block and one shape: its pickup is cells x the shaped cost.
                var places = j.Steps.Where(s => s.Kind == StepKind.Place).ToList();
                var shape = places[0].PlacedForm.Shape;
                shapesSeen.Add(shape);
                Assert.All(places, p => Assert.Equal(shape, p.PlacedForm.Shape));
                Assert.All(places, p => Assert.Equal(want[p.Cell].Form, p.PlacedForm));
                int cost = sim.Content.CostOf(places[0].PlacedBlock, shape).Count;
                Assert.Equal(places.Count * cost, j.Steps.Single(s => s.Kind == StepKind.PickUpFromStorage).Count);
                Assert.True(places.Count * cost <= Agents.Agent.CarryCapacity);
            }
        });
        Assert.Equal(new[] { BlockShape.Slab, BlockShape.Stair, BlockShape.Pillar }.OrderBy(s => s), shapesSeen.OrderBy(s => s));
        foreach (var (c, (block, form)) in want)
        {
            Assert.Equal(block, sim.World.GetBlock(c));
            Assert.Equal(form, sim.World.FormAt(c));
        }
        Assert.Equal(10, sim.World.FormCount);
        RunUntil(sim, () => Loose(sim) == 0, 2000);
        Assert.Equal(30 - 2, Stored(Hub(sim), "stone"));
        Assert.Equal(30 - 19, Stored(Hub(sim), "cutstone"));
        Assert.Equal(0, sim.Counters.JobsFailed);

        // CON-17 with CON-20: digging a shaped block refunds its shaped cost, and the cell is Full air again.
        sim.Enqueue(new DesignateDeconstructBlocks(new Int3(8, G, 6), new Int3(16, G, 16)));
        RunUntil(sim, () => want.Keys.All(c => sim.World.GetBlock(c) == BlockId.Air)
            && sim.Piles.Count == 0 && Loose(sim) == 0, 6000);
        Assert.Equal(0, sim.World.FormCount);
        Assert.Equal(30, Stored(Hub(sim), "stone"));
        Assert.Equal(30, Stored(Hub(sim), "cutstone"));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    private static Core.ItemId Stone => TestContent.Db.Item("stone");

    private static Core.ItemId Cut => TestContent.Db.Item("cutstone");

    /// <summary>Stone and cut stone carried or in piles.</summary>
    private static int Loose(Simulation sim) =>
        CarriedTotal(sim, "stone") + PileTotal(sim, "stone") + CarriedTotal(sim, "cutstone") + PileTotal(sim, "cutstone");

    /// <summary>CON-21 (ADR-080): every shape is one solid cell. A slab is a floor to stand on, holds up a pile (GRV-02)
    /// and a block on it (CON-09).</summary>
    [Fact]
    public void EveryShape_IsOneSolidCell()
    {
        var slab = new Int3(8, G, 8);
        var pillar = new Int3(12, G, 8);
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(slab, slab, BlockId.Masonry).FillBox(pillar, pillar, BlockId.Masonry)
            .Hub(HubOrigin).Agent(new Int3(12, G, 14)).Build();
        sim.World.SetForm(slab, Slab);
        sim.World.SetForm(pillar, Pillar);
        sim.Piles.Add(slab + Int3.Up, Stone, 3);
        sim.Piles.Add(pillar + Int3.Up, Stone, 2);
        sim.RunTicks(3);
        Assert.Equal(3, sim.Piles.At(slab + Int3.Up).Count);   // rests on the slab, does not fall
        Assert.Equal(2, sim.Piles.At(pillar + Int3.Up).Count);
        Assert.Empty(Grounding.FloatingPiles(sim));
        Assert.True(sim.PathGrid.IsWalkable(slab + Int3.Up));
        Assert.False(sim.PathGrid.IsWalkable(slab));
        Assert.True(Aurvangar.Sim.Blocks.Support.Placement(sim.World, pillar + Int3.Up));   // a block on a pillar is supported
        Assert.True(Aurvangar.Sim.Blocks.Support.Placement(sim.World, slab + Int3.Up));
    }

    /// <summary>CON-07 repaint (CON-19): changing only the form of an entry held by a Build job cancels that job; the
    /// cells are built in the new form.</summary>
    [Fact]
    public void RepaintToAnotherForm_CancelsTheHolder()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HubOrigin).Stock("stone", 30)
            .Agent(new Int3(12, G, 17)).Build();
        var a = new Int3(10, G, 12);
        var b = new Int3(13, G, 12);
        sim.Enqueue(new DesignateBuild(BuildShape.Line, a, b, 1, BlockId.Masonry, false));
        RunUntil(sim, () => Builds(sim).Count > 0, 100);
        var first = Builds(sim).Single();
        Assert.All(first.Steps.Where(s => s.Kind == StepKind.Place), s => Assert.Equal(BlockForm.Full, s.PlacedForm));

        sim.Enqueue(new DesignateBuild(BuildShape.Line, a, b, 1, BlockId.Masonry, false, StairEast));
        sim.Tick();
        Assert.Null(sim.Jobs.Get(first.Id));
        RunUntil(sim, () => sim.Plans.Count == 0 && CarriedTotal(sim, "stone") == 0 && PileTotal(sim, "stone") == 0, 4000);
        foreach (var c in BuildShapes.Cells(BuildShape.Line, a, b, 1))
            Assert.Equal(StairEast, sim.World.FormAt(c));
        Assert.Equal(30 - 4, Stored(Hub(sim), "stone"));
    }

    [Fact]
    public void SaveLoad_MidShapedBuild_ContinuesIdentically()
    {
        var sim = ShapeWorld(30, out var want);
        RunUntil(sim, () => sim.World.FormCount >= 2 && Builds(sim).Any(j => j.IsClaimed)
            && CarriedTotal(sim, "cutstone") > 0, 3000);

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.Equal(sim.World.Forms.ToList(), loaded.World.Forms.ToList());
        for (int t = 1; t <= 2000; t++)
        {
            sim.Tick();
            loaded.Tick();
            if (t % 100 == 0) Assert.True(sim.StateHash() == loaded.StateHash(), $"hash differs {t} ticks after load");
        }
        foreach (var (c, (_, form)) in want) Assert.Equal(form, loaded.World.FormAt(c));
    }
}
