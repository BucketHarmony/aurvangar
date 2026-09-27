using System.Numerics;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Tools;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M10-T2: the block tool shows a material shortage while dragging (VIEW-21, ADR-072). Worlds are stone to
/// y = 8, so the ground's top faces are picked at y = 8 and the first course is y = 9.</summary>
[Trait("Category", "Unit")]
public class BlockShortageTests
{
    private const int G = 9;
    private static readonly Int3 HubOrigin = new(20, G, 20);

    private static Simulation World(int stone = 0, int planks = 0, int cutstone = 0)
    {
        var b = new ScenarioBuilder().Ground(G - 1).Hub(HubOrigin);
        if (stone > 0) b.Stock("stone", stone);
        if (planks > 0) b.Stock("planks", planks);       // M11-T4: Wood planks cost planks (CRF-02)
        if (cutstone > 0) b.Stock("cutstone", cutstone);   // and Polished stone cut stone
        return b.Agent(new Int3(12, G, 17)).Agent(new Int3(13, G, 17)).Build();
    }

    private static PickHit Top(int x, int z) => new(new Int3(x, G - 1, z), Int3.Up);

    private static ItemId Stone => TestContent.Db.CostOf(BlockId.Masonry).Item;
    private static ItemId Planks => TestContent.Db.CostOf(BlockId.Planks).Item;

