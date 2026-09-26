using System.Numerics;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Farming;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.Tests.Scenarios;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Screenshots;
using Aurvangar.ViewCore.Tools;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M6-T5: farm tool (VIEW-12), farm overlay and crops (VIEW-11), ripe bushes, season HUD (VIEW-15, ECO-18).</summary>
[Trait("Category", "Unit")]
public class FarmToolTests
{
    private static PickHit Top(int x, int y, int z) => new(new Int3(x, y, z), Int3.Up);

    [Fact]
    public void F_SelectsFarm_AndFarmIsADragTool()
    {
        Assert.Equal(ToolKind.Farm, ToolController.ForHotkey('F'));
        Assert.Equal(ToolKind.Farm, ToolController.ForHotkey('f'));
        Assert.True(ToolController.IsDragTool(ToolKind.Farm));
    }

    [Fact]
    public void FarmDrag_SendsDesignateFarmForTheXZRectangle()
    {
        var t = new ToolController();
        t.SetTool(ToolKind.Farm);
        t.Press(Top(10, 20, 4));
        t.Move(Top(6, 21, 9));
        Assert.Equal((new Int3(6, 20, 4), new Int3(10, 21, 9)), t.PreviewBox(63));
        Assert.Equal(new DesignateFarm(10, 4, 6, 9), t.Release(null, 63));
        Assert.Equal(ToolKind.Farm, t.Tool);   // stays active like the other drag tools
    }

    /// <summary>The farm tool's mouse label says whether crops can grow there (ECO-12: only on moist tiles).</summary>
    [Fact]
    public void Tooltip_SaysMoistOrDry_AndCountsMoistColumnsInADrag()
    {
        var sim = FarmScenarioTests.FarmWorld();
        sim.Tick();   // moisture is computed at tick 0 (ECO-15)
        var moist = FarmScenarioTests.MoistTile;
        var dry = FarmScenarioTests.DryTile;
        Assert.Equal(FarmTool.MoistText, FarmTool.Tooltip(sim, Top(moist.X, moist.Y, moist.Z), null));
        Assert.Equal(FarmTool.DryText, FarmTool.Tooltip(sim, Top(dry.X, dry.Y, dry.Z), null));
        Assert.Null(FarmTool.Tooltip(sim, null, null));

        var box = (new Int3(moist.X, 4, moist.Z), new Int3(moist.X + 1, 4, moist.Z));
        Assert.Equal("2 of 2 columns moist", FarmTool.Tooltip(sim, null, box));
        var wide = (new Int3(moist.X, 4, moist.Z), new Int3(dry.X, 4, moist.Z));
        int total = dry.X - moist.X + 1;
        int wet = Enumerable.Range(moist.X, total).Count(x => sim.Moisture.IsMoist(x, moist.Z));
        Assert.True(wet < total);
        Assert.Equal($"{wet} of {total} columns moist", FarmTool.Tooltip(sim, null, wide));
    }
}

[Trait("Category", "Unit")]
public class CropViewTests
{
    private static readonly PlantColors Colors = new(TestContent.Db);

    private static (Simulation Sim, FarmTile Tile) OneTile(Int3 cell)
    {
        var sim = FarmScenarioTests.FarmWorld();
        sim.Enqueue(new DesignateFarm(cell.X, cell.Z, cell.X, cell.Z));
        sim.Tick();
        return (sim, sim.Farms.Get(cell)!);
    }

    [Fact]
    public void EmptyTile_HasNoCrop_GrowingCropRisesWithProgress_MatureShowsPotatoes()
    {
        var (sim, tile) = OneTile(FarmScenarioTests.MoistTile);
        Assert.Equal(CropState.Empty, tile.State);
        Assert.True(CropMesher.Build(sim, 63, Colors).IsEmpty);

        tile.State = CropState.Growing;
        tile.Progress = 0;
        var young = CropMesher.Build(sim, 63, Colors);
        Assert.False(young.IsEmpty);
        float youngTop = young.Positions.Max(p => p.Y);

        tile.Progress = FarmSystem.MatureTicks - 1;
        float oldTop = CropMesher.Build(sim, 63, Colors).Positions.Max(p => p.Y);
        Assert.True(oldTop > youngTop, $"{oldTop} > {youngTop}");
        Assert.True(oldTop <= tile.Cell.Y + 1 + CropMesher.MaxHeight + 1e-4f);
        Assert.DoesNotContain(Colors.Potato, CropMesher.Build(sim, 63, Colors).Colors);

        tile.State = CropState.Mature;
        tile.Progress = FarmSystem.MatureTicks;
        var mature = CropMesher.Build(sim, 63, Colors);
        Assert.Contains(Colors.Potato, mature.Colors);
        Assert.True(mature.Positions.Max(p => p.Y) >= oldTop);
        // Everything stands on the tile's top face, inside its cell.
        Assert.All(mature.Positions, p =>
        {
            Assert.InRange(p.X, tile.Cell.X, tile.Cell.X + 1);
            Assert.InRange(p.Z, tile.Cell.Z, tile.Cell.Z + 1);
            Assert.True(p.Y >= tile.Cell.Y + 1 - 1e-4f);
        });
    }

