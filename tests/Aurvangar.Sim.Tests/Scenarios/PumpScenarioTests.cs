using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M5-T4: the pump (BLD-13, BLD-14; buildings.md scenario 4). Worlds are stone to y = 4 with a stone layer
/// at y = 5 holding an enclosed basin, so agents walk on y = 6. A pump at (8,6,10), rotation 0, covers (8..9,6,10),
/// has its entrance at (8,6,9) and draws from the intake (8,5,11), the basin cell under its front (8,6,11).</summary>
[Trait("Category", "Scenario")]
public class PumpScenarioTests
{
    private const int Basin = 5;
    internal const int Bank = 6;
    internal static readonly Int3 PumpOrigin = new(8, Bank, 10);
    internal static readonly Int3 Intake = new(8, Basin, 11);

    /// <summary>Stone layer at y = 5 with an open basin x 5..14, z 11..13 (30 cells) holding <paramref name="level"/>
    /// per cell (0 = dry). Hub on the bank, <paramref name="agents"/> agents near the pump.</summary>
    internal static ScenarioBuilder BasinWorld(int level, int agents, int logs = 0, int x0 = 5, int x1 = 14, int z1 = 13)
    {
        var b = new ScenarioBuilder().Ground(Basin - 1);
        var rows = new string[32];
        for (int z = 0; z < 32; z++)
            rows[z] = new string(Enumerable.Range(0, 32).Select(x => x >= x0 && x <= x1 && z >= 11 && z <= z1 ? '.' : 'S').ToArray());
        b.Layer(Basin, rows);
        if (level > 0)
            for (int z = 11; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    b.Water(new Int3(x, Basin, z), level);
        b.Hub(new Int3(20, Bank, 20));
        if (logs > 0) b.Stock("log", logs);
        for (int i = 0; i < agents; i++) b.Agent(new Int3(10 + i, Bank, 6));
        return b;
    }

    internal static Building Pump(Simulation sim) => Site(sim, "pump");

    internal static int Buffer(Simulation sim) => Stored(Pump(sim), "water");

    /// <summary>All water that exists as an item: hub, pump buffer, carried, piles.</summary>
    internal static int WaterItems(Simulation sim) =>
        Stored(Hub(sim), "water") + Buffer(sim) + CarriedTotal(sim, "water") + PileTotal(sim, "water");

    private static long Accounted(Simulation sim) =>
        sim.Water.TotalVolume() + sim.Water.Stats.Drained + sim.Water.Stats.Evaporated + sim.Water.Stats.Pumped
        - sim.Water.Stats.SourceAdded;

    /// <summary>Scenario 4, first half: the colonists build a pump from hub logs, one of them works it and the water
    /// is hauled to the hub (BLD-14). The buffer never holds more than 10.</summary>
    [Fact]
    public void Pump_FillsHubWithWater()
    {
        var sim = BasinWorld(WaterGrid.Full, agents: 2, logs: 20).Build();
        sim.Enqueue(new PlaceBuilding("pump", PumpOrigin, 0));
        sim.Tick();
        var pump = Pump(sim);
        Assert.Equal(Intake, BuildingShape.Intake(pump.Def, pump.Origin, pump.Rotation));
        RunUntil(sim, () => pump.State == BuildingState.Complete, 3000);
        Assert.Equal(20 - 12, Stored(Hub(sim), "log"));

        bool operated = false, hauled = false;
        RunUntil(sim, () => Stored(Hub(sim), "water") >= 10, 3000, () =>
        {
            Assert.True(Buffer(sim) <= 10, $"buffer {Buffer(sim)} over 10");
            Assert.True(sim.Jobs.All.Count(j => j.Kind == JobKind.OperatePump) <= 1);
            operated |= sim.Jobs.All.Any(j => j.Kind == JobKind.OperatePump && j.IsClaimed);
            hauled |= sim.Jobs.All.Any(j => j.Kind == JobKind.Haul && j.IsClaimed
                && j.Steps.Any(s => s.Kind == StepKind.PickUpFromStorage && s.Target == pump.Id.Value));
        });
        Assert.True(operated, "no one worked the pump");
        Assert.True(hauled, "no water haul from the pump");
        Assert.False(pump.NoWater);
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.True(sim.Water.Stats.Pumped >= 10 * 64);
        Assert.Equal(sim.Water.Stats.Pumped, 64L * WaterItems(sim));
    }

    /// <summary>Scenario 4, second half (BLD-13): a pump on a dry bank flags NoWater, produces nothing and keeps no
    /// worker; when water reaches the intake the flag clears and it produces. A pump that draws its intake below 256
    /// flags NoWater again and stops.</summary>
    [Fact]
    public void Pump_DryIntake_FlagsNoWater()
    {
        var sim = BasinWorld(0, agents: 1).Storage("pump", PumpOrigin).Build();
        var pump = Pump(sim);
        sim.RunTicks(200);
        Assert.True(pump.NoWater);
        Assert.Equal(0, Buffer(sim));
        Assert.Equal(0, sim.Water.Stats.Pumped);
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.OperatePump);
        Assert.All(sim.Agents.All, a => Assert.Equal(AgentState.Idle, a.State));

        // Water arrives: only the intake cell is walled in, so its level stays where it is put.
        sim.World.SetBlock(new Int3(7, Basin, 11), BlockId.Stone);
        sim.World.SetBlock(new Int3(9, Basin, 11), BlockId.Stone);
        sim.World.SetBlock(new Int3(8, Basin, 12), BlockId.Stone);
        sim.Water.SetLevel(Intake, 320);
        RunUntil(sim, () => WaterItems(sim) >= 1, 200);
        Assert.False(pump.NoWater);

        // 320 -> 256 -> 192: two cycles produce; then the intake is below 256 and the pump stops.
        RunUntil(sim, () => pump.NoWater, 400);
        sim.RunTicks(200);
        Assert.Equal(2, WaterItems(sim));
        Assert.Equal(192, sim.Water.GetLevel(Intake));
        Assert.Equal(2 * 64, sim.Water.Stats.Pumped);
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.OperatePump);
        Assert.All(sim.Agents.All, a => Assert.Equal(AgentState.Idle, a.State));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>BLD-13, WAT-11: every water item made took 64 units out of the basin, counted in WaterStats.Pumped,
    /// and the volume books balance exactly. A save in the middle of a cycle continues identically.</summary>
    [Fact]
    public void Pump_RemovesWaterFromWorld()
    {
        var sim = BasinWorld(WaterGrid.Full, agents: 1).Storage("pump", PumpOrigin).Build();
        long initial = sim.Water.TotalVolume();
        Assert.Equal(30L * WaterGrid.Full, initial);

        RunUntil(sim, () => Pump(sim).Progress > 0 && Pump(sim).Progress < 30 && WaterItems(sim) >= 3, 1000);
        using (var ms = new MemoryStream())
        {
            SaveGame.Save(sim, ms);
            ms.Position = 0;
            var loaded = SaveGame.Load(ms, TestContent.Db);
            Assert.Equal(sim.StateHash(), loaded.StateHash());
            for (int t = 0; t < 300; t++) { sim.Tick(); loaded.Tick(); }
            Assert.Equal(sim.StateHash(), loaded.StateHash());
        }

        sim.RunTicks(1200);
        int made = WaterItems(sim);
        Assert.True(made >= 20, $"only {made} water made");
        Assert.True(Stored(Hub(sim), "water") > 0);
        Assert.Equal(64L * made, sim.Water.Stats.Pumped);
        Assert.Equal(initial, Accounted(sim));
        Assert.True(sim.Water.TotalVolume() < initial);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }
}
