using System.Numerics;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Screenshots;
using Aurvangar.ViewCore.Scripts;
using Aurvangar.ViewCore.Tools;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M11-T11 (VIEW-27): the block tool's shape picker, R to rotate, shaped ghosts and costs, shaped plan ghosts,
/// picking on inner faces, and the <c>shapes</c> screenshot script. Worlds are stone to y = 8; the first course is y = 9.</summary>
[Trait("Category", "Unit")]
public class BlockShapeToolTests
{
    private const int G = 9;
    private static readonly BlockForm Slab = new(BlockShape.Slab, 0);

    private static Simulation World(int stone = 0, int log = 0)
    {
        var b = new ScenarioBuilder().Ground(G - 1).Hub(new Int3(20, G, 20));
        if (stone > 0) b.Stock("stone", stone);
        if (log > 0) b.Stock("log", log);
        return b.Agent(new Int3(12, G, 17)).Agent(new Int3(13, G, 17)).Build();
    }

    private static PickHit Top(int x, int z) => new(new Int3(x, G - 1, z), Int3.Up);

    [Fact]
    public void ShapePicker_ListsTheDataShapes_RotateOnlyTurnsStairs()
    {
        var tool = new BlockTool(TestContent.Db);
        Assert.Equal(new[] { BlockShape.Full, BlockShape.Slab, BlockShape.Stair, BlockShape.Pillar }, tool.Shapes);
        Assert.Equal(BlockForm.Full, tool.Form);
        Assert.False(tool.Rotate());                  // Full has one rotation
        Assert.Equal(BlockForm.Full, tool.Form);

        tool.SelectShape(BlockShape.Stair);
        Assert.Equal(new BlockForm(BlockShape.Stair, 0), tool.Form);
        for (byte r = 1; r <= 4; r++)
        {
            Assert.True(tool.Rotate());
            Assert.Equal(new BlockForm(BlockShape.Stair, (byte)(r % 4)), tool.Form);
        }
        tool.Rotate();                                // rotation 1
        tool.SelectShape(BlockShape.Pillar);          // one rotation: always 0, so the sim never sees BadRotation
        Assert.Equal(new BlockForm(BlockShape.Pillar, 0), tool.Form);
        tool.NextShape();
        Assert.Equal(BlockForm.Full, tool.Form);      // V cycles and wraps
        tool.NextShape();
        Assert.Equal(Slab, tool.Form);

        Assert.Contains("R rotate", BlockTool.ControlsHint);
        Assert.Equal("Stone wall", BlockTool.FormLabel(TestContent.Db, BlockId.Masonry, BlockForm.Full));
        Assert.Equal("Slate tiles stair, climbing east", BlockTool.FormLabel(TestContent.Db, BlockId.Slate, new BlockForm(BlockShape.Stair, 1)));
        Assert.Equal("Slate tiles stair, climbing south", BlockTool.FormLabel(TestContent.Db, BlockId.Slate, new BlockForm(BlockShape.Stair, 0)));
        Assert.Equal("Wood planks slab", BlockTool.FormLabel(TestContent.Db, BlockId.Planks, Slab));
    }

    [Fact]
    public void Release_SendsTheForm_AndTheSimPlansIt()
    {
        var sim = World(stone: 20);
        var tool = new BlockTool(TestContent.Db);
        tool.Select(BlockId.Slate);
        tool.SelectShape(BlockShape.Stair);
        tool.Rotate();
        tool.Rotate();
        tool.Press(Top(5, 5));
        tool.DragTo(new Int3(6, G, 5));
        var click = tool.Release(sim);
        Assert.Equal(2, click.Commands.Count);
        Assert.All(click.Commands, c => Assert.Equal(new BlockForm(BlockShape.Stair, 2), c.Form));
        foreach (var c in click.Commands) sim.Enqueue(c);
        sim.Tick();
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        Assert.Equal(new BlockForm(BlockShape.Stair, 2), sim.Plans.Get(new Int3(5, G, 5))!.Value.Form);
    }

    [Fact]
    public void Ghost_PricesTheShape_AndShortageUsesTheShapedCost()
    {
        var sim = World(stone: 5);
        var tool = new BlockTool(TestContent.Db);
        tool.Select(BlockId.Slate);                   // 3 stone full, 2 as a slab (CON-20)
        tool.SelectShape(BlockShape.Slab);
        tool.Press(Top(4, 5));
        tool.DragTo(new Int3(6, G, 5));
        var ghost = tool.Ghost(sim, null, FreeStock.Of(sim))!;
        Assert.Equal(Slab, ghost.Form);
        Assert.Equal(2, ghost.Cost!.Value.PerBlock);
        Assert.Equal(1, ghost.ShortCount);            // 5 stone: two slabs, the third is short
        string tip = BlockTool.Tooltip(sim, ghost);
        Assert.Contains("Build Slate tiles slab (3 blocks, 6 stone)", tip);
        Assert.Contains("1 block short", tip);

        tool.SelectShape(BlockShape.Pillar);          // 1 stone each: all three fit
        var pillars = tool.Ghost(sim, null, FreeStock.Of(sim))!;
        Assert.Equal(0, pillars.ShortCount);
        Assert.Contains("3 stone", BlockTool.Tooltip(sim, pillars));
    }

