using System.Numerics;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Tools;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M9-T3: the top bar never overlaps the toolbar (the M9-T2 monument shot drew "Day 6" over "Cancel (Z)"),
/// and invalid cells stay readable at monument camera distance (the tool ghost's red cells were faint or hidden
/// inside built blocks in the m9t1 blocks shot).</summary>
[Trait("Category", "Unit")]
public class HudLayoutTests
{
    /// <summary>The toolbar in the 1600x900 shots: x 8..792, y 8..40.</summary>
    private static readonly ScreenRect Toolbar = new(8, 8, 784, 32);
    private const float Padding = 0f;

    private static FlowItem W(float w, float lead = 0) => new(lead, w);

    [Fact]
    public void Flow_FitsOnOneLine_WhenWideEnough()
    {
        var items = new[] { W(50), W(150), W(60), W(420) };
        Assert.Equal(new[] { 0, 0, 0, 0 }, HudLayout.Flow(items, 1000, 18));
        Assert.Equal(50 + 150 + 60 + 420 + 3 * 18, HudLayout.LineWidths(items, HudLayout.Flow(items, 1000, 18), 18).Single());
    }

    [Fact]
    public void Flow_BreaksBeforeTheItemThatDoesNotFit_AndDropsItsLead()
    {
        // "Building: stone 88/81" · "Planned: stone 70" , "log 12": leads are " · " and ", ".
        var items = new[] { W(160), W(140, lead: 20), W(60, lead: 10) };
        var lines = HudLayout.Flow(items, 300, 0);
        Assert.Equal(new[] { 0, 1, 1 }, lines);
        // The first item on a line pays no lead: line 1 is 140 + 10 + 60.
        Assert.Equal(new[] { 160f, 210f }, HudLayout.LineWidths(items, lines, 0));
    }

    [Fact]
    public void Flow_ItemWiderThanTheLimit_GetsItsOwnLine()
    {
        var items = new[] { W(100), W(900), W(100) };
        Assert.Equal(new[] { 0, 1, 2 }, HudLayout.Flow(items, 500, 18));
        Assert.Empty(HudLayout.Flow(Array.Empty<FlowItem>(), 500, 18));
    }

    [Fact]
    public void MonumentBar_WrapsBesideTheToolbar_NotOverIt()
    {
        // The G4 monument shot: Day 6, Drought 2 days left, Paused, totals, "Pump has no water"; plan "Building: stone 88/81".
        var row = new[] { W(52), W(152), W(56), W(420), W(150) };
        var plan = new[] { W(162) };
        var bar = HudLayout.ArrangeTopBar(1600, Toolbar, Padding, row, plan);
        Assert.True(bar.Beside);
        Assert.Equal(HudLayout.Margin, bar.Rect.Y);
        Assert.False(bar.Rect.Intersects(Toolbar), $"bar {bar.Rect} overlaps the toolbar {Toolbar}");
        Assert.True(bar.Rect.X >= Toolbar.Right + HudLayout.Gap);
        Assert.Equal(1600 - HudLayout.Margin, bar.Rect.Right);
        Assert.Equal(new[] { 0, 0, 0, 0, 1 }, bar.RowLines);   // the alert wraps to a second line
        Assert.Equal(new[] { 2 }, bar.PlanLines);   // the plan line follows the two row lines
        Assert.Equal(3, bar.LineCount);
    }

    [Fact]
    public void TooWideItem_BarDropsBelowTheToolbar()
    {
        var row = new[] { W(52), W(152), W(900) };   // a long "Unreachable: ..." notice
        var bar = HudLayout.ArrangeTopBar(1600, Toolbar, Padding, row, Array.Empty<FlowItem>());
        Assert.False(bar.Beside);
        Assert.Equal(Toolbar.Bottom + HudLayout.Gap, bar.Rect.Y);
        Assert.False(bar.Rect.Intersects(Toolbar));
    }

    [Fact]
    public void NeverOverlapsTheToolbar_AtAnyWidthOrText()
    {
        var rng = new Rng(7);
        for (int trial = 0; trial < 500; trial++)
        {
            float viewW = 900 + rng.NextInt(1200);
            var row = Enumerable.Range(0, 3 + rng.NextInt(4)).Select(_ => W(30 + rng.NextInt(500))).ToArray();
            var plan = Enumerable.Range(0, rng.NextInt(6)).Select(i => W(60 + rng.NextInt(200), i == 0 ? 0 : 12)).ToArray();
            var bar = HudLayout.ArrangeTopBar(viewW, Toolbar, 12, row, plan);
            Assert.False(bar.Rect.Intersects(Toolbar), $"trial {trial}: bar {bar.Rect} overlaps the toolbar at width {viewW}");
            Assert.True(bar.Rect.Right <= viewW - HudLayout.Margin + 0.01f);
            if (bar.Beside)
                Assert.All(HudLayout.LineWidths(row, bar.RowLines, HudLayout.RowSeparation)
                    .Concat(HudLayout.LineWidths(plan, bar.PlanLines, 0)), w => Assert.True(w + 12 <= bar.Rect.W + 0.01f));
        }
    }

