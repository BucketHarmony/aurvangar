using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
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
/// the dwarves wall the mouth with two levees on day 1; in the other the pool stays open to the river.</summary>
[Trait("Category", "Scenario")]
public class ReservoirScenarioTests
{
    private const int C = 5, Bank = 6;
    private const long Day = SimClock.TicksPerDay;
    private static readonly Int3 PumpOrigin = new(8, Bank, 10);
    private static readonly Int3 Intake = new(8, C, 11);
    private static readonly Int3[] Mouth = { new(9, C, 14), new(10, C, 14) };
    private static readonly Int3 RiverAtMouth = new(9, C, 16);

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
        for (int z = 11; z <= 17; z++)
            for (int x = 1; x <= 30; x++)
                if (rows[z][x] == '.') b.Water(new Int3(x, C, z), WaterGrid.Full);
        b.Source(new Int3(1, C, 16)).Source(new Int3(1, C, 17)).Drain(new Int3(30, C, 16)).Drain(new Int3(30, C, 17));
        return b.Hub(new Int3(20, Bank, 4)).Stock("log", 30).Stock("berries", 100).Stock("water", 20)
            .Agent(new Int3(10, Bank, 6)).Agent(new Int3(12, Bank, 6)).Build();
    }

    private sealed record Run(bool NoWaterInDrought, long PumpedInDrought, int RiverAtMouthMidDrought, int IntakeMidDrought,
        int MinIntakeInDrought, bool AllAlive);

    private static Run Play(bool levees)
    {
        var sim = World();
        sim.Enqueue(new PlaceBuilding("pump", PumpOrigin, 0));
        sim.Tick();
        var pump = Site(sim, "pump");
        Assert.Equal(Intake, BuildingShape.Intake(pump.Def, pump.Origin, pump.Rotation));
        RunUntil(sim, () => pump.State == BuildingState.Complete, 3000);

        sim.RunTicks((int)(Day - sim.Clock.Tick));
        Assert.True(sim.Water.GetLevel(Intake) >= 256, $"pool not filled by the river: {sim.Water.GetLevel(Intake)}");
        if (levees)
        {
            foreach (var m in Mouth) sim.Enqueue(new PlaceBuilding("levee", m, 0));
            sim.Tick();
            var line = sim.Buildings.All.Where(x => x.Def.Id == "levee").ToList();
            Assert.Equal(2, line.Count);
            RunUntil(sim, () => line.All(l => l.State == BuildingState.Complete), 4000);
        }

        sim.RunTicks((int)(5 * Day - sim.Clock.Tick));
        Assert.Equal(Season.Drought, WeatherSystem.SeasonAt(sim.Clock.Tick));
        long pumpedAtStart = sim.Water.Stats.Pumped;
        bool noWater = false; int minIntake = int.MaxValue, riverMid = -1, intakeMid = -1;
        while (sim.Clock.Tick < 7 * Day)
        {
            sim.Tick();
            noWater |= pump.NoWater;
            minIntake = Math.Min(minIntake, sim.Water.GetLevel(Intake));
            if (sim.Clock.Tick == 6 * Day) { riverMid = sim.Water.GetLevel(RiverAtMouth); intakeMid = sim.Water.GetLevel(Intake); }
        }
        return new Run(noWater, sim.Water.Stats.Pumped - pumpedAtStart, riverMid, intakeMid, minIntake,
            sim.Agents.All.All(a => a.IsAlive));
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
        Assert.True(r.NoWaterInDrought, "the pump never flagged NoWater");
    }
}