    [Fact]
    public void GhostMesh_ShowsTheShape()
    {
        var sim = World(stone: 20);
        var colors = new BlockColors(TestContent.Db);
        var entities = new EntityColors(TestContent.Db);
        var slab = BlockTool.GhostFor(sim, BlockId.Masonry, false, new[] { new Int3(5, G, 5) }, form: Slab);
        var m = BlockGhostMesher.Build(slab, colors, entities);
        Assert.Equal(6, m.QuadCount);
        Assert.InRange(m.Positions.Max(p => p.Y), G + 0.5f, G + 0.55f);
        var pillar = BlockTool.GhostFor(sim, BlockId.Masonry, false, new[] { new Int3(5, G, 5) }, form: new BlockForm(BlockShape.Pillar, 0));
        var pm = BlockGhostMesher.Build(pillar, colors, entities);
        Assert.InRange(pm.Positions.Min(p => p.X), 5.2f, 5.25f);
        Assert.InRange(pm.Positions.Max(p => p.Y), G + 1f, G + 1.05f);
        var full = BlockTool.GhostFor(sim, BlockId.Masonry, false, new[] { new Int3(5, G, 5) });
        Assert.Equal(6, BlockGhostMesher.Build(full, colors, entities).QuadCount);
    }

    [Fact]
    public void PlanGhosts_ShowTheShape_AndTheSignatureSeesAFormChange()
    {
        var sim = World(stone: 20);
        sim.Enqueue(new DesignateBuild(BuildShape.Single, new Int3(5, G, 5), new Int3(5, G, 5), 1, BlockId.Masonry, true, Slab));
        sim.Tick();
        var colors = new BlockColors(TestContent.Db);
        var entities = new EntityColors(TestContent.Db);
        var m = PlanGhostMesher.Build(PlanGhostMesher.Ghosts(sim, sim.World.SizeY - 1), sim.World.SizeY - 1, colors, entities);
        Assert.Equal(6, m.QuadCount);
        Assert.InRange(m.Positions.Max(p => p.Y), G + 0.4f, G + 0.5f);
        ulong before = PlanGhostMesher.Signature(sim);
        sim.Enqueue(new DesignateBuild(BuildShape.Single, new Int3(5, G, 5), new Int3(5, G, 5), 1, BlockId.Masonry, true,
            new BlockForm(BlockShape.Pillar, 0)));
        sim.Tick();
        Assert.NotEqual(before, PlanGhostMesher.Signature(sim));
    }

    [Fact]
    public void Pick_OnAnInnerFace_ResolvesToTheShapedCell()
    {
        var w = new VoxelWorld(32, 32, 32, TestContent.Db.SolidTable);
        // A slab's top at y = 5.5 and a rotation-0 stair's riser (facing -Z) at z = 7.5, both in cell (3,5,7).
        var top = PickResolver.Resolve(w, new Vector3(3.5f, 5.5f, 7.5f), Vector3.UnitY, 31)!.Value;
        Assert.Equal(new Int3(3, 5, 7), top.Cell);
        Assert.Equal(new Int3(3, 6, 7), top.Adjacent);
        var riser = PickResolver.Resolve(w, new Vector3(3.5f, 5.75f, 7.5f), -Vector3.UnitZ, 31)!.Value;
        Assert.Equal(new Int3(3, 5, 7), riser.Cell);
        var pillarSide = PickResolver.Resolve(w, new Vector3(3.75f, 5.5f, 7.5f), Vector3.UnitX, 31)!.Value;
        Assert.Equal(new Int3(3, 5, 7), pillarSide.Cell);
        Assert.Equal(new Int3(4, 5, 7), pillarSide.Adjacent);
    }

    [Fact]
    public void ShapesScript_Seed1_BuildsAStairSlabsAndPillars()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        Assert.Contains("shapes", ScreenshotScripts.Names);
        Assert.Contains("shapes", ScreenshotPresets.Names);
        var site = ShapesScript.Site(sim);
        Assert.NotNull(site);
        ScreenshotScripts.Run("shapes", sim, ShapesScript.DoneTicks);
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        var forms = sim.World.Forms.Select(f => f.Form.Shape).ToList();
        Assert.Equal(ShapesScript.StairCount, forms.Count(s => s == BlockShape.Stair));
        Assert.Equal(ShapesScript.SlabCount, forms.Count(s => s == BlockShape.Slab));
        Assert.Equal(ShapesScript.PillarCount, forms.Count(s => s == BlockShape.Pillar));
        Assert.Equal(0, sim.Plans.All.Count(p => p.Item2.State == PlanState.Released));   // every released entry built
        Assert.Equal(site, ShapesScript.Site(sim));
        var drag = ShapesScript.GhostDrag(sim);
        Assert.NotNull(drag);
        var shot = ScreenshotPresets.For("shapes", sim);
        Assert.InRange(shot.Focus.X, site!.Value.X, site.Value.X + BlocksScript.SiteW);
    }
}