    [Fact]
    public void PlanAtoms_KeepTheHeaderWithItsFirstItem_AndConcatenateToTheText()
    {
        var text = new PlanText(
            new[] { new PlanItem("stone", 88, 81, true) },
            new[] { new PlanItem("stone", 70, null, false), new PlanItem("log", 12, null, false) });
        var atoms = text.Atoms();
        Assert.Equal(3, atoms.Count);
        Assert.Equal("", atoms[0].Lead);
        Assert.Equal(new[] { "Building: ", "stone 88/81" }, atoms[0].Runs.Select(r => r.Text));
        Assert.Equal(PlanText.Separator, atoms[1].Lead);
        Assert.Equal(new[] { "Planned: ", "stone 70" }, atoms[1].Runs.Select(r => r.Text));
        Assert.Equal(", ", atoms[2].Lead);
        Assert.Equal(text.Text, string.Concat(atoms.Select(a => a.Lead + string.Concat(a.Runs.Select(r => r.Text)))));
        Assert.Equal(new[] { true }, atoms.SelectMany(a => a.Runs).Where(r => r.Short).Select(_ => true));
    }

    [Fact]
    public void ScreenRect_BoundsOfPoints()
    {
        var r = ScreenRect.Bounding(new[] { new Vector2(10, 40), new Vector2(30, 5), new Vector2(20, 20) });
        Assert.Equal(new ScreenRect(10, 5, 20, 35), r);
    }

    [Fact]
    public void InvalidCells_AreOpaqueRedWithADarkOutline_DrawnApartFromTheValidGhost()
    {
        var sim = new ScenarioBuilder().Ground(8).Build();
        sim.World.SetBlock(new Int3(7, 9, 5), BlockId.Stone);
        var ghost = BlockTool.GhostFor(sim, BlockId.Planks, false, new[] { new Int3(5, 9, 5), new Int3(6, 9, 5), new Int3(7, 9, 5) });
        var blocks = new BlockColors(TestContent.Db);
        var entities = new EntityColors(TestContent.Db);

        var ok = BlockGhostMesher.Build(ghost, blocks, entities);
        Assert.Equal(2 * 6, ok.QuadCount);
        Assert.DoesNotContain(ok.Colors, c => c.X > c.Y + 0.3f && c.X > c.Z + 0.3f);

        var bad = BlockGhostMesher.BuildInvalid(ghost, entities);
        Assert.Equal(InvalidCellStyle.QuadsPerCell, bad.QuadCount);
        Assert.Equal(6 + 12 * 6, InvalidCellStyle.QuadsPerCell);   // a fill box plus 12 edge bars
        var fill = InvalidCellStyle.Fill(entities);
        var edge = InvalidCellStyle.Edge(entities);
        Assert.True(fill.W >= 0.75f, "the red fill must be strong enough to read at distance");
        Assert.Equal(1f, edge.W);
        Assert.True(edge.X < fill.X * 0.5f, "the outline is darker than the fill");
        Assert.Equal(6 * 4, bad.Colors.Count(c => c == fill));
        Assert.Equal(12 * 6 * 4, bad.Colors.Count(c => c == edge));
        // It reaches outside its (solid) cell, so it shows over the block that makes it invalid.
        Assert.True(bad.Positions.Min(p => p.X) <= 7 - InvalidCellStyle.Inflate + 0.001f);
        Assert.True(InvalidCellStyle.Inflate >= 0.05f);

        Assert.Equal(0, BlockGhostMesher.BuildInvalid(ghost with { Cells = ghost.Cells.Where(c => c.Ok).ToList() }, entities).QuadCount);
        Assert.Equal((new Int3(5, 9, 5), new Int3(7, 9, 5)), ghost.Bounds());
    }

    [Fact]
    public void StuckPlanGhosts_UseTheInvalidCellStyle()
    {
        var blocks = new BlockColors(TestContent.Db);
        var entities = new EntityColors(TestContent.Db);
        var g = new PlanGhost(new Int3(1, 9, 4), new PlanEntry(BlockId.Masonry, PlanState.Released), BuildStatus.NoAccess);
        var m = PlanGhostMesher.Build(new[] { g }, 31, blocks, entities);
        Assert.Equal(InvalidCellStyle.QuadsPerCell, m.QuadCount);
        Assert.Contains(InvalidCellStyle.Fill(entities), m.Colors);
        Assert.Contains(InvalidCellStyle.Edge(entities), m.Colors);
    }
}
