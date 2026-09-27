using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Tools;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M11-T1 (G6: "Not sure why levees have doors"): a building with no workers and no storage (the levee) has
/// no entrance in data (BLD-02, ADR-076). Its builders stand on any standable cell in reach of the footprint: beside
/// it, or one level up or down (as for stacked levees). Worlds are flat stone to y = 4, so agents stand on y = 5.</summary>
[Trait("Category", "Scenario")]
public class LeveeStandScenarioTests
{
    private static readonly Int3 Notch = new(10, G, 10);

    /// <summary>A notch in a two-high stone block (x 9..11, z 9..11, y 5..6): the levee cell <see cref="Notch"/> and the
    /// cell south of it (10,5,11) are open, so the only standable cell in reach of the notch is (10,5,11), reached
    /// from the open ground to the south. Hub with logs and two agents.</summary>
    private static Simulation NotchWorld() =>
        new ScenarioBuilder().Ground(G - 1)
            .FillBox(new Int3(9, G, 9), new Int3(11, G + 1, 11), BlockId.Stone)
            .FillBox(new Int3(10, G, 10), new Int3(10, G + 1, 11), BlockId.Air)
            .Hub(new Int3(20, G, 20)).Stock("log", 20)
            .Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15)).Build();

    [Fact]
    public void Levee_HasNoEntrance_InDataOrGhost()
    {
        var levee = TestContent.Db.Building("levee");
        Assert.Null(levee.Entrance);
        Assert.False(levee.HasEntrance);
        foreach (var id in new[] { "hub", "warehouse", "pump" }) Assert.True(TestContent.Db.Building(id).HasEntrance, id);

        var sim = new ScenarioBuilder().Ground(G - 1).Hub(new Int3(20, G, 20)).Build();
        var g = BuildTool.GhostAt(sim, levee, new Int3(10, G, 10), 0);
        Assert.True(g.Ok);
        Assert.Null(g.Entrance);
        Assert.Equal(new Int3(10, G, 9), BuildTool.GhostAt(sim, TestContent.Db.Building("warehouse"), new Int3(10, G, 10), 0).Entrance);

        // Nothing around a levee is reserved: a block or another building may take the cell its entrance used to be.
        Assert.Equal(PlacementResult.Ok, sim.Buildings.TryPlaceBlueprint(levee, new Int3(10, G, 10), 0, out var b));
        Assert.False(sim.Buildings.IsEntranceOrStand(new Int3(10, G, 9)));
        Assert.False(sim.Buildings.IsEntranceOrStand(new Int3(10, G - 1, 9)));
        Assert.Equal(PlacementResult.Ok, sim.Buildings.CanPlace(TestContent.Db.Building("warehouse"), new Int3(10, G, 8), 0));
        Assert.Equal(b!.Origin, b.JobCell);
    }

    /// <summary>Only the south side of the notch can be reached. The levee is placed at rotation 0 (whose old
    /// entrance, north, is stone) and built from the south cell; a second levee stacked on it builds from the same
    /// cell, one level down.</summary>
    [Fact]
    public void Levee_WithOnlyOneSideReachable_IsBuilt_AndStacks()
    {
        var sim = NotchWorld();
        var levee = TestContent.Db.Building("levee");
        foreach (var rot in new[] { 0, 90, 180, 270 })
            Assert.Equal(PlacementResult.Ok, sim.Buildings.CanPlace(levee, Notch, rot));
        Assert.Equal(new Int3(10, G, 11), sim.Buildings.PlannedStandCell(levee, Notch, 0));

        sim.Enqueue(new PlaceBuilding("levee", Notch, 0));
        sim.Enqueue(new PlaceBuilding("levee", Notch + Int3.Up, 0));
        sim.Tick();
        var line = sim.Buildings.All.Where(x => x.Def.Id == "levee").OrderBy(x => x.Origin.Y).ToList();
        Assert.Equal(2, line.Count);

        RunUntil(sim, () => line.All(l => l.State == BuildingState.Complete), 3000);
        Assert.Equal(BlockId.BuildingSolid, sim.World.GetBlock(Notch));
        Assert.Equal(BlockId.BuildingSolid, sim.World.GetBlock(Notch + Int3.Up));
        Assert.Equal(20 - 4, Stored(Hub(sim), "log"));
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.All(sim.Agents.All, a => Assert.True(a.IsAlive));

        // Deconstructed, the refund lands on the only stand cell (BLD-09).
        sim.Enqueue(new Deconstruct(line[1].Id));
        RunUntil(sim, () => sim.Buildings.Get(line[1].Id) is null, 1000);
        Assert.Equal(BlockId.Air, sim.World.GetBlock(Notch + Int3.Up));
    }

    /// <summary>With no standable cell in reach at all (the notch closed on the south too), a levee is refused with
    /// <see cref="PlacementResult.NoStandCell"/>; a third stacked levee on open ground has none either.</summary>
    [Fact]
    public void Levee_WithNoStandCellInReach_IsRefused()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(new Int3(9, G, 9), new Int3(11, G + 1, 11), BlockId.Stone)
            .FillBox(new Int3(10, G, 10), new Int3(10, G + 1, 10), BlockId.Air)
            .Hub(new Int3(20, G, 20)).Build();
        var levee = TestContent.Db.Building("levee");
        Assert.Equal(PlacementResult.NoStandCell, sim.Buildings.CanPlace(levee, Notch, 0));
        Assert.Equal("No room for a builder beside it", BuildTool.ReasonText(PlacementResult.NoStandCell));

        Assert.Equal(PlacementResult.Ok, sim.Buildings.TryPlaceBlueprint(levee, new Int3(5, G, 5), 0, out _));
        Assert.Equal(PlacementResult.Ok, sim.Buildings.TryPlaceBlueprint(levee, new Int3(5, G + 1, 5), 0, out _));
        Assert.Equal(PlacementResult.NoStandCell, sim.Buildings.CanPlace(levee, new Int3(5, G + 2, 5), 0));
    }

    /// <summary>The screenshot harness's <c>--ghost levee</c> (M11-T1): on seed 1 after the <c>build</c> script, the
    /// pick shows a green levee ghost next to the levee line, with no entrance tile.</summary>
    [Fact]
    public void ScreenshotGhost_Levee_IsGreen_WithNoEntranceTile()
    {
        var sim = Aurvangar.Sim.World.WorldFactory.Create(1, TestContent.Db);
        foreach (var c in Aurvangar.ViewCore.Screenshots.ScreenshotScripts.For("build", sim)) sim.Enqueue(c);
        sim.Tick();
        var levee = TestContent.Db.Building("levee");
        var pick = Aurvangar.ViewCore.Screenshots.ScreenshotScripts.GhostPickFor(sim, levee)!.Value;
        var tool = new BuildTool(TestContent.Db);
        tool.Select("levee");
        var g = tool.Ghost(sim, pick)!.Value;
        Assert.True(g.Ok, g.Reason);
        Assert.Null(g.Entrance);
        Assert.Equal("levee", Aurvangar.ViewCore.Screenshots.ScreenshotArgs.Parse(new[] { "--ghost", "levee" }).Ghost);
    }
}
