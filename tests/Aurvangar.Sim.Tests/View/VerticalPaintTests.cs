using System.Numerics;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Screenshots;
using Aurvangar.ViewCore.Scripts;
using Aurvangar.ViewCore.Tools;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M10-T3: a block-tool drag that starts on a side face paints in that face's vertical plane (G5 Q2; VIEW-21,
/// ADR-073, which amends ADR-067). Worlds are stone to y = 8, so the first course is y = 9.</summary>
[Trait("Category", "Unit")]
public class VerticalPaintTests
{
    private const int G = 9;
    private static readonly Int3 HubOrigin = new(20, G, 20);

    private static Simulation World(int stone = 0)
    {
        var b = new ScenarioBuilder().Ground(G - 1).Hub(HubOrigin);
        if (stone > 0) b.Stock("stone", stone);
        return b.Agent(new Int3(12, G, 17)).Agent(new Int3(13, G, 17)).Build();
    }

    private static PickHit Top(int x, int z) => new(new Int3(x, G - 1, z), Int3.Up);

    /// <summary>A stone post at x = 4 from the ground up to <c>G + 3</c> with a cap at (5, G + 3, 5): the cap's east face
    /// is a side face with nothing under the cell it looks into.</summary>
    private static Simulation Capped(int stone = 0)
    {
        var sim = World(stone);
        for (int y = G; y <= G + 3; y++) sim.World.SetBlock(new Int3(4, y, 5), BlockId.Stone);
        sim.World.SetBlock(new Int3(5, G + 3, 5), BlockId.Stone);
        return sim;
    }

    [Fact]
    public void SideFaceDrag_PaintsTheFacesVerticalPlane_TopFaceStaysHorizontal()
    {
        var sim = World();
        sim.World.SetBlock(new Int3(5, G, 5), BlockId.Masonry);
        var tool = new BlockTool(TestContent.Db);

        // East face: the plane x = 6. A column up, then across (the cursor's X is ignored), then down: a 2-wide wall.
        tool.Press(new PickHit(new Int3(5, G, 5), Int3.East));
        Assert.True(tool.Vertical);
        tool.DragTo(new Int3(6, G + 3, 5));
        Assert.Equal(Enumerable.Range(G, 4).Select(y => new Int3(6, y, 5)), tool.Painted);
        tool.DragTo(new Int3(40, G + 3, 6));
        tool.DragTo(new Int3(6, G, 6));
        Assert.Equal(8, tool.Painted.Count);
        Assert.All(tool.Painted, c => Assert.Equal(6, c.X));
        Assert.Equal(8, tool.Painted.Distinct().Count());
        Assert.All(tool.Ghost(sim, null)!.Cells, c => Assert.True(c.Ok));
        var click = tool.Release(sim);
        Assert.Equal(8, click.Commands.Count);
        Assert.False(tool.Vertical);
        sim.Events.Drain();
        foreach (var c in click.Commands) sim.Enqueue(c);
        sim.Tick();
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        Assert.Equal(8, sim.Plans.Count);

        // North face: the plane z = 4; each step is one face step in the XY plane.
        tool.Press(new PickHit(new Int3(5, G, 5), Int3.North));
        tool.DragTo(new Int3(9, G + 2, 30));
        var path = tool.Painted.ToList();
        Assert.Equal(1 + 4 + 2, path.Count);
        Assert.All(path, c => Assert.Equal(4, c.Z));
        Assert.Equal(new Int3(9, G + 2, 4), path[^1]);
        for (int k = 1; k < path.Count; k++)
            Assert.Equal(1, Math.Abs(path[k].X - path[k - 1].X) + Math.Abs(path[k].Y - path[k - 1].Y));
        tool.AbortDrag();

        // A top face still paints its layer.
        tool.Press(Top(8, 8));
        Assert.False(tool.Vertical);
        tool.DragTo(new Int3(8, G + 3, 10));
        Assert.Equal(new[] { new Int3(8, G, 8), new Int3(8, G, 9), new Int3(8, G, 10) }, tool.Painted);
    }

    [Fact]
    public void VerticalDrag_FollowsTheMouseRayOnTheFacePlane()
    {
        var tool = new BlockTool(TestContent.Db);
        tool.Press(new PickHit(new Int3(5, G, 5), Int3.East));   // the face plane is x = 6

        var origin = new Vector3(20.5f, G + 10f, 5.5f);
        tool.MoveRay(origin, new Vector3(6f, G + 3.5f, 7.5f) - origin, null);
        Assert.Equal(new Int3(6, G + 3, 7), tool.Painted[^1]);
        Assert.Equal(1 + 3 + 2, tool.Painted.Count);
        Assert.All(tool.Painted, c => Assert.Equal(6, c.X));

        tool.MoveRay(origin, new Vector3(0, 0, 1), new PickHit(new Int3(5, G, 8), Int3.East));   // parallel: the pick
        Assert.Equal(new Int3(6, G, 8), tool.Painted[^1]);
        int n = tool.Painted.Count;
        tool.MoveRay(origin, new Vector3(1, 0, 0), null);   // pointing away from the plane, no pick: kept
        Assert.Equal(n, tool.Painted.Count);

        var west = new PaintDrag(new PickHit(new Int3(5, G, 5), Int3.West), new Int3(4, G, 5), vertical: true);
        Assert.True(west.Vertical);
        Assert.Equal(5f, west.Plane);
        var o2 = new Vector3(-10f, G + 5f, 5.5f);
        Assert.Equal(new Int3(4, G + 2, 3), west.OnPlane(o2, new Vector3(5f, G + 2.5f, 3.5f) - o2));
    }