    /// <summary>A growing crop on a dry tile (it will wither, ECO-13) is drawn in the dry color.</summary>
    [Fact]
    public void DryGrowingCrop_UsesDryColor()
    {
        var (sim, tile) = OneTile(FarmScenarioTests.DryTile);
        Assert.False(sim.Moisture.IsMoist(tile.Cell.X, tile.Cell.Z));
        tile.State = CropState.Growing;
        tile.Progress = 100;
        var colors = CropMesher.Build(sim, 63, Colors).Colors;
        Assert.Contains(Colors.CropDry, colors);
        Assert.DoesNotContain(Colors.Crop, colors);

        var (wet, wetTile) = OneTile(FarmScenarioTests.MoistTile);
        wetTile.State = CropState.Growing;
        Assert.Contains(Colors.Crop, CropMesher.Build(wet, 63, Colors).Colors);
    }

    [Fact]
    public void CropAboveSlice_IsHidden()
    {
        var (sim, tile) = OneTile(FarmScenarioTests.MoistTile);
        tile.State = CropState.Mature;
        Assert.True(CropMesher.Build(sim, tile.Cell.Y - 1, Colors).IsEmpty);
        Assert.False(CropMesher.Build(sim, tile.Cell.Y, Colors).IsEmpty);
    }

    /// <summary>There is no farm event, so the renderer polls a signature: it changes with the growth stage, the state
    /// and the moisture, not with every tick of progress.</summary>
    [Fact]
    public void Signature_ChangesPerStageStateAndMoisture_NotEveryTick()
    {
        var (sim, tile) = OneTile(FarmScenarioTests.MoistTile);
        ulong empty = CropMesher.Signature(sim);
        tile.State = CropState.Growing;
        tile.Progress = 0;
        ulong growing = CropMesher.Signature(sim);
        Assert.NotEqual(empty, growing);
        tile.Progress = 1;
        Assert.Equal(growing, CropMesher.Signature(sim));
        tile.Progress = FarmSystem.MatureTicks / 2;
        ulong half = CropMesher.Signature(sim);
        Assert.NotEqual(growing, half);
        tile.State = CropState.Mature;
        Assert.NotEqual(half, CropMesher.Signature(sim));
        Assert.Equal(0, CropMesher.Stage(new FarmTile { State = CropState.Growing, Progress = 0 }));
        Assert.Equal(CropMesher.Stages, CropMesher.Stage(new FarmTile { State = CropState.Mature, Progress = FarmSystem.MatureTicks }));

        // Moisture: dry the pit and let the map recompute.
        ulong before = CropMesher.Signature(sim);
        sim.Water.SetLevel(FarmScenarioTests.Pit, 0);
        sim.RunTicks(MoistureMap.Interval);
        Assert.False(sim.Moisture.IsMoist(tile.Cell.X, tile.Cell.Z));
        Assert.NotEqual(before, CropMesher.Signature(sim));
    }

    [Fact]
    public void HoverLabel_NamesTheCropState()
    {
        var (sim, tile) = OneTile(FarmScenarioTests.MoistTile);
        var hit = new PickHit(tile.Cell, Int3.Up);
        Assert.Equal("Farm tile: waiting for planting", CropMesher.Label(sim, hit));
        tile.State = CropState.Growing;
        tile.Progress = FarmSystem.MatureTicks / 4;
        Assert.Equal("Potatoes 25% (moist)", CropMesher.Label(sim, hit));
        tile.State = CropState.Mature;
        Assert.Equal("Potatoes ready to harvest", CropMesher.Label(sim, hit));
        Assert.Null(CropMesher.Label(sim, new PickHit(tile.Cell + new Int3(1, 0, 0), Int3.Up)));

        var (dry, dryTile) = OneTile(FarmScenarioTests.DryTile);
        dryTile.State = CropState.Growing;
        dryTile.DryTicks = FarmSystem.WitherTicks - 600;
        Assert.Equal("Potatoes 0% (dry, withers in 600 ticks)", CropMesher.Label(dry, new PickHit(dryTile.Cell, Int3.Up)));
    }

    /// <summary>VIEW-11 "farm = brown overlay": every farm tile gets furrows in `designations.farm`, and the overlay's
    /// signature covers the tiles.</summary>
    [Fact]
    public void FarmTiles_GetABrownOverlay()
    {
        var colors = new EntityColors(TestContent.Db);
        var sim = FarmScenarioTests.FarmWorld();
        ulong none = DesignationMesher.Signature(sim);
        Assert.True(DesignationMesher.Build(sim, 63, colors).IsEmpty);
        var c = FarmScenarioTests.MoistTile;
        sim.Enqueue(new DesignateFarm(c.X, c.Z, c.X + 1, c.Z));
        sim.Tick();
        Assert.NotEqual(none, DesignationMesher.Signature(sim));
        var mesh = DesignationMesher.Build(sim, 63, colors);
        Assert.Equal(2 * DesignationMesher.FurrowsPerTile * 6, mesh.QuadCount);
        Assert.All(mesh.Colors, col => Assert.Equal(colors.Farm with { W = DesignationMesher.FurrowAlpha }, col));
        Assert.True(DesignationMesher.Build(sim, c.Y - 1, colors).IsEmpty);
    }
}