    [Fact]
    public void FreeStock_IsStockMinusReleasedNeed_PlanModeAlsoMinusPlanned()
    {
        var sim = World(stone: 10, planks: 4);
        var none = FreeStock.Of(sim);
        Assert.Equal(10, none.Of(Stone, plan: false));
        Assert.Equal(10, none.Of(Stone, plan: true));
        Assert.Equal(4, none.Of(Planks, plan: false));

        sim.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(4, G, 12), new Int3(7, G, 12), 1, BlockId.Masonry, false));   // 4 stone released
        sim.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(4, G, 14), new Int3(6, G, 14), 1, BlockId.Masonry, true));    // 3 stone planned
        sim.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(4, G, 16), new Int3(9, G, 16), 1, BlockId.Planks, false));    // 6 planks released
        sim.Tick();
        var free = FreeStock.Of(sim);
        Assert.Equal(6, free.Of(Stone, plan: false));
        Assert.Equal(3, free.Of(Stone, plan: true));
        Assert.Equal(0, free.Of(Planks, plan: false));   // 4 - 6: never below zero
        Assert.Equal(0, free.Of(Planks, plan: true));
    }

    [Fact]
    public void Drag_CellsBeyondFreeStock_AreShort_InPaintOrder_StillSent()
    {
        var sim = World(stone: 3);
        var tool = new BlockTool(TestContent.Db);   // Stone wall: 1 stone each
        tool.Press(Top(5, 5));
        tool.DragTo(new Int3(9, G, 5));             // 5 cells, painted west to east
        var ghost = tool.Ghost(sim, null, FreeStock.Of(sim))!;

        Assert.Equal(5, ghost.ValidCount);
        Assert.Equal(new[] { false, false, false, true, true }, ghost.Cells.Select(c => c.Short));
        Assert.All(ghost.Cells, c => Assert.True(c.Ok));   // short is not invalid
        Assert.Equal(2, ghost.ShortCount);
        Assert.Equal(new GhostCost(Stone, 1, 3), ghost.Cost);

        string tip = BlockTool.Tooltip(sim, ghost);
        Assert.Contains("5 blocks, 5 stone", tip);
        Assert.Contains("Only 3 stone free: 2 blocks short", tip);

        // The release still sends every valid cell: short cells wait for material (NoMaterial), they are not skipped.
        Assert.Equal(5, tool.Release(sim).Commands.Count);
    }

    [Fact]
    public void ZeroStock_EveryValidCellShort_RedCellsTakeNoStock()
    {
        var sim = World(stone: 2);
        sim.World.SetBlock(new Int3(6, G, 5), BlockId.Stone);   // the second cell is red
        var ghost = BlockTool.GhostFor(sim, BlockId.Masonry, false,
            new[] { new Int3(5, G, 5), new Int3(6, G, 5), new Int3(7, G, 5), new Int3(8, G, 5) }, FreeStock.Of(sim));
        Assert.Equal(new[] { false, false, false, true }, ghost.Cells.Select(c => c.Short));
        Assert.False(ghost.Cells[1].Ok);

        var empty = World();
        var none = BlockTool.GhostFor(empty, BlockId.Masonry, false, new[] { new Int3(5, G, 5), new Int3(6, G, 5) }, FreeStock.Of(empty));
        Assert.All(none.Cells, c => Assert.True(c.Short));
        Assert.Contains("No stone free: 2 blocks short", BlockTool.Tooltip(empty, none));

        // Polished stone costs 2 cut stone: 3 pay for one block.
        var three = World(cutstone: 3);
        var polished = BlockTool.GhostFor(three, BlockId.PolishedStone, false, new[] { new Int3(5, G, 5), new Int3(6, G, 5) }, FreeStock.Of(three));
        Assert.Equal(new[] { false, true }, polished.Cells.Select(c => c.Short));

        // Enough stock: nothing short, the tooltip says what is free.
        var rich = World(stone: 40);
        var fine = BlockTool.GhostFor(rich, BlockId.Masonry, false, new[] { new Int3(5, G, 5), new Int3(6, G, 5) }, FreeStock.Of(rich));
        Assert.Equal(0, fine.ShortCount);
        Assert.Contains("40 stone free", BlockTool.Tooltip(rich, fine));

        // Without stock (scripts, older callers) nothing is short and no stock line is shown.
        var plain = BlockTool.GhostFor(empty, BlockId.Masonry, false, new[] { new Int3(5, G, 5) });
        Assert.Equal(0, plain.ShortCount);
        Assert.Null(plain.Cost);
        Assert.DoesNotContain("free", BlockTool.Tooltip(empty, plain));
    }

    [Fact]
    public void PlanMode_ComparesWithStockAfterTheWholePlan()
    {
        var sim = World(stone: 4);
        sim.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(4, G, 12), new Int3(5, G, 12), 1, BlockId.Masonry, false));   // 2 released
        sim.Enqueue(new DesignateBuild(BuildShape.Single, new Int3(4, G, 14), new Int3(4, G, 14), 1, BlockId.Masonry, true));  // 1 planned
        sim.Tick();
        var cells = new[] { new Int3(5, G, 5), new Int3(6, G, 5) };
        var stock = FreeStock.Of(sim);
        Assert.Equal(0, BlockTool.GhostFor(sim, BlockId.Masonry, false, cells, stock).ShortCount);   // 4 - 2 = 2 free
        var plan = BlockTool.GhostFor(sim, BlockId.Masonry, true, cells, stock);                      // 4 - 2 - 1 = 1 free
        Assert.Equal(new[] { false, true }, plan.Cells.Select(c => c.Short));
        Assert.Contains("free after the plan", BlockTool.Tooltip(sim, plan));
    }

    [Fact]
    public void Ghost_Recomputed_WhenStockChanges()
    {
        var sim = World(stone: 1);
        var tool = new BlockTool(TestContent.Db);
        tool.Press(Top(5, 5));
        tool.DragTo(new Int3(6, G, 5));
        var a = tool.Ghost(sim, null, FreeStock.Of(sim))!;
        Assert.Equal(1, a.ShortCount);
        var richer = World(stone: 5);
        var b = tool.Ghost(sim, null, FreeStock.Of(richer))!;
        Assert.Equal(0, b.ShortCount);
        var same = FreeStock.Of(richer);
        Assert.Same(tool.Ghost(sim, null, same), tool.Ghost(sim, null, same));
    }

    [Fact]
    public void ShortCells_DrawAmber_DistinctFromRedAndPalette()
    {
        var sim = World(stone: 1);
        var ghost = BlockTool.GhostFor(sim, BlockId.Masonry, false,
            new[] { new Int3(5, G, 5), new Int3(6, G, 5), new Int3(7, G, 5) }, FreeStock.Of(sim));
        var blocks = new BlockColors(TestContent.Db);
        var entities = new EntityColors(TestContent.Db);
        var m = BlockGhostMesher.Build(ghost, blocks, entities);
        var palette = blocks.Get(BlockId.Masonry) with { W = BlockGhostMesher.Alpha };
        Assert.Equal(24, m.Colors.Count(c => c == palette));                          // one cell in the palette colour
        Assert.Equal(2 * 24, m.Colors.Count(c => c == ShortCellStyle.Fill(entities)));  // two amber cells
        Assert.Equal(2 * ShortCellStyle.QuadsPerCell + 6, m.QuadCount);                // amber cells have an outline
        Assert.Equal(0, BlockGhostMesher.BuildInvalid(ghost, entities).QuadCount);     // short is not red

        // Amber is its own colour: not red, not the deconstruct orange, not a palette block colour.
        var amber = entities.Short;
        Assert.NotEqual(new Vector4(1, 0, 1, 1), amber);
        Assert.True(Distance(amber, entities.Unreachable) > 0.3f);
        Assert.True(Distance(amber, BlockGhostMesher.DeconstructColor) > 0.3f);
        foreach (var b in new BlockTool(TestContent.Db).Blocks)
            Assert.True(Distance(amber, blocks.Get(b)) > 0.3f, $"{b} is too close to amber");
    }

    private static float Distance(Vector4 a, Vector4 b) =>
        Vector3.Distance(new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, b.Y, b.Z));
}
