using System.Numerics;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Tools;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.PumpScenarioTests;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M5-T6: shared world. Stone ground up to y = 4; the hub at (2, 5, 2) (x/z 2..4, y 5..6).</summary>
internal static class BuildWorld
{
    public static readonly Int3 HubOrigin = new(2, 5, 2);

    public static ScenarioBuilder Flat(int logs = 40) => new ScenarioBuilder().Ground(4).Hub(HubOrigin).Stock("log", logs);

    public static PickHit Top(int x, int y, int z) => new(new Int3(x, y, z), Int3.Up);

    public static Building Only(Simulation sim, string def) => sim.Buildings.All.Single(b => b.Def.Id == def);
}

/// <summary>M5-T6: build tool with ghost and reasons (VIEW-12, VIEW-14; ADR-044).</summary>
[Trait("Category", "Unit")]
public class BuildToolTests
{
    private static PickHit Top(int x, int y, int z) => BuildWorld.Top(x, y, z);

    [Theory]
    [InlineData('B', ToolKind.Build)]
    [InlineData('X', ToolKind.Deconstruct)]
    public void Hotkeys_SelectClickTools(char key, ToolKind tool) => Assert.Equal(tool, ToolController.ForHotkey(key));

    [Fact]
    public void ClickTools_NeverStartADrag()
    {
        var t = new ToolController();
        t.SetTool(ToolKind.Build);
        t.Press(Top(5, 4, 5));
        Assert.False(t.Dragging);
        t.SetTool(ToolKind.Deconstruct);
        t.Press(Top(5, 4, 5));
        Assert.False(t.Dragging);
        Assert.Null(t.Release(Top(5, 4, 5), 31));
    }

    [Fact]
    public void Buildable_LeavesOutTheHub_BCycles_RRotates()
    {
        var b = new BuildTool(TestContent.Db);
        Assert.Equal(new[] { "warehouse", "pump", "levee", "sawmill", "stonecutter" }, b.Buildable.Select(d => d.Id));   // M11-T4: workshops
        Assert.Equal("warehouse", b.Def.Id);
        b.Cycle(); Assert.Equal("pump", b.Def.Id);
        b.Cycle(); b.Cycle(); Assert.Equal("sawmill", b.Def.Id);
        b.Cycle(); b.Cycle(); Assert.Equal("warehouse", b.Def.Id);
        b.Select("levee"); Assert.Equal("levee", b.Def.Id);
        b.Select("hub"); Assert.Equal("levee", b.Def.Id);   // not buildable: ignored
        Assert.Equal(0, b.Rotation);
        foreach (var expected in new[] { 90, 180, 270, 0 }) { b.Rotate(); Assert.Equal(expected, b.Rotation); }
    }

    [Fact]
    public void Ghost_StandsOnThePickedBlock_GreenOnFreeGround()
    {
        var sim = BuildWorld.Flat().Build();
        var b = new BuildTool(TestContent.Db);
        var g = b.Ghost(sim, Top(10, 4, 10))!.Value;
        Assert.Equal(new Int3(10, 5, 10), g.Origin);
        Assert.Equal((new Int3(10, 5, 10), new Int3(11, 6, 11)), (g.Min, g.Max));
        Assert.Equal(new Int3(10, 5, 9), g.Entrance);
        Assert.True(g.Ok);
        Assert.Null(g.Reason);
        Assert.Contains("Warehouse (20 log)", BuildTool.Tooltip(sim, g));

        // Rotated 90 degrees the footprint turns about the origin (BLD-01): the box is min-first all the same.
        b.Rotate();
        var r = b.Ghost(sim, Top(10, 4, 10))!.Value;
        Assert.Equal(BuildingShape.Entrance(r.Def, r.Origin, 90), r.Entrance);
        Assert.Equal(2, r.Max.X - r.Min.X + 1);
        Assert.Equal(2, r.Max.Z - r.Min.Z + 1);
        Assert.Null(b.Ghost(sim, null));
    }

