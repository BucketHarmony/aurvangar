using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Scripts;
using Xunit;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>needs-economy.md scenario 5 (M6-T6): the full <see cref="SurvivalScript"/> on seed 1 keeps all 5 dwarves
/// alive to day 10 through the pump, farm, hill dig, bank breach, levee repair and the day-5 drought. The control
/// run without commands loses the colony.</summary>
[Trait("Category", "Scenario")]
public class SurvivalScenarioTests
{
    private const long Day = 2400;

    private static int Stored(Simulation sim, string item)
    {
        var id = TestContent.Db.Item(item).Value;
        return sim.Buildings.All.Where(b => b.State == BuildingState.Complete).Sum(b => b.Stored.GetValueOrDefault(id));
    }

    /// <summary>The tunnel's standing cells (its lower layer), which the breach floods.</summary>
    private static IEnumerable<Int3> TunnelFloor()
    {
        var a = SurvivalScript.TunnelA; var b = SurvivalScript.TunnelB;
        for (int z = a.Z; z <= b.Z; z++)
            for (int x = a.X; x <= b.X; x++)
                yield return new Int3(x, a.Y, z);
    }

    private sealed class Watch
    {
        public readonly List<(long Tick, SimEvent Event)> Events = new();
        /// <summary>Agents that stood in a deep tunnel cell at some tick after the breach.</summary>
        public readonly SortedSet<int> CaughtInFlood = new();
    }

    private static void RunTo(Simulation sim, long tick, Watch w)
    {
        var tunnel = TunnelFloor().ToHashSet();
        while (sim.Clock.Tick < tick)
        {
            SurvivalScript.EnqueueDue(sim);
            sim.Tick();
            foreach (var e in sim.Events.Drain())
                if (e is CommandRejected or AgentDied or ColonyLost or BuildingCompleted) w.Events.Add((sim.Clock.Tick, e));
            if (sim.Clock.Tick > SurvivalScript.BreachTick)
                foreach (var a in sim.Agents.All)
                    if (a.IsAlive && tunnel.Contains(a.Cell) && sim.Water.IsDeep(a.Cell)) w.CaughtInFlood.Add(a.Id.Value);
        }
    }

    [Fact]
    public void Seed1_SurvivalScript_AllAliveAtDay10()
    {
        var sim = WorldFactory.Create(SurvivalScript.Seed, TestContent.Db);
        var w = new Watch();

        // DoD 3, 6: the field is planted and the tunnel is dug into stone, which ends in storage before the breach.
        RunTo(sim, SurvivalScript.BreachTick, w);
        Assert.True(sim.Farms.All.Count() == 36, $"farm tiles: {sim.Farms.All.Count()}");
        Assert.All(TunnelFloor(), c => Assert.Equal(BlockId.Air, sim.World.GetBlock(c)));
        Assert.Empty(sim.Designations.All);
        Assert.True(Stored(sim, "stone") >= 10, $"stone stored before the breach: {Stored(sim, "stone")}");
        Assert.All(SurvivalScript.BreachCells(), c => Assert.NotEqual(BlockId.Air, sim.World.GetBlock(c)));
        Assert.All(TunnelFloor(), c => Assert.Equal(0, sim.Water.GetLevel(c)));

        // DoD 7: the bank is dug through and the river floods the tunnel.
        RunTo(sim, SurvivalScript.RepairTick, w);
        Assert.All(SurvivalScript.BreachCells(), c => Assert.Equal(BlockId.Air, sim.World.GetBlock(c)));
        Assert.Contains(TunnelFloor(), c => sim.Water.IsDeep(c));

        // The levees on the breach are built before the drought (DoD 8 starts at day 5), and the potato harvest is in.
        RunTo(sim, 5 * Day, w);
        var repairs = sim.Buildings.All.Where(b => SurvivalScript.BreachCells().Contains(b.Origin)).ToList();
        Assert.Equal(2, repairs.Count);
        Assert.All(repairs, b => Assert.Equal(BuildingState.Complete, b.State));
        Assert.All(SurvivalScript.BreachCells(), c => Assert.Equal(BlockId.BuildingSolid, sim.World.GetBlock(c)));
        Assert.True(Stored(sim, "potato") > 0, "no potatoes harvested before the drought");

        // Mid-drought the river outside the breach is dry, but the walled-off tunnel keeps its flood: the levees hold.
        RunTo(sim, 6 * Day + Day / 2, w);
        Assert.Equal(0, sim.Water.GetLevel(SurvivalScript.BreachB + new Int3(0, 0, 1)));
        Assert.Contains(TunnelFloor(), c => sim.Water.IsDeep(c));

        RunTo(sim, 10 * Day, w);
        Assert.DoesNotContain(w.Events, e => e.Event is CommandRejected);
        Assert.DoesNotContain(w.Events, e => e.Event is AgentDied or ColonyLost);
        Assert.Equal(5, sim.Agents.All.Count(a => a.IsAlive));
        Assert.False(sim.Agents.ColonyLost);
        // Dwarves caught in the flooded tunnel got out (WAT-14 flee) and lived.
        Assert.NotEmpty(w.CaughtInFlood);
    }

