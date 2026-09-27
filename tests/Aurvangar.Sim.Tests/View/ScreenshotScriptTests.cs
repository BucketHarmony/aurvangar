using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Events;
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

    /// <summary>M5-T6: the build script places a warehouse, a pump on a river bank and a levee line near the hub, all
    /// accepted.</summary>
    [Fact]
    public void Build_Seed1_PlacesWarehousePumpAndLevees_AllAccepted()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var commands = ScreenshotScripts.For("build", sim);
        Assert.IsType<DesignateChop>(commands[0]);
        var places = commands.OfType<PlaceBuilding>().ToList();
        Assert.Equal(new[] { "warehouse", "pump" }, places.Take(2).Select(p => p.DefId));
        Assert.Equal(ScreenshotScripts.LeveeCount, places.Count(p => p.DefId == "levee"));
        foreach (var c in commands) sim.Enqueue(c);
        sim.Tick();
        Assert.DoesNotContain(sim.Events.Drain(), e => e is CommandRejected);
        Assert.Equal(1 + places.Count, sim.Buildings.All.Count());

        // The harness shows a red warehouse ghost on the hub roof.
        var pick = ScreenshotScripts.GhostPick("build", sim)!.Value;
        Assert.Equal(PlacementResult.NotOnGround, new Aurvangar.ViewCore.Tools.BuildTool(TestContent.Db).Ghost(sim, pick)!.Value.Result);
        Assert.Null(ScreenshotScripts.GhostPick("digchop", sim));

        // Part way (TICKS=500) the shots show several building states; by tick 1600 everything is built. (It was 1200
        // before M6-T3: the dwarves first pick berries (40 < 60 food), which delays the chops that supply logs; ADR-048.)
        sim.RunTicks(499);
        var states = sim.Buildings.All.Where(b => b.Def.Id != "hub").Select(b => b.State).Distinct().ToList();
        Assert.True(states.Count >= 2, $"states at tick 500: {string.Join(",", states)}");
        sim.RunTicks(1100);
        Assert.All(sim.Buildings.All, b => Assert.Equal(BuildingState.Complete, b.State));
        // M7-T2: the pump stands on a wet bank site with no dug notch (its worker stands one level up), so it runs.
        var pump = sim.Buildings.All.Single(b => b.Def.Id == "pump");
        Assert.False(pump.NoWater, "the build-script pump has no water");
        Assert.Equal(pump.EntranceCell + Aurvangar.Sim.Core.Int3.Up, Construction.StandCell(sim, pump));
    }
}
