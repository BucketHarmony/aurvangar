using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Screenshots;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M4-T11: the screenshot harness can enqueue a dig + chop script so shots show colonists at work.</summary>
[Trait("Category", "Unit")]
public class ScreenshotScriptTests
{
    [Fact]
    public void Args_ScriptDefaultsToNone_AndParses()
    {
        Assert.Equal("none", ScreenshotArgs.Parse(Array.Empty<string>()).Script);
        Assert.Equal("digchop", ScreenshotArgs.Parse(new[] { "--script", "digchop" }).Script);
        Assert.Throws<ArgumentException>(() => ScreenshotArgs.Parse(new[] { "--script", "moon" }));
    }

    [Fact]
    public void None_IsEmpty() =>
        Assert.Empty(ScreenshotScripts.For("none", WorldFactory.Create(1, TestContent.Db)));

    [Fact]
    public void DigChop_Seed1_MarksPitAndTrees_AndColonistsWork()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var commands = ScreenshotScripts.For("digchop", sim);
        Assert.IsType<DesignateDig>(commands[0]);
        Assert.IsType<DesignateChop>(commands[1]);
        foreach (var c in commands) sim.Enqueue(c);
        sim.Tick();
        int digMarks = sim.Designations.All.Count(m => m.Mark == DesignationMark.Dig);
        int trees = sim.Plants.All.Count(p => p.MarkedForChop);
        Assert.InRange(digMarks, 60, ScreenshotScripts.PitLength * ScreenshotScripts.PitWidth * ScreenshotScripts.PitDepth);
        Assert.InRange(trees, 3, 150);

        sim.RunTicks(1199);
        Assert.True(sim.Counters.JobsCompleted > 10, $"jobs completed: {sim.Counters.JobsCompleted}");
        Assert.True(sim.Designations.Count > 0 || sim.Plants.All.Any(p => p.MarkedForChop),
            "some work is still left at tick 1200, so the shots show colonists busy");
    }
}