    [Fact]
    public void PaintedTopDown_IsSentBottomUp_EachCellSupported()
    {
        var sim = Capped();
        var tool = new BlockTool(TestContent.Db);
        tool.TogglePlan();
        tool.Press(new PickHit(new Int3(5, G + 3, 5), Int3.East));   // (6, G+3, 5), held by the cap beside it
        tool.DragTo(new Int3(6, G, 5));
        var painted = tool.Painted.ToList();
        Assert.Equal(new[] { G + 3, G + 2, G + 1, G }, painted.Select(c => c.Y));
        Assert.All(tool.Ghost(sim, null)!.Cells, c => Assert.True(c.Ok));   // a column on the ground, as a set

        var click = tool.Release(sim);
        Assert.Equal(new[] { G, G + 1, G + 2, G + 3 }, click.Commands.Select(c => c.A.Y));
        Assert.All(click.Commands, c => Assert.Equal(new DesignateBuild(BuildShape.Single, c.A, c.A, 1, BlockId.Masonry, true), c));

        // In paint order the sim refuses (G+2 hangs on nothing yet); bottom-up every cell is planned.
        var naive = Capped();
        foreach (var c in painted) naive.Enqueue(new DesignateBuild(BuildShape.Single, c, c, 1, BlockId.Masonry, true));
        naive.Tick();
        Assert.NotEmpty(naive.Events.Drain().OfType<CommandRejected>());

        sim.Events.Drain();
        foreach (var c in click.Commands) sim.Enqueue(c);
        sim.Tick();
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        Assert.All(painted, c => Assert.NotNull(sim.Plans.Get(c)));
    }

    [Fact]
    public void Shortage_OnAVerticalDrag_IsTheTopCells_TheOnesSentLast()
    {
        var sim = Capped(stone: 2);
        var tool = new BlockTool(TestContent.Db);   // Stone wall, 1 stone each
        tool.Press(new PickHit(new Int3(5, G + 3, 5), Int3.East));
        tool.DragTo(new Int3(6, G, 5));             // painted top-down
        var ghost = tool.Ghost(sim, null, FreeStock.Of(sim))!;
        Assert.Equal(2, ghost.ShortCount);
        Assert.Equal(new[] { G + 2, G + 3 }, ghost.Cells.Where(c => c.Short).Select(c => c.Cell.Y).OrderBy(y => y));
        Assert.Contains("Only 2 stone free: 2 blocks short", BlockTool.Tooltip(sim, ghost));

        // Short cells are the last ones sent.
        var sent = tool.Release(sim).Commands.Select(c => c.A).ToList();
        Assert.Equal(ghost.Cells.Where(c => c.Short).Select(c => c.Cell).OrderBy(c => c.Y), sent.Skip(2));
    }

    [Fact]
    public void Hints_AndTheWallScreenshotScript()
    {
        Assert.Contains("side face", BlockTool.ControlsHint);

        var sim = WorldFactory.Create(1, TestContent.Db);
        Assert.Contains("wall", ScreenshotScripts.Names);
        Assert.True(ScreenshotScripts.IsTimed("wall"));
        Assert.Contains("wall", ScreenshotPresets.Names);
        ScreenshotScripts.Run("wall", sim, 2);
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        Assert.Equal(PaintScript.Course1Blocks, sim.Plans.All.Count(p => p.Entry.State == PlanState.Released));

        var wall = PaintScript.WallDrag(sim);
        Assert.NotNull(wall);
        Assert.NotEqual(0, wall!.Value.Start.Normal.X);   // an east or west face
        var tool = new BlockTool(TestContent.Db);
        tool.Select(BlockId.Planks);
        tool.Press(wall.Value.Start);
        foreach (var c in wall.Value.Path) tool.DragTo(c);
        Assert.Equal(PaintScript.WallBlocks, tool.Painted.Count);
        Assert.Single(tool.Painted.Select(c => c.X).Distinct());
        Assert.Equal(PaintScript.WallHeight, tool.Painted.Select(c => c.Y).Distinct().Count());
        Assert.All(tool.Ghost(sim, null)!.Cells, c => Assert.True(c.Ok));

        var shot = ScreenshotPresets.For("wall", sim);
        var site = PaintScript.Site(sim)!.Value;
        Assert.InRange(shot.Focus.X, site.X, site.X + BlocksScript.SiteW);
    }
}
