using System.Numerics;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Screenshots;
using Aurvangar.ViewCore.Scripts;
using Aurvangar.ViewCore.Tools;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M9-T1: the block tool places single blocks (G4 answer "building should be one square at a time"; VIEW-21,
/// ADR-067). Worlds are stone to y = 8, so the ground's top faces are picked at y = 8 and the first course is y = 9.</summary>
[Trait("Category", "Unit")]
public class BlockPaintTests
{
    private const int G = 9;
    private static readonly Int3 HubOrigin = new(20, G, 20);

    private static Simulation World(int stone = 0, int log = 0)
    {
        var b = new ScenarioBuilder().Ground(G - 1).Hub(HubOrigin);
        if (stone > 0) b.Stock("stone", stone);
        if (log > 0) b.Stock("log", log);
        return b.Agent(new Int3(12, G, 17)).Agent(new Int3(13, G, 17)).Build();
    }

    private static PickHit Top(int x, int z) => new(new Int3(x, G - 1, z), Int3.Up);

    private static DesignateBuild Single(Int3 c, BlockId block, bool plan) => new(BuildShape.Single, c, c, 1, block, plan);

    [Fact]
    public void Click_PlacesOneBlock_TopStacksUp_SideBeside()
    {
        var sim = World();
        var tool = new BlockTool(TestContent.Db);
        Assert.Equal(new[] { BlockId.Masonry, BlockId.Planks, BlockId.PolishedStone, BlockId.Rubble, BlockId.Beam, BlockId.Slate }, tool.Blocks);

        // A click on the ground's top face: one block in the cell above it. The tool stays active (no drag left).
        tool.Press(Top(5, 5));
        var click = tool.Release(sim);
        Assert.Equal(new[] { Single(new Int3(5, G, 5), BlockId.Masonry, false) }, click.Commands);
        Assert.Null(click.Message);
        Assert.False(tool.Dragging);

        // On a built block: the top face stacks upward, a side face places beside.
        sim.World.SetBlock(new Int3(5, G, 5), BlockId.Masonry);
        Assert.Equal(new Int3(5, G + 1, 5), BlockTool.Anchor(new PickHit(new Int3(5, G, 5), Int3.Up)));
        tool.Press(new PickHit(new Int3(5, G, 5), Int3.Up));
        Assert.Equal(new[] { Single(new Int3(5, G + 1, 5), BlockId.Masonry, false) }, tool.Release(sim).Commands);
        tool.Press(new PickHit(new Int3(5, G, 5), Int3.East));
        Assert.Equal(new[] { Single(new Int3(6, G, 5), BlockId.Masonry, false) }, tool.Release(sim).Commands);
        tool.Press(new PickHit(new Int3(5, G, 5), Int3.West));
        Assert.Equal(new[] { Single(new Int3(4, G, 5), BlockId.Masonry, false) }, tool.Release(sim).Commands);

        // Without a drag the ghost is the one cell a click would place; over nothing there is no ghost.
        var hover = tool.Ghost(sim, Top(12, 5))!;
        Assert.Equal(new[] { new Int3(12, G, 5) }, hover.Cells.Select(c => c.Cell));
        Assert.True(hover.Cells[0].Ok);
        Assert.Null(tool.Ghost(sim, null));
        Assert.Contains("1 block", BlockTool.Tooltip(sim, hover));
        Assert.Contains(BlockTool.ControlsHint, BlockTool.Tooltip(sim, hover));
        Assert.DoesNotContain("Tab", BlockTool.Tooltip(sim, hover));

        // A release with no press sends nothing.
        Assert.Empty(tool.Release(sim).Commands);
    }

    [Fact]
    public void Line4_IsFaceConnected()
    {
        var a = new Int3(0, 3, 0);
        Assert.Equal(new[] { new Int3(1, 3, 0), new Int3(1, 3, 1), new Int3(2, 3, 1) }, PaintDrag.Line4(a, new Int3(2, 3, 1)));
        Assert.Empty(PaintDrag.Line4(a, a));
        foreach (var b in new[] { new Int3(5, 3, 0), new Int3(-4, 3, 7), new Int3(3, 3, -3), new Int3(-6, 3, -2), new Int3(0, 3, -5) })
        {
            var path = PaintDrag.Line4(a, b).ToList();
            Assert.Equal(Math.Abs(b.X) + Math.Abs(b.Z), path.Count);
            Assert.Equal(b, path[^1]);
            var prev = a;
            foreach (var c in path)
            {
                Assert.Equal(1, Math.Abs(c.X - prev.X) + Math.Abs(c.Z - prev.Z));   // one face step each time
                Assert.Equal(a.Y, c.Y);
                prev = c;
            }
            Assert.Equal(path.Count, path.Distinct().Count());
        }
    }

    [Fact]
    public void Drag_PaintsEachNewCellOnce_OnTheFirstLayer()
    {
        var sim = World();
        var tool = new BlockTool(TestContent.Db);
        tool.Select(BlockId.Planks);
        tool.TogglePlan();

        tool.Press(Top(5, 5));
        tool.Move(Top(9, 5));
        Assert.Equal(Enumerable.Range(5, 5).Select(x => new Int3(x, G, 5)), tool.Painted);
        tool.Move(Top(7, 5));                                         // back over painted cells: nothing new
        Assert.Equal(5, tool.Painted.Count);
        tool.Move(new PickHit(new Int3(7, G + 2, 7), Int3.Up));      // a pick on a higher block: still the first layer
        Assert.Equal(new[] { new Int3(7, G, 6), new Int3(7, G, 7) }, tool.Painted.Skip(5));
        tool.Move(null);                                              // over nothing: kept
        Assert.Equal(7, tool.Painted.Count);

        // The mouse ray is cut with the picked face's plane (the ground's top face, y = 9).
        var origin = new Vector3(7.5f, 30f, 20.5f);
        tool.MoveRay(origin, new Vector3(9.5f, G, 12.5f) - origin, null);
        Assert.Equal(new Int3(9, G, 12), tool.Painted[^1]);
        Assert.Equal(7 + 2 + 5, tool.Painted.Count);                  // (7,7) -> (9,12): 2 + 5 face steps
        tool.MoveRay(origin, new Vector3(1, 0, 0), Top(9, 13));       // parallel to the plane: the pick is used
        Assert.Equal(new Int3(9, G, 13), tool.Painted[^1]);
        tool.DragTo(new Int3(9 + PaintDrag.MaxStep + 5, G, 13));      // a jump off toward the horizon is ignored
        Assert.Equal(new Int3(9, G, 13), tool.Painted[^1]);

        var painted = tool.Painted.ToList();
        var click = tool.Release(sim);
        Assert.False(tool.Dragging);
        Assert.Equal(painted.Count, click.Commands.Count);
        Assert.Equal(painted.OrderBy(c => c.X).ThenBy(c => c.Z), click.Commands.Select(c => c.A).OrderBy(c => c.X).ThenBy(c => c.Z));
        Assert.All(click.Commands, c => Assert.Equal(Single(c.A, BlockId.Planks, true), c));

        sim.Events.Drain();
        foreach (var c in click.Commands) sim.Enqueue(c);
        sim.Tick();
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        Assert.Equal(painted.Count, sim.Plans.Count);
        Assert.All(painted, c => Assert.Equal(new PlanEntry(BlockId.Planks, PlanState.Planned), sim.Plans.Get(c)));
    }

    [Fact]
    public void PaintPlane_FollowsThePickedFace()
    {
        Assert.Equal(G, PaintDrag.PlaneOf(Top(5, 5)));                                          // top face of the ground
        Assert.Equal(G + 0.5f, PaintDrag.PlaneOf(new PickHit(new Int3(5, G, 5), Int3.West)));  // side: mid-layer
        Assert.Equal(12f, PaintDrag.PlaneOf(new PickHit(new Int3(5, 12, 5), Int3.Down)));      // bottom face

        var side = new PaintDrag(new PickHit(new Int3(5, G, 5), Int3.West), new Int3(4, G, 5));
        Assert.Equal(G, side.LayerY);
        var origin = new Vector3(0.5f, 20f, 5.5f);
        Assert.Equal(new Int3(2, G, 5), side.OnPlane(origin, new Vector3(2.5f, G + 0.5f, 5.5f) - origin));
        Assert.Null(side.OnPlane(origin, new Vector3(0, 1, 0)));   // pointing away from the plane
        Assert.Null(side.OnPlane(origin, new Vector3(1, 0, 0)));   // parallel
    }

    [Fact]
    public void Release_SkipsInvalidCells_AndOrdersBySupport()
    {
        var sim = World();
        sim.World.SetBlock(new Int3(7, G, 5), BlockId.Stone);
        var tool = new BlockTool(TestContent.Db);

        // A course across a stone block: that cell is red with its reason; the others are sent.
        tool.Press(Top(5, 5));
        tool.Move(Top(9, 5));
        var ghost = tool.Ghost(sim, null)!;
        Assert.Equal(PlanResult.Solid, ghost.Cells.Single(c => c.Cell == new Int3(7, G, 5)).Result);
        Assert.Equal(4, ghost.ValidCount);
        Assert.Contains(BlockTool.ReasonText(PlanResult.Solid)!, BlockTool.Tooltip(sim, ghost));
        Assert.All(ghost.Cells, c => Assert.Equal(BlockPlans.CanPlan(sim, c.Cell, ghost.Cells.Select(g => g.Cell).ToList()), c.Result));
        var click = tool.Release(sim);
        Assert.Equal(new[] { 5, 6, 8, 9 }, click.Commands.Select(c => c.A.X).OrderBy(x => x));

        // Nothing valid: no command, and the reason.
        tool.Press(new PickHit(new Int3(7, G - 1, 5), Int3.Up));   // (7,9,5) is the stone block
        var none = tool.Release(sim);
        Assert.Empty(none.Commands);
        Assert.Contains(BlockTool.ReasonText(PlanResult.Solid)!, none.Message);

        // An overhang painted from its free end: valid as a set (each hangs on the next), and sent from the pillar out.
        for (int y = G; y <= G + 3; y++) sim.World.SetBlock(new Int3(3, y, 3), BlockId.Stone);
        var outward = new[] { new Int3(6, G + 3, 3), new Int3(5, G + 3, 3), new Int3(4, G + 3, 3) };
        Assert.All(BlockTool.GhostFor(sim, BlockId.Masonry, false, outward).Cells, c => Assert.True(c.Ok));
        var order = BlockTool.SupportOrder(sim, outward);
        Assert.Equal(outward.AsEnumerable().Reverse(), order);
        Assert.Equal(new[] { new Int3(9, G + 5, 9) }, BlockTool.SupportOrder(sim, new[] { new Int3(9, G + 5, 9) }));   // floating: last

        // In paint order the sim would refuse the free end; in support order every block is planned.
        var naive = World();
        for (int y = G; y <= G + 3; y++) naive.World.SetBlock(new Int3(3, y, 3), BlockId.Stone);
        foreach (var c in outward) naive.Enqueue(Single(c, BlockId.Masonry, true));
        naive.Tick();
        Assert.NotEmpty(naive.Events.Drain().OfType<CommandRejected>());

        sim.Events.Drain();
        foreach (var c in order) sim.Enqueue(Single(c, BlockId.Masonry, true));
        sim.Tick();
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        Assert.All(outward, c => Assert.NotNull(sim.Plans.Get(c)));
    }

    [Fact]
    public void Deconstruct_ByBlock_ClickOrDrag()
    {
        var sim = World();
        for (int x = 4; x <= 6; x++) sim.World.SetBlock(new Int3(x, G, 12), BlockId.Masonry);
        sim.World.SetBlock(new Int3(5, G + 1, 12), BlockId.Planks);
        var d = new DeconstructPaint();

        // Hover marks the one built block under the mouse; natural ground is never marked.
        Assert.Equal(new[] { new Int3(4, G, 12) }, d.Marked(sim, new PickHit(new Int3(4, G, 12), Int3.Up)));
        Assert.Empty(d.Marked(sim, Top(3, 12)));
        Assert.Null(DeconstructPaint.Tooltip(Array.Empty<Int3>(), false));
        Assert.Contains("this block", DeconstructPaint.Tooltip(new[] { new Int3(4, G, 12) }, false));

        // A drag on the first block's layer marks each built block it passes, and nothing above or below it.
        d.Start(new PickHit(new Int3(4, G, 12), Int3.Up));
        d.Move(Top(8, 12));                                    // a ground pick: kept on the blocks' layer
        Assert.Equal(new[] { new Int3(4, G, 12), new Int3(5, G, 12), new Int3(6, G, 12) }, d.Marked(sim, null));
        Assert.Contains("3 blocks", DeconstructPaint.Tooltip(d.Marked(sim, null), true));
        var (commands, message) = d.Release(sim);
        Assert.Null(message);
        Assert.False(d.Dragging);
        Assert.Equal(new[] { 4, 5, 6 }.Select(x => new DesignateDeconstructBlocks(new Int3(x, G, 12), new Int3(x, G, 12))), commands);

        foreach (var c in commands) sim.Enqueue(c);
        sim.Tick();
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        Assert.All(new[] { 4, 5, 6 }, x => Assert.NotEqual(DesignationMark.None, sim.Designations.Get(new Int3(x, G, 12))));
        Assert.Equal(DesignationMark.None, sim.Designations.Get(new Int3(5, G + 1, 12)));
        Assert.Equal(DesignationMark.None, sim.Designations.Get(new Int3(4, G - 1, 12)));

        // A drag over no built block sends nothing and says so.
        d.Start(Top(3, 4));
        d.Move(Top(6, 4));
        var empty = d.Release(sim);
        Assert.Empty(empty.Commands);
        Assert.Equal("No built blocks here", empty.Message);
    }

    [Fact]
    public void PaintScript_Seed1_PaintsSingleBlocks_PresetAndLiveDrag()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var site = BlocksScript.Find(sim);
        Assert.NotNull(site);
        Assert.Contains("paint", ScreenshotScripts.Names);
        Assert.True(ScreenshotScripts.IsTimed("paint"));
        ScreenshotScripts.Run("paint", sim, 2);
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        Assert.Equal(PaintScript.Course1Blocks, sim.Plans.All.Count(p => p.Entry.State == PlanState.Released && p.Entry.Block == BlockId.Planks));
        Assert.Equal(PaintScript.Course2Blocks, sim.Plans.All.Count(p => p.Entry.State == PlanState.Planned && p.Entry.Block == BlockId.Planks));
        Assert.Equal(site, PaintScript.Site(sim));

        var live = PaintScript.LiveDrag(sim);
        Assert.NotNull(live);
        var tool = new BlockTool(TestContent.Db);
        tool.Press(live!.Value.Start);
        foreach (var c in live.Value.Path) tool.DragTo(c);
        Assert.Equal(1 + 7 + 3, tool.Painted.Count);   // a face-connected staircase from (11,1) to (4,4)

        var shot = ScreenshotPresets.For("paint", sim);
        Assert.InRange(shot.Focus.X, site!.Value.X, site.Value.X + BlocksScript.SiteW);
    }
}
