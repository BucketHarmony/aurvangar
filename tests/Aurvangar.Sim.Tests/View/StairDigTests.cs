using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.ViewCore.Hud;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Screenshots;
using Aurvangar.ViewCore.Scripts;
using Aurvangar.ViewCore.Tools;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M11-T8: the dig tool's stair-down mode (VIEW-24), the dig hover reasons (VIEW-25) and the slice hint
/// (VIEW-26).</summary>
[Trait("Category", "Unit")]
public class StairDigTests
{
    private static PickHit Top(int x, int y, int z) => new(new Int3(x, y, z), Int3.Up);

    [Fact]
    public void Steps_OneLevelDownPerCellAlongTheDrag_ToTheViewLevel()
    {
        var steps = StairDig.Steps(Top(5, 20, 5), Top(9, 20, 6), 17);
        Assert.Equal(new[] { new Int3(5, 20, 5), new Int3(6, 19, 5), new Int3(7, 18, 5), new Int3(8, 17, 5) }, steps);
    }

    [Fact]
    public void Direction_MainAxisOfTheDrag_PlusXWhenStill()
    {
        Assert.Equal(new Int3(0, 0, -1), StairDig.Direction(Top(5, 20, 5), Top(6, 20, 1)));
        Assert.Equal(new Int3(-1, 0, 0), StairDig.Direction(Top(5, 20, 5), Top(2, 20, 7)));
        Assert.Equal(new Int3(1, 0, 0), StairDig.Direction(Top(5, 20, 5), Top(5, 20, 5)));
        var steps = StairDig.Steps(Top(5, 20, 5), Top(5, 20, 3), 18);
        Assert.Equal(new[] { new Int3(5, 20, 5), new Int3(5, 19, 4), new Int3(5, 18, 3) }, steps);
    }

    [Fact]
    public void Bottom_IsTheSecondPickClampedToTheSlice_NeverAboveTheFirst()
    {
        Assert.Equal(18, StairDig.BottomY(Top(5, 20, 5), Top(8, 18, 5), 63));
        Assert.Equal(15, StairDig.BottomY(Top(5, 20, 5), Top(8, 18, 5), 15));
        Assert.Equal(20, StairDig.BottomY(Top(5, 20, 5), Top(8, 22, 5), 63));
        Assert.Single(StairDig.Steps(Top(5, 20, 5), Top(8, 22, 5), 63));
    }

    [Fact]
    public void Commands_OneThreeHighColumnPerStep()
    {
        var commands = StairDig.Commands(Top(5, 20, 5), Top(9, 20, 5), 18);
        Assert.Equal(new ICommand[]
        {
            new DesignateDig(new Int3(5, 20, 5), new Int3(5, 22, 5)),
            new DesignateDig(new Int3(6, 19, 5), new Int3(6, 21, 5)),
            new DesignateDig(new Int3(7, 18, 5), new Int3(7, 20, 5)),
        }, commands);
    }

    [Fact]
    public void SolidCells_OnlyTheSolidOnes()
    {
        var sim = new ScenarioBuilder().Ground(20).Build();   // solid up to y = 20
        var cells = StairDig.SolidCells(sim.World, Top(5, 20, 5), Top(9, 20, 5), 18);
        Assert.Equal(new[]
        {
            new Int3(5, 20, 5),
            new Int3(6, 19, 5), new Int3(6, 20, 5),
            new Int3(7, 18, 5), new Int3(7, 19, 5), new Int3(7, 20, 5),
        }, cells);
    }

    [Fact]
    public void ToolController_StairMode_SendsTheColumns_AndKeepsTheMode()
    {
        var t = new ToolController();
        t.SetTool(ToolKind.Dig);
        Assert.Equal(DigMode.Box, t.DigMode);
        t.ToggleDigMode();
        Assert.True(t.StairMode);
        t.Press(Top(5, 20, 5));
        t.Move(Top(9, 20, 5));
        Assert.Null(t.PreviewBox(18));
        Assert.Equal("Stair down 2 levels to level 18", t.StairText(18));
        Assert.Throws<InvalidOperationException>(() => t.Release(null, 18));
        t.Press(Top(5, 20, 5));
        Assert.Equal(StairDig.Commands(Top(5, 20, 5), Top(9, 20, 5), 18), t.ReleaseCommands(Top(9, 20, 5), 18));
        Assert.False(t.Dragging);

        t.SetTool(ToolKind.Chop);
        Assert.False(t.StairMode);   // only the dig tool digs stairs
        t.SetTool(ToolKind.Dig);
        Assert.True(t.StairMode);    // the mode is kept
        t.ToggleDigMode();
        t.Press(Top(5, 20, 5));
        Assert.Equal(new ICommand[] { new DesignateDig(new Int3(5, 20, 5), new Int3(9, 18, 5)) }, t.ReleaseCommands(Top(9, 18, 5), 63));
        Assert.Empty(t.ReleaseCommands(Top(9, 18, 5), 63));   // no drag
    }

    [Fact]
    public void StairText_AsksForTheViewLevel_WhenFlat()
    {
        Assert.StartsWith("Stair down: lower the view level", StairDig.DragText(Top(5, 20, 5), Top(9, 20, 5), 63));
        Assert.Equal("Stair down 1 level to level 19", StairDig.DragText(Top(5, 20, 5), Top(9, 20, 5), 19));
    }

    [Fact]
    public void SliceHint_NamesTheKeysAndTheLevel()
    {
        Assert.Equal("View level: top (63) · PageUp/PageDown or [ ] to slice", SliceHint.Text(63, 63));
        Assert.Equal("View level: 40 of 63 (23 down) · PageUp/PageDown or [ ] to move", SliceHint.Text(40, 63));
    }

    [Theory]
    [InlineData(DigWait.WouldTrap, "Dig: would trap a dwarf")]
    [InlineData(DigWait.CellAbove, "Dig: waiting for the cell above")]
    [InlineData(DigWait.Unreachable, "Dig: unreachable")]
    public void DigHover_TextPerReason(DigWait wait, string start) => Assert.StartsWith(start, DigHover.Text(wait));

    [Fact]
    public void StairsScript_SeedOne_StairToStone_WithAWaitingPitCell()
    {
        Assert.Contains("stairs", ScreenshotScripts.Names);
        Assert.Contains("stairs", ScreenshotPresets.Names);
        var sim = WorldFactory.Create(1, TestContent.Db);
        var plan = StairScript.Site(sim);
        Assert.NotNull(plan);
        Assert.True(plan!.Steps > 1);
        var commands = ScreenshotScripts.For("stairs", sim);
        Assert.Equal(StairDig.Commands(plan.First, plan.Second, plan.BottomY), commands.Take(plan.Steps + 1));
        var shot = ScreenshotPresets.For("stairs", sim);
        Assert.Equal(plan.First.Cell.Y - 1, shot.SliceY);

        foreach (var c in commands) sim.Enqueue(c);
        for (int t = 0; t < 700; t++) sim.Tick();
        var pick = StairScript.TooltipPick(sim);
        Assert.NotNull(pick);
        Assert.NotNull(DigHover.For(sim, pick, 63));
    }

    [Fact]
    public void DigHover_NullWithoutAMark()
    {
        var sim = new ScenarioBuilder().Ground(8).Agent(new Int3(5, 9, 5)).Build();
        Assert.Null(DigHover.For(sim, Top(10, 8, 10), 63));
        Assert.Null(DigHover.For(sim, null, 63));
    }
}
