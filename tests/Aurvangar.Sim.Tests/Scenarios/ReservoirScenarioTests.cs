using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Farming;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M6-T8, DoD step 8 (last sentence): "Colonies that built a levee reservoir keep their pump running."
/// Stone to y = 4, a stone layer at y = 5 cut by a river (z 16..17, source at x = 1, drains at x = 30) and a side
/// pool (x 5..24, z 11..13) joined to the river by a 2-wide mouth (x 9..10, z 14..15). The pump stands on the bank at
/// (8,6,10) with its intake in the pool (the <see cref="PumpScenarioTests"/> layout); the Great Hall and the dwarves are
/// on the same side. Both runs follow the real weather (ECO-17): wet to tick 12,000, then a 2-day drought. In one run
/// the dwarves wall the mouth with two levees on day 1; in the other the pool stays open to the river.
/// M7-T3 (G3 answer 4) adds a field on dirt beside the pool: held water keeps it moist through the drought.</summary>
[Trait("Category", "Scenario")]
public class ReservoirScenarioTests
{
    private const int C = 5, Bank = 6;
    private const long Day = SimClock.TicksPerDay;
    private static readonly Int3 PumpOrigin = new(8, Bank, 10);
    private static readonly Int3 Intake = new(8, C, 11);
    private static readonly Int3[] Mouth = { new(9, C, 14), new(10, C, 14) };
    private static readonly Int3 RiverAtMouth = new(9, C, 16);
    /// <summary>M7-T3: a 5x4 field on dirt north of the pool (z 6..9), 2 to 5 columns from its water and 7 or more from
    /// the river, so only the pool can keep it moist (ECO-15: water at or above level 128 within 5 columns).</summary>
    private static readonly Int3 FieldMin = new(12, C, 6), FieldMax = new(16, C, 9);

    private static Simulation World()
    {
        var b = new ScenarioBuilder().Ground(C - 1);
        var rows = new string[32];
        for (int z = 0; z < 32; z++)
            rows[z] = new string(Enumerable.Range(0, 32).Select(x =>
                (z is 16 or 17 && x is >= 1 and <= 30)
                || (z is >= 11 and <= 13 && x is >= 5 and <= 24)
                || (z is 14 or 15 && x is 9 or 10) ? '.' : 'S').ToArray());
        b.Layer(C, rows);
        b.FillBox(FieldMin, FieldMax, BlockId.Dirt);
        for (int z = 11; z <= 17; z++)
            for (int x = 1; x <= 30; x++)
                if (rows[z][x] == '.') b.Water(new Int3(x, C, z), WaterGrid.Full);
        b.Source(new Int3(1, C, 16)).Source(new Int3(1, C, 17)).Drain(new Int3(30, C, 16)).Drain(new Int3(30, C, 17));
        return b.Hub(new Int3(20, Bank, 4)).Stock("log", 30).Stock("berries", 100).Stock("water", 20)
            .Agent(new Int3(10, Bank, 6)).Agent(new Int3(12, Bank, 6)).Build();
    }

    private sealed record Run(bool NoWaterInDrought, long PumpedInDrought, int RiverAtMouthMidDrought, int IntakeMidDrought,
        int MinIntakeInDrought, bool AllAlive, Field? Field);

    /// <summary>M7-T3: what happened to the field. A harvest is a Mature tile that became Empty, a wither a Growing one
    /// (ECO-13). Dry tile-ticks count every tick of the drought on which a tile's column was not moist.</summary>
    private sealed record Field(int Tiles, int HarvestsBeforeDrought, int WitheredInDrought, long DryTileTicksInDrought,
        int GrowingAtDroughtStart, int GrowingOrMatureAtDroughtEnd, int HarvestsInDrought, int DryTilesAtDroughtEnd);

    /// <summary>Counts crop transitions; call <see cref="Observe"/> after every tick.</summary>
    private sealed class FieldWatch
    {
        private readonly Dictionary<Int3, CropState> _last = new();
        public int Harvests, Withers;

        public void Observe(Simulation sim)
        {
            foreach (var t in sim.Farms.All)
            {
                if (_last.TryGetValue(t.Cell, out var before) && before != t.State && t.State == CropState.Empty)
                {
                    if (before == CropState.Mature) Harvests++;
                    else Withers++;
                }
                _last[t.Cell] = t.State;
            }
        }
    }