[Trait("Category", "Unit")]
public class BushViewTests
{
    private static readonly PlantColors Colors = new(TestContent.Db);

    [Fact]
    public void RipeBush_ShowsBerries_HarvestedBushDoesNot_AndSignatureChanges()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var bush = sim.Plants.AddBush(new Int3(8, 5, 8));
        ulong ripe = PlantMesher.Signature(sim.Plants);
        var mesh = PlantMesher.Build(sim.Plants, 63, Colors);
        Assert.Contains(Colors.Berries, mesh.Colors);
        Assert.Equal(PlantMesher.ConeQuads + PlantMesher.BerryCount * 6, mesh.QuadCount);

        PlantSystem.Harvest(bush);
        Assert.NotEqual(ripe, PlantMesher.Signature(sim.Plants));
        var bare = PlantMesher.Build(sim.Plants, 63, Colors);
        Assert.DoesNotContain(Colors.Berries, bare.Colors);
        Assert.Equal(PlantMesher.ConeQuads, bare.QuadCount);
    }
}

[Trait("Category", "Unit")]
public class SeasonHudTests
{
    [Theory]
    [InlineData(0L, Season.Wet, 5, "Wet season, 5 days left")]
    [InlineData(4L * SimClock.TicksPerDay + 1, Season.Wet, 1, "Wet season, 1 day left")]
    [InlineData(5L * SimClock.TicksPerDay, Season.Drought, 2, "Drought, 2 days left")]
    [InlineData(7L * SimClock.TicksPerDay - 1, Season.Drought, 1, "Drought, 1 day left")]
    [InlineData(7L * SimClock.TicksPerDay, Season.Wet, 5, "Wet season, 5 days left")]
    public void TopBar_ShowsSeasonAndDaysLeft(long tick, Season season, int days, string text)
    {
        var sim = BuildWorld.Flat().Build();
        sim.Clock.Tick = tick;
        var bar = TopBarModel.Build(sim, 1);
        Assert.Equal(season, bar.Season);
        Assert.Equal(days, bar.DaysLeft);
        Assert.Equal(text, bar.SeasonText);
    }

    [Fact]
    public void SeasonChange_HasAMessage()
    {
        Assert.Contains("Drought", TopBarModel.SeasonMessage(Season.Drought));
        Assert.Contains("Wet", TopBarModel.SeasonMessage(Season.Wet));
    }
}

/// <summary>M6-T5: the screenshot harness can show a farm (`SCRIPT=farm`, preset `farm`).</summary>
[Trait("Category", "Unit")]
public class FarmScreenshotTests
{
    [Fact]
    public void FarmScript_Seed1_DesignatesAMoistFieldNearTheHub_AndCropsGrow()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var commands = ScreenshotScripts.For("farm", sim);
        var farm = Assert.Single(commands.OfType<DesignateFarm>());
        Assert.Equal(ScreenshotScripts.FarmSize - 1, Math.Abs(farm.X1 - farm.X0));
        Assert.Equal(ScreenshotScripts.FarmSize - 1, Math.Abs(farm.Z1 - farm.Z0));
        foreach (var c in commands) sim.Enqueue(c);
        sim.Tick();
        Assert.Equal(ScreenshotScripts.FarmSize * ScreenshotScripts.FarmSize, sim.Farms.Count);
        Assert.All(sim.Farms.All, t => Assert.True(sim.Moisture.IsMoist(t.Cell.X, t.Cell.Z), $"{t.Cell} moist"));

        var shot = ScreenshotPresets.For("farm", sim);
        var mid = sim.Farms.All.Aggregate(Vector2.Zero, (s, t) => s + new Vector2(t.Cell.X + 0.5f, t.Cell.Z + 0.5f)) / sim.Farms.Count;
        Assert.Equal(mid.X, shot.Focus.X, 2);
        Assert.Equal(mid.Y, shot.Focus.Z, 2);

        // TICKS=4000 shows growing crops; by 9000 (before the day-5 drought) the first harvests are in.
        sim.RunTicks(3999);
        Assert.Contains(sim.Farms.All, t => t.State == CropState.Growing && t.Progress > 0);
        sim.RunTicks(5000);
        Assert.True(sim.Farms.All.Any(t => t.State == CropState.Mature) || TopBarModel.Totals(sim).Single(i => i.Name == "Potato").Count > 0);
    }

    [Fact]
    public void FarmPreset_WithoutFarms_FallsBackToTheHub()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var hub = ScreenshotPresets.HubFocus(sim);
        var shot = ScreenshotPresets.For("farm", sim);
        Assert.Equal(hub.X, shot.Focus.X, 2);
        Assert.Equal(hub.Z, shot.Focus.Z, 2);
    }
}