    /// <summary>Control: with no commands the 30 starting water runs out and the colony dies of thirst before the end
    /// of day 7 (tick 15,009 on seed 1, M5-T5).</summary>
    [Fact]
    public void Seed1_NoCommands_ColonyLost()
    {
        var sim = WorldFactory.Create(SurvivalScript.Seed, TestContent.Db);
        long lostAt = -1;
        while (sim.Clock.Tick < 7 * Day && lostAt < 0)
        {
            sim.Tick();
            foreach (var e in sim.Events.Drain())
                if (e is ColonyLost) lostAt = sim.Clock.Tick;
        }
        Assert.True(lostAt > 0, "the colony survived 7 days without commands");
        Assert.True(sim.Agents.ColonyLost);
        Assert.All(sim.Agents.All, a => Assert.False(a.IsAlive));
        Assert.Contains(sim.Agents.All, a => a.Death == DeathCause.Dehydrated);
        Assert.Empty(sim.Commands.Log);
    }

    /// <summary>DoD step 10 (M6-T8): the player saves mid-session, quits, loads and plays on. The save is taken in the
    /// flooded tunnel before the levee repair (the repair command is still to come); the loaded game, run on with the
    /// rest of the script past the levee repair and into the drought's drain, ends on the same <c>StateHash</c> as the
    /// uninterrupted run. (The day-5 save of SAV-05 and the golden hashes cover the rest of the session.)</summary>
    [Fact]
    public void Seed1_SurvivalScript_SaveLoadMidSession_ContinuesIdentically()
    {
        const long saveAt = SurvivalScript.BreachTick + 200, compareAt = 5 * Day + 600;
        var sim = WorldFactory.Create(SurvivalScript.Seed, TestContent.Db);
        SurvivalScript.Run(sim, saveAt);
        sim.Events.Drain();
        byte[] save;
        using (var ms = new MemoryStream()) { SaveGame.Save(sim, ms); save = ms.ToArray(); }
        SurvivalScript.Run(sim, compareAt - sim.Clock.Tick);

        Simulation loaded;
        using (var ms = new MemoryStream(save)) loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(saveAt, loaded.Clock.Tick);
        SurvivalScript.Run(loaded, compareAt - loaded.Clock.Tick);
        Assert.True(sim.StateHash() == loaded.StateHash(), "hash differs after save, load and continue");
        Assert.Equal(2, loaded.Buildings.All.Count(b => SurvivalScript.BreachCells().Contains(b.Origin)
            && b.State == BuildingState.Complete));
        Assert.Equal(5, loaded.Agents.All.Count(a => a.IsAlive));
    }
}