    [Fact]
    public void Ghost_RedWithReason_ClickSendsNothing()
    {
        var sim = BuildWorld.Flat().Build();
        var b = new BuildTool(TestContent.Db);
        var onHub = Top(3, 6, 3);   // the hub's roof: not ground for a warehouse
        var g = b.Ghost(sim, onHub)!.Value;
        Assert.False(g.Ok);
        Assert.Equal(PlacementResult.NotOnGround, g.Result);
        Assert.Equal("Needs solid ground under it", g.Reason);
        Assert.Contains("Needs solid ground under it", BuildTool.Tooltip(sim, g));

        var click = b.Press(sim, onHub, shift: false);
        Assert.Null(click.Command);
        Assert.True(click.KeepTool);
        Assert.Equal("Can't build Warehouse: Needs solid ground under it", click.Message);

        b.Select("pump");   // a pump on dry flat ground
        Assert.Equal("Must stand on a bank with water in front", b.Ghost(sim, Top(20, 4, 20))!.Value.Reason);
    }

    [Fact]
    public void Click_SendsPlaceBuilding_ThenBackToSelect_BlueprintAppears()
    {
        var sim = BuildWorld.Flat().Build();
        var b = new BuildTool(TestContent.Db);
        var click = b.Press(sim, Top(10, 4, 10), shift: false);
        Assert.Equal(new PlaceBuilding("warehouse", new Int3(10, 5, 10), 0), click.Command);
        Assert.False(click.KeepTool);
        Assert.Null(click.Message);
        b.Release();

        sim.Enqueue(click.Command!);
        sim.Tick();
        var w = BuildWorld.Only(sim, "warehouse");
        Assert.Equal(BuildingState.Blueprint, w.State);
        // The same spot is now red (the sim's CanPlace sees the blueprint).
        Assert.Equal(PlacementResult.Overlaps, b.Ghost(sim, Top(10, 4, 10))!.Value.Result);
    }

    [Fact]
    public void ShiftDrag_PlacesALeveeLine_OncePerCell()
    {
        var sim = BuildWorld.Flat().Build();
        var b = new BuildTool(TestContent.Db);
        b.Select("levee");
        var commands = new List<ICommand>();
        var first = b.Press(sim, Top(10, 4, 20), shift: true);
        Assert.True(first.KeepTool);
        commands.Add(first.Command!);
        foreach (var x in new[] { 10, 11, 11, 12, 13 })
            if (b.Drag(sim, Top(x, 4, 20), shift: true) is { } c) commands.Add(c);
        Assert.Null(b.Drag(sim, Top(14, 4, 20), shift: false));   // Shift released: no more
        b.Release();
        Assert.Null(b.Drag(sim, Top(15, 4, 20), shift: true));    // button up: no more

        Assert.Equal(new[] { 10, 11, 12, 13 }, commands.Cast<PlaceBuilding>().Select(p => p.Origin.X));
        foreach (var c in commands) sim.Enqueue(c);
        sim.Tick();
        Assert.Equal(4, sim.Buildings.All.Count(x => x.Def.Id == "levee"));
    }

    [Theory]
    [InlineData(PlacementResult.OutOfBounds, "Outside the map")]
    [InlineData(PlacementResult.Overlaps, "Overlaps another building or its entrance")]
    [InlineData(PlacementResult.FootprintBlocked, "Something is in the way")]
    [InlineData(PlacementResult.EntranceBlocked, "The entrance is blocked")]
    public void ReasonText_IsPlayerFacing(PlacementResult r, string text) => Assert.Equal(text, BuildTool.ReasonText(r));
}

/// <summary>M5-T6: deconstruct tool (VIEW-12, BLD-09).</summary>
[Trait("Category", "Unit")]
public class DeconstructToolTests
{
    private static PickHit Top(int x, int y, int z) => BuildWorld.Top(x, y, z);

    [Fact]
    public void Hub_IsRefused_WithAReason()
    {
        var sim = BuildWorld.Flat().Build();
        var hub = DeconstructTool.Target(sim, Top(3, 6, 3))!;
        Assert.Equal("hub", hub.Def.Id);
        Assert.Equal("The Great Hall cannot be torn down", DeconstructTool.Tooltip(sim, hub));
        var (cmd, msg) = DeconstructTool.Click(sim, Top(3, 6, 3));
        Assert.Null(cmd);
        Assert.Equal("The Great Hall cannot be torn down", msg);
        Assert.Equal((null, null), DeconstructTool.Click(sim, Top(20, 4, 20)));   // bare ground
        Assert.Null(DeconstructTool.Target(sim, null));
    }