    private static Run Play(bool levees, bool field = false)
    {
        var sim = World();
        var watch = new FieldWatch();
        sim.Enqueue(new PlaceBuilding("pump", PumpOrigin, 0));
        if (field) sim.Enqueue(new DesignateFarm(FieldMin.X, FieldMin.Z, FieldMax.X, FieldMax.Z));
        sim.Tick();
        var pump = Site(sim, "pump");
        Assert.Equal(Intake, BuildingShape.Intake(pump.Def, pump.Origin, pump.Rotation));
        RunUntil(sim, () => pump.State == BuildingState.Complete, 3000);

        while (sim.Clock.Tick < Day) { sim.Tick(); watch.Observe(sim); }
        Assert.True(sim.Water.GetLevel(Intake) >= 256, $"pool not filled by the river: {sim.Water.GetLevel(Intake)}");
        if (levees)
        {
            foreach (var m in Mouth) sim.Enqueue(new PlaceBuilding("levee", m, 0));
            sim.Tick();
            var line = sim.Buildings.All.Where(x => x.Def.Id == "levee").ToList();
            Assert.Equal(2, line.Count);
            RunUntil(sim, () => line.All(l => l.State == BuildingState.Complete), 4000);
        }

        while (sim.Clock.Tick < 5 * Day) { sim.Tick(); watch.Observe(sim); }
        Assert.Equal(Season.Drought, WeatherSystem.SeasonAt(sim.Clock.Tick));
        int harvestsBefore = watch.Harvests, withersBefore = watch.Withers;
        int growingAtStart = sim.Farms.All.Count(t => t.State == CropState.Growing);
        long pumpedAtStart = sim.Water.Stats.Pumped, dryTileTicks = 0;
        bool noWater = false; int minIntake = int.MaxValue, riverMid = -1, intakeMid = -1;
        while (sim.Clock.Tick < 7 * Day)
        {
            sim.Tick();
            watch.Observe(sim);
            foreach (var t in sim.Farms.All)
                if (!sim.Moisture.IsMoist(t.Cell.X, t.Cell.Z)) dryTileTicks++;
            noWater |= pump.NoWater;
            minIntake = Math.Min(minIntake, sim.Water.GetLevel(Intake));
            if (sim.Clock.Tick == 6 * Day) { riverMid = sim.Water.GetLevel(RiverAtMouth); intakeMid = sim.Water.GetLevel(Intake); }
        }
        Field? f = field
            ? new Field(sim.Farms.Count, harvestsBefore, watch.Withers - withersBefore, dryTileTicks, growingAtStart,
                sim.Farms.All.Count(t => t.State is CropState.Growing or CropState.Mature),
                watch.Harvests - harvestsBefore, sim.Farms.All.Count(t => !sim.Moisture.IsMoist(t.Cell.X, t.Cell.Z)))
            : null;
        return new Run(noWater, sim.Water.Stats.Pumped - pumpedAtStart, riverMid, intakeMid, minIntake,
            sim.Agents.All.All(a => a.IsAlive), f);
    }

    /// <summary>With the levees the river beside the pool runs dry, but the pool keeps its water and the pump never
    /// flags NoWater through the whole drought, and keeps pumping.</summary>
    [Fact]
    public void LeveeReservoir_PumpRunsThroughDrought()
    {
        var r = Play(levees: true);
        Assert.Equal(0, r.RiverAtMouthMidDrought);
        Assert.True(r.MinIntakeInDrought >= 256, $"intake fell to {r.MinIntakeInDrought}");
        Assert.False(r.NoWaterInDrought, "the pump flagged NoWater during the drought");
        Assert.True(r.PumpedInDrought > 0, "nothing pumped during the drought");
        Assert.True(r.AllAlive);
    }

    /// <summary>Control: without the levees the pool drains out through the mouth with the river, and the pump runs dry
    /// (NoWater). The pool is still draining into the river bed mid-drought, so the bed at the mouth is not asserted.</summary>
    [Fact]
    public void OpenPool_PumpRunsDryInDrought()
    {
        var r = Play(levees: false);
        Assert.True(r.MinIntakeInDrought < 256, $"open pool still holds {r.MinIntakeInDrought} at the intake");
        Assert.True(r.NoWaterInDrought, "the pump flagged NoWater");
    }

    /// <summary>M7-T3, ECO-15 (G3 answer 4): player-held water keeps a field moist. The field beside the levee
    /// reservoir is harvested before the drought, is replanted, and keeps growing through the whole drought: every
    /// tile is moist on every drought tick, nothing withers, and crops keep maturing and are harvested in the drought.</summary>
    [Fact]
    public void FieldBesideLeveeReservoir_GrowsThroughDrought()
    {
        var r = Play(levees: true, field: true);
        var f = r.Field!;
        Assert.Equal(20, f.Tiles);
        Assert.True(f.HarvestsBeforeDrought > 0, "no harvest before the drought");
        Assert.True(f.GrowingAtDroughtStart > 0, "nothing growing when the drought began");
        Assert.Equal(0L, f.DryTileTicksInDrought);
        Assert.Equal(0, f.WitheredInDrought);
        Assert.Equal(f.Tiles, f.GrowingOrMatureAtDroughtEnd);
        Assert.True(f.HarvestsInDrought > 0, "no crop matured and was harvested during the drought");
        Assert.False(r.NoWaterInDrought, "the pump flagged NoWater during the drought");
        Assert.True(r.AllAlive);
    }

    /// <summary>Control for the field: beside the open pool the same field dries out, because the pool drains into the
    /// empty river bed; by the end of the drought no tile is moist. (The 20-wide pool drains through its 2-wide mouth
    /// slowly, so the dry spell is shorter than the day it takes to wither a crop, ECO-13; withering in a drought is
    /// shown on seed 1 without a reservoir, M6-T8.)</summary>
    [Fact]
    public void FieldBesideOpenPool_DriesOutInDrought()
    {
        var f = Play(levees: false, field: true).Field!;
        Assert.Equal(20, f.Tiles);
        Assert.True(f.DryTileTicksInDrought > 0, "the field stayed moist beside the open pool");
        Assert.Equal(f.Tiles, f.DryTilesAtDroughtEnd);
    }
}
