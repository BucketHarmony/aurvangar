using System.Numerics;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Screenshots;
using Aurvangar.ViewCore.Scripts;
using Aurvangar.Sim.Events;
using Aurvangar.ViewCore.Tools;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M8-T5: the block tool, plan ghosts and material totals (view-ui.md VIEW-21..23, construction.md CON-01
/// colours). Worlds are stone to y = 8, so the ground's top faces are picked at y = 8 and anchors sit at y = 9.</summary>
[Trait("Category", "Unit")]
public class BlockToolTests
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

    private static int Top(Simulation sim) => sim.World.SizeY - 1;

    [Fact]
    public void Ghost_UsesBuildShapes_InvalidCellsRedWithReason()
    {
        var sim = World();
        sim.World.SetBlock(new Int3(7, G, 5), BlockId.Stone);   // in the way of the x-lines at z = 5
        var tool = new BlockTool(TestContent.Db);
        Assert.Equal(new[] { BlockId.Masonry, BlockId.Planks, BlockId.PolishedStone }, tool.Blocks);
        Assert.Equal(new[] { "Stone wall", "Wood planks", "Polished stone" }, tool.Blocks.Select(TestContent.Db.LabelOf));

        // The anchor is the air cell on the picked face: the top face of (5,8,5) looks into (5,9,5); a side face
        // looks sideways.
        Assert.Equal(new Int3(5, G, 5), BlockTool.Anchor(Top(5, 5)));
        Assert.Equal(new Int3(4, G, 5), BlockTool.Anchor(new PickHit(new Int3(5, G, 5), Int3.West)));

        foreach (var shape in BlockTool.Shapes)
        {
            tool.SetShape(shape);
            tool.Press(Top(5, 5));
            tool.Move(Top(9, 7));
            var ghost = tool.Ghost(sim, null, Top(sim));
            Assert.NotNull(ghost);
            int h = BuildShapes.UsesHeight(shape) ? BlockTool.DefaultHeight : 1;
            var expected = BuildShapes.Cells(shape, new Int3(5, G, 5), new Int3(9, G, 7), h);
            Assert.Equal(expected, ghost!.Cells.Select(c => c.Cell));
            // The batched validity equals CON-08 cell by cell, with the drag's cells as pending.
            Assert.All(ghost.Cells, c => Assert.Equal(BlockPlans.CanPlan(sim, c.Cell, expected), c.Result));
            tool.AbortDrag();
        }

        // A Line through the stone block: that cell is red with the CON-08 reason; the rest are fine.
        tool.SetShape(BuildShape.Line);
        tool.Press(Top(5, 5));
        tool.Move(Top(9, 5));
        var line = tool.Ghost(sim, null, Top(sim))!;
        Assert.Equal(PlanResult.Solid, line.Cells.Single(c => c.Cell == new Int3(7, G, 5)).Result);
        Assert.False(line.Cells.Single(c => c.Cell == new Int3(7, G, 5)).Ok);
        Assert.Equal(4, line.Cells.Count(c => c.Ok));
        Assert.Contains(BlockTool.ReasonText(PlanResult.Solid)!, BlockTool.Tooltip(sim, line));
        Assert.All(Enum.GetValues<PlanResult>().Where(r => r != PlanResult.Ok), r => Assert.False(string.IsNullOrEmpty(BlockTool.ReasonText(r))));
        Assert.Null(BlockTool.ReasonText(PlanResult.Ok));

        // A cell with no support is red with its reason (a Single on the side of a hanging stone, nothing below).
        sim.World.SetBlock(new Int3(3, G + 4, 3), BlockId.Stone);
        var floating = BlockTool.GhostFor(sim, new DesignateBuild(BuildShape.Single, new Int3(3, G + 5, 3), new Int3(3, G + 5, 3), 1, BlockId.Masonry, false));
        Assert.Equal(PlanResult.Ok, floating.Cells.Single().Result);   // on top of the stone: supported
        var hanging = BlockTool.GhostFor(sim, new DesignateBuild(BuildShape.Single, new Int3(3, G + 2, 3), new Int3(3, G + 2, 3), 1, BlockId.Masonry, false));
        Assert.Equal(PlanResult.Unsupported, hanging.Cells.Single().Result);

        // Release sends DesignateBuild with the chosen block and the Plan flag (P toggles); without Shift the tool ends.
        tool.Select(BlockId.Planks);
        tool.TogglePlan();
        Assert.True(tool.Plan);
        var click = tool.Release(sim, Top(9, 5), Top(sim), shift: false);
        Assert.Equal(new DesignateBuild(BuildShape.Line, new Int3(5, G, 5), new Int3(9, G, 5), 1, BlockId.Planks, true), click.Command);
        Assert.False(click.KeepTool);
        Assert.False(tool.Dragging);
        tool.TogglePlan();
        tool.Press(Top(5, 9));
        var kept = tool.Release(sim, Top(5, 12), Top(sim), shift: true);
        Assert.Equal(new DesignateBuild(BuildShape.Line, new Int3(5, G, 9), new Int3(5, G, 12), 1, BlockId.Planks, false), kept.Command);
        Assert.True(kept.KeepTool);

        // A drag with no valid cell sends nothing and says why.
        tool.SetShape(BuildShape.Single);
        tool.Press(new PickHit(new Int3(7, G - 1, 5), Int3.Up));   // anchor (7,9,5) is the stone block
        var none = tool.Release(sim, null, Top(sim), shift: true);
        Assert.Null(none.Command);
        Assert.Contains(BlockTool.ReasonText(PlanResult.Solid)!, none.Message);

        // Without a drag the ghost follows the hover: the shape anchored and ended at the hovered cell.
        tool.SetShape(BuildShape.Wall);
        var hover = tool.Ghost(sim, Top(12, 5), Top(sim))!;
        Assert.Equal(BuildShapes.Cells(BuildShape.Wall, new Int3(12, G, 5), new Int3(12, G, 5), BlockTool.DefaultHeight), hover.Cells.Select(c => c.Cell));
        Assert.Null(tool.Ghost(sim, null, Top(sim)));
    }

    [Fact]
    public void Height_FromSliceAndKeys()
    {
        // Wall/Box height starts at SliceY - A.Y + 1 with the slice active, else 3 (clamped to 1..32).
        Assert.Equal(4, BlockTool.StartHeight(anchorY: 9, sliceY: 12, sizeY: 32));
        Assert.Equal(1, BlockTool.StartHeight(anchorY: 9, sliceY: 9, sizeY: 32));
        Assert.Equal(1, BlockTool.StartHeight(anchorY: 13, sliceY: 12, sizeY: 32));
        Assert.Equal(BlockTool.DefaultHeight, BlockTool.StartHeight(anchorY: 9, sliceY: 31, sizeY: 32));
        Assert.Equal(32, BlockTool.StartHeight(anchorY: 1, sliceY: 62, sizeY: 64));

        var sim = World();
        var tool = new BlockTool(TestContent.Db);
        tool.SetShape(BuildShape.Wall);
        Assert.Equal(4, tool.Height(9, 12, 32));
        tool.AdjustHeight(+1, 9, 12, 32);
        Assert.Equal(5, tool.Height(9, 12, 32));
        Assert.Equal(5, tool.Height(9, 31, 32));     // a changed height sticks until the tool is reset
        tool.AdjustHeight(-10, 9, 12, 32);
        Assert.Equal(BuildShapes.MinHeight, tool.Height(9, 12, 32));
        tool.AdjustHeight(+100, 9, 12, 32);
        Assert.Equal(BuildShapes.MaxHeight, tool.Height(9, 12, 32));
        tool.Reset();
        Assert.Equal(BlockTool.DefaultHeight, tool.Height(9, 31, 32));

        // The command carries the height for Wall and Box; the others send 1 and their cells ignore it.
        int slice = 11;   // slice active: 11 - 9 + 1 = 3
        tool.AdjustHeight(+2, 9, slice, sim.World.SizeY);   // 5
        foreach (var shape in BlockTool.Shapes)
        {
            tool.SetShape(shape);
            tool.Press(Top(2, 2));
            var cmd = Assert.IsType<DesignateBuild>(tool.Release(sim, Top(6, 4), slice, shift: true).Command);
            Assert.Equal(BuildShapes.UsesHeight(shape) ? 5 : 1, cmd.Height);
        }
        tool.SetShape(BuildShape.Line);
        tool.Press(Top(2, 2));
        tool.Move(Top(6, 2));
        Assert.Equal(5, tool.Ghost(sim, null, slice)!.Cells.Count);   // a Line is one course whatever the height
    }

    [Fact]
    public void ReleaseAndDeconstructTools_SendCommands()
    {
        Assert.Equal(ToolKind.Blocks, ToolController.ForHotkey('K'));
        Assert.Equal(ToolKind.Release, ToolController.ForHotkey('L'));
        Assert.True(ToolController.IsDragTool(ToolKind.Release));

        // Release: the columns under the drag, from the lower pick up to the view level.
        var t = new ToolController();
        t.SetTool(ToolKind.Release);
        t.Press(Top(6, 9));
        t.Move(Top(3, 4));
        Assert.Equal(new ReleasePlan(new Int3(3, G - 1, 4), new Int3(6, 20, 9)), t.Release(null, 20));
        Assert.Equal(ToolKind.Release, t.Tool);

        var sim = World();
        var max = new Int3(sim.World.SizeX - 1, sim.World.SizeY - 1, sim.World.SizeZ - 1);
        Assert.Equal(new ReleasePlan(new Int3(0, 0, 0), max), ToolController.ReleaseAll(sim.World));

        // Deconstruct: a press on a building is the BLD-09 click; a press over no building starts a box drag that
        // sends DesignateDeconstructBlocks for the same columns.
        var onHub = DeconstructTool.Press(sim, new PickHit(HubOrigin + Int3.Up, Int3.Up));
        Assert.Equal(DeconstructTool.Click(sim, new PickHit(HubOrigin + Int3.Up, Int3.Up)), (onHub.Command, onHub.Message));
        Assert.NotNull(onHub.Message);        // the Great Hall is prebuilt-only: refused, and no drag either
        Assert.False(onHub.StartDrag);
        var onGround = DeconstructTool.Press(sim, Top(3, 4));
        Assert.Null(onGround.Command);
        Assert.True(onGround.StartDrag);
        Assert.False(DeconstructTool.Press(sim, null).StartDrag);

        t.SetTool(ToolKind.Deconstruct);
        t.Press(Top(3, 4));
        Assert.False(t.Dragging);             // Deconstruct only drags when the caller starts it
        t.BeginDrag(Top(3, 4));
        Assert.True(t.Dragging);
        Assert.Equal(new DesignateDeconstructBlocks(new Int3(3, G - 1, 4), new Int3(6, 25, 9)), t.Release(Top(6, 9), 25));
        Assert.False(t.Dragging);
    }

    [Fact]
    public void PlanGhosts_AlphaByState_RedWhenStuck()
    {
        var content = TestContent.Db;
        var blocks = new BlockColors(content);
        var entities = new EntityColors(content);
        var masonry = blocks.Get(BlockId.Masonry);
        PlanGhost Ghost(int x, PlanState s, BuildStatus st) => new(new Int3(x, G, 4), new PlanEntry(BlockId.Masonry, s), st);

        var released = PlanGhostMesher.Build(new[] { Ghost(1, PlanState.Released, BuildStatus.Ready) }, 31, blocks, entities);
        Assert.Equal(6, released.QuadCount);
        Assert.All(released.Colors, c => Assert.Equal(masonry with { W = PlanGhostMesher.ReleasedAlpha }, c));
        Assert.Equal(0.45f, PlanGhostMesher.ReleasedAlpha);

        var planned = PlanGhostMesher.Build(new[] { Ghost(1, PlanState.Planned, BuildStatus.Planned) }, 31, blocks, entities);
        Assert.Equal(0.25f, PlanGhostMesher.PlannedAlpha);
        Assert.All(planned.Colors, c =>
        {
            Assert.Equal(PlanGhostMesher.PlannedAlpha, c.W);
            Assert.True(c.X > masonry.X && c.Y > masonry.Y && c.Z > masonry.Z);   // lightened
        });

        foreach (var s in Enum.GetValues<BuildStatus>())
        {
            bool stuck = s is BuildStatus.GivenUp or BuildStatus.NoAccess or BuildStatus.WouldStrand or BuildStatus.NoSupport;
            Assert.Equal(stuck, PlanGhostMesher.IsStuck(s));
            var m = PlanGhostMesher.Build(new[] { Ghost(1, PlanState.Released, s) }, 31, blocks, entities);
            var c = m.Colors[0];
            if (stuck) Assert.True(c.X > c.Y + 0.3f && c.X > c.Z + 0.3f, $"{s} should be red: {c}");
            else Assert.Equal(masonry with { W = PlanGhostMesher.ReleasedAlpha }, c);
        }

        // Ghosts above the slice are hidden.
        var sliced = PlanGhostMesher.Build(new[] { Ghost(1, PlanState.Released, BuildStatus.Ready), new PlanGhost(new Int3(2, G + 1, 4),
            new PlanEntry(BlockId.Planks, PlanState.Released), BuildStatus.BelowFirst) }, G, blocks, entities);
        Assert.Equal(6, sliced.QuadCount);

        // From the sim: one scan gives every entry's status; hovering an entry names its label and status.
        var sim = World(stone: 20);
        sim.Enqueue(new DesignateBuild(BuildShape.Wall, new Int3(4, G, 12), new Int3(5, G, 12), 2, BlockId.Masonry, false));
        sim.Enqueue(new DesignateBuild(BuildShape.Single, new Int3(8, G, 12), new Int3(8, G, 12), 1, BlockId.Planks, true));
        sim.Tick();
        var ghosts = PlanGhostMesher.Ghosts(sim, sim.World.SizeY - 1);
        Assert.Equal(5, ghosts.Count);
        Assert.All(ghosts, g => Assert.Equal(sim.Plans.StatusOf(sim, g.Cell), g.Status));
        Assert.Empty(PlanGhostMesher.Ghosts(sim, G - 1));
        Assert.Equal("Stone wall: waiting for the block below",
            PlanGhostMesher.HoverText(sim, new PickHit(new Int3(4, G + 1, 11), new Int3(0, 0, 1)), 31));   // face into (4,10,12)
        Assert.Equal("Wood planks: planned, not released yet", PlanGhostMesher.HoverText(sim, Top(8, 12), 31));
        Assert.Null(PlanGhostMesher.HoverText(sim, Top(9, 12), 31));
        Assert.Null(PlanGhostMesher.HoverText(sim, Top(8, 12), G - 1));   // above the slice: hidden
        Assert.All(Enum.GetValues<BuildStatus>(), s => Assert.False(string.IsNullOrEmpty(PlanGhostMesher.StatusText(sim, BlockId.Masonry, s))));
        Assert.NotEqual(PlanGhostMesher.Signature(sim), PlanGhostMesher.Signature(World()));
    }

    [Fact]
    public void TopBar_PlanMaterialText()
    {
        var sim = World(stone: 5, log: 20);
        Assert.True(TopBarModel.PlanText(sim).IsEmpty);
        Assert.Equal("", TopBarModel.PlanText(sim).Text);

        sim.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(4, G, 12), new Int3(9, G, 12), 1, BlockId.Masonry, false));   // 6 stone
        sim.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(4, G, 14), new Int3(6, G, 14), 1, BlockId.Planks, true));     // 3 log
        sim.Enqueue(new DesignateBuild(BuildShape.Single, new Int3(4, G, 16), new Int3(4, G, 16), 1, BlockId.PolishedStone, true)); // 2 stone
        sim.Tick();

        var text = TopBarModel.PlanText(sim);
        Assert.False(text.IsEmpty);
        Assert.Equal("Building: stone 6/5 · Planned: log 3, stone 2", text.Text);
        // Short: released need over stock; planned need plus released need over stock.
        Assert.Equal(new[] { true }, text.Building.Select(i => i.Short));
        Assert.Equal(new[] { false, true }, text.Planned.Select(i => i.Short));
        Assert.Equal("Building: [color=#ffa640]stone 6/5[/color] · Planned: log 3, [color=#ffa640]stone 2[/color]", text.BbCode("#ffa640"));

        Assert.Equal(text.Text, string.Concat(text.Segments().Select(r => r.Text)));
        Assert.Equal(new[] { "stone 6/5", "stone 2" }, text.Segments().Where(r => r.Short).Select(r => r.Text));

        // Only planned entries: no "Building" part.
        var planOnly = World(stone: 50);
        planOnly.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(4, G, 12), new Int3(5, G, 12), 1, BlockId.Masonry, true));
        planOnly.Tick();
        Assert.Equal("Planned: stone 2", TopBarModel.PlanText(planOnly).Text);
    }

    [Fact]
    public void ChunkMesher_ConstructionBlocksUsePaletteColours()
    {
        var content = TestContent.Db;
        var colors = new BlockColors(content);
        var magenta = new Vector4(1, 0, 1, 1);
        var w = new VoxelWorld(32, 32, 32, content.SolidTable);
        w.SetBlock(new Int3(1, 1, 1), BlockId.Stone);
        w.SetBlock(new Int3(2, 1, 1), BlockId.Masonry);   // beside Stone: must not merge with it
        w.SetBlock(new Int3(5, 1, 1), BlockId.Planks);
        w.SetBlock(new Int3(8, 1, 1), BlockId.PolishedStone);
        var m = ChunkMesher.Build(w, 0, 0, 0, 31, colors);
        Assert.DoesNotContain(magenta, m.Colors);
        foreach (var (id, hex) in new[] { (BlockId.Masonry, "#a39e94"), (BlockId.Planks, "#b98a52"), (BlockId.PolishedStone, "#d8d2c4") })
        {
            Assert.Equal(BlockColors.ParseHex(hex), colors.Get(id));
            Assert.Equal(24, m.Colors.Count(c => c == colors.Get(id)) + (id == BlockId.Masonry ? 4 : 0));   // Masonry: 5 faces (one shared with Stone)
        }
        Assert.Equal(20, m.Colors.Count(c => c == colors.Get(BlockId.Stone)));   // Stone: 5 faces, none merged with Masonry
        Assert.Equal(5 + 5 + 6 + 6, m.QuadCount);
    }

    [Fact]
    public void ToolGhostMesh_PaletteColourOrRed()
    {
        var sim = World();
        sim.World.SetBlock(new Int3(7, G, 5), BlockId.Stone);
        var ghost = BlockTool.GhostFor(sim, new DesignateBuild(BuildShape.Line, new Int3(5, G, 5), new Int3(8, G, 5), 1, BlockId.Planks, false));
        var blocks = new BlockColors(TestContent.Db);
        var entities = new EntityColors(TestContent.Db);
        var m = BlockGhostMesher.Build(ghost, blocks, entities);
        Assert.Equal(4 * 6, m.QuadCount);
        Assert.Equal(3 * 24, m.Colors.Count(c => c == blocks.Get(BlockId.Planks) with { W = BlockGhostMesher.Alpha }));
        Assert.Equal(24, m.Colors.Count(c => c == entities.Unreachable with { W = BlockGhostMesher.BadAlpha }));
    }

    [Fact]
    public void BlocksScript_Seed1_AllCommandsAccepted_PresetFindsSite()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var site = BlocksScript.Site(sim);
        Assert.NotNull(site);
        ScreenshotScripts.Run("blocks", sim, 1);
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        Assert.Equal(12 + 18 + 64, sim.Plans.Count);
        Assert.Equal(site, BlocksScript.Site(sim));   // recovered from the planned box after the run
        var shot = ScreenshotPresets.For("blocks", sim);
        Assert.InRange(shot.Focus.X, site!.Value.X, site.Value.X + BlocksScript.SiteW);
        Assert.NotNull(BlocksScript.GhostDrag(sim));
    }
}