    [Fact]
    public void Blueprint_FoundFromTheGroundUnderIt_ClickCancelsIt()
    {
        var sim = BuildWorld.Flat().Build();
        sim.Enqueue(new PlaceBuilding("warehouse", new Int3(10, 5, 10), 0));
        sim.Tick();
        var w = BuildWorld.Only(sim, "warehouse");
        Assert.Same(w, DeconstructTool.Target(sim, Top(11, 4, 11)));
        Assert.StartsWith("Cancel Warehouse", DeconstructTool.Tooltip(sim, w));
        var (cmd, msg) = DeconstructTool.Click(sim, Top(11, 4, 11));
        Assert.Null(msg);
        Assert.Equal(new Deconstruct(w.Id), cmd);
        sim.Enqueue(cmd!);
        sim.Tick();
        Assert.DoesNotContain(sim.Buildings.All, b => b.Def.Id == "warehouse");
    }

    [Fact]
    public void Complete_FoundFromItsBlocks_ClickStartsTeardown()
    {
        var sim = BuildWorld.Flat().Storage("warehouse", new Int3(10, 5, 10)).Build();
        var w = BuildWorld.Only(sim, "warehouse");
        Assert.Same(w, DeconstructTool.Target(sim, Top(11, 6, 11)));
        Assert.StartsWith("Tear down Warehouse", DeconstructTool.Tooltip(sim, w));
        sim.Enqueue(DeconstructTool.Click(sim, Top(11, 6, 11)).Command!);
        sim.Tick();
        Assert.Equal(BuildingState.Deconstructing, w.State);
        Assert.Equal("Already being torn down", DeconstructTool.Refusal(sim, w));
    }
}

/// <summary>M5-T6: building render data (VIEW-09).</summary>
[Trait("Category", "Unit")]
public class BuildingVisualTests
{
    private static readonly EntityColors Colors = new(TestContent.Db);
    private const float I = BuildingVisuals.Inflate;

    private static BuildingVisual Of(Simulation sim, string def, int slice = 31) =>
        BuildingVisuals.Build(sim, slice, Colors).Single(v => v.Id == BuildWorld.Only(sim, def).Id);

    [Fact]
    public void Hub_IsASolidFootprintBox_InItsPaletteColor()
    {
        var sim = BuildWorld.Flat().Build();
        var v = Of(sim, "hub");
        Assert.Equal(BuildingLook.Complete, v.Look);
        Assert.Equal(BlockColors.ParseHex("#c28b4a"), v.Color);
        Assert.Equal(new Vector3(2 - I, 5 - I, 2 - I), v.Min);
        Assert.Equal(new Vector3(3 + 2 * I, 2 + 2 * I, 3 + 2 * I), v.Size);
        Assert.Equal("", v.Label);
        Assert.Equal(-1f, v.Progress);
        Assert.False(v.NoWater);
    }

    [Fact]
    public void Slice_CutsTheBox_AndHidesBuildingsAboveIt()
    {
        var sim = BuildWorld.Flat().Build();
        Assert.Equal(1 + 2 * I, Of(sim, "hub", slice: 5).Size.Y);
        Assert.Empty(BuildingVisuals.Build(sim, 4, Colors));
    }

    [Fact]
    public void Blueprint_Site_Complete_Teardown()
    {
        var sim = BuildWorld.Flat().Build();
        sim.Enqueue(new PlaceBuilding("warehouse", new Int3(10, 5, 10), 0));
        sim.Tick();
        var w = BuildWorld.Only(sim, "warehouse");
        int log = TestContent.Db.Item("log").Value;

        var v = Of(sim, "warehouse");
        Assert.Equal(BuildingLook.Blueprint, v.Look);
        Assert.Equal(Colors.Blueprint with { W = BuildingVisuals.BlueprintAlpha }, v.Color);
        Assert.Equal("Warehouse: log 0/20", v.Label);
        Assert.Equal(0f, v.Progress);

        w.State = BuildingState.UnderConstruction;
        w.Delivered[log] = 5;
        v = Of(sim, "warehouse");
        Assert.Equal(BuildingLook.Site, v.Look);
        Assert.Equal(BlockColors.ParseHex("#a07850") with { W = BuildingVisuals.SiteAlpha }, v.Color);
        Assert.Equal("Warehouse: log 5/20", v.Label);
        Assert.Equal(0.25f, v.Progress);

        w.Delivered[log] = 20;
        w.Progress = 150;
        v = Of(sim, "warehouse");
        Assert.Equal("Building Warehouse 50%", v.Label);
        Assert.Equal(0.5f, v.Progress);

        w.State = BuildingState.Complete;
        v = Of(sim, "warehouse");
        Assert.Equal(BlockColors.ParseHex("#a07850"), v.Color);
        Assert.Equal(-1f, v.Progress);

        w.State = BuildingState.Deconstructing;
        w.Progress = 75;
        v = Of(sim, "warehouse");
        Assert.Equal(BuildingLook.Deconstructing, v.Look);
        Assert.Equal("Tearing down 50%", v.Label);
        // Label centered over the box top (x/z 10..11 -> 11), LabelLift above it.
        Assert.True(Vector3.Distance(new Vector3(11f, 7f + I + BuildingVisuals.LabelLift, 11f), v.LabelAnchor) < 1e-4f);
    }

    [Fact]
    public void DryPump_ShowsNoWater()
    {
        var sim = BasinWorld(0, agents: 0).Storage("pump", PumpOrigin).Build();
        sim.Tick();
        var v = Of(sim, "pump");
        Assert.True(v.NoWater);
        Assert.Equal(BlockColors.ParseHex("#4f7fa0"), v.Color);
        Assert.Contains(TopBarModel.PumpDry, TopBarModel.Build(sim, 1).Alerts);
    }
}

/// <summary>M5-T6: HUD top bar (VIEW-15) and colony lost (VIEW-18).</summary>
[Trait("Category", "Unit")]
public class TopBarTests
{
    [Fact]
    public void DaySpeedAndTotals()
    {
        var sim = BuildWorld.Flat(logs: 40).Stock("berries", 7).Storage("warehouse", new Int3(10, 5, 10)).Stock("log", 5).Build();
        var bar = TopBarModel.Build(sim, 3);
        Assert.Equal("Day 1", bar.DayText);
        Assert.Equal("3x", bar.Speed);
        Assert.Equal("Paused", TopBarModel.SpeedText(0));
        Assert.Equal(new[] { new ItemTotal("Log", 45), new ItemTotal("Stone", 0), new ItemTotal("Berries", 7),
            new ItemTotal("Potato", 0), new ItemTotal("Water", 0), new ItemTotal("Planks", 0), new ItemTotal("Cut stone", 0) }, bar.Totals);
        Assert.Equal("Log 45   Stone 0   Berries 7   Potato 0   Water 0   Planks 0   Cut stone 0", bar.TotalsText);
        Assert.Empty(bar.Alerts);
        Assert.False(bar.ColonyLost);

        sim.Clock.Tick = 3 * SimClock.TicksPerDay + 5;
        Assert.Equal(4, TopBarModel.Build(sim, 1).Day);

        // A blueprint warehouse stores nothing yet: only complete storage counts.
        BuildWorld.Only(sim, "warehouse").State = BuildingState.Deconstructing;
        Assert.Equal(40, TopBarModel.Build(sim, 1).Totals[0].Count);
    }

    [Fact]
    public void Alerts_NoFoodNoWater_ThenColonyLost()
    {
        var sim = BuildWorld.Flat().Agent(new Int3(8, 5, 8)).Build();
        var a = sim.Agents.All.Single();
        a.Hunger = 100;
        a.Thirst = 100;
        Assert.Equal(new[] { TopBarModel.NoFood, TopBarModel.NoWater }, TopBarModel.Build(sim, 1).Alerts);

        a.Thirst = 0;
        a.Health = 1;
        sim.Tick();
        Assert.False(a.IsAlive);
        Assert.True(TopBarModel.Build(sim, 1).ColonyLost);
    }
}
