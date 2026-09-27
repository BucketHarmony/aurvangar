using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Scripts;
using Xunit;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M5-T7: <see cref="SurvivalScript"/> through the pump, the warehouse and a levee line on seed 1. The full
/// survival scenario (all 5 alive at day 10) needs farms and is M6-T6 (<c>SurvivalScenarioTests</c>).</summary>
[Trait("Category", "Scenario")]
public class SurvivalScriptTests
{
    private const long Day = 2400;

    private static int HubWater(Simulation sim) =>
        sim.Buildings.All.Single(b => b.Def.Id == "hub").Stored.GetValueOrDefault(TestContent.Db.Item("water").Value);

    /// <summary>Runs the script on <paramref name="sim"/> for <paramref name="ticks"/> ticks, collecting the events
    /// of interest with the tick they were emitted on.</summary>
    private static void Run(Simulation sim, long ticks, List<(long Tick, SimEvent Event)> events)
    {
        for (long i = 0; i < ticks; i++)
        {
            SurvivalScript.EnqueueDue(sim);
            sim.Tick();
            foreach (var e in sim.Events.Drain())
                if (e is CommandRejected or AgentDied or ColonyLost or BuildingCompleted) events.Add((sim.Clock.Tick, e));
        }
    }

    [Fact]
    public void Commands_AreInTickOrder_AndLoggedAtTheirTicks()
    {
        var ticks = SurvivalScript.Commands.Select(c => c.Tick).ToList();
        Assert.Equal(ticks.OrderBy(t => t), ticks);
        Assert.Equal(0, ticks[0]);
        Assert.IsType<DesignateDig>(SurvivalScript.Commands[0].Command);

        var sim = WorldFactory.Create(SurvivalScript.Seed, TestContent.Db);
        SurvivalScript.Run(sim, SurvivalScript.LastTick + 1);
        Assert.Equal(SurvivalScript.Commands, sim.Commands.Log);
    }

    /// <summary>Every command is accepted: the pump's pad and the reservoir's pump end are dug before the pump goes down,
    /// and within day 1 the pump, the warehouse and the levee line are complete. The pump draws from the reservoir, which
    /// the river fills once its dam is dug (M7-T3), and the hub's water rises above its starting 30. (Before M7-T3 the
    /// pump stood on the open river from tick ~800 and the hub held more than 50 by now; the reservoir pump starts at
    /// ~1,300, after its trench is dug and filled.)</summary>
    [Fact]
    public void Seed1_BuildsPumpWarehouseAndLevees_AllAccepted()
    {
        var sim = WorldFactory.Create(SurvivalScript.Seed, TestContent.Db);
        var events = new List<(long Tick, SimEvent Event)>();
        Run(sim, Day + 600, events);
        Assert.DoesNotContain(events, e => e.Event is CommandRejected);
        Assert.Empty(sim.Designations.All);

        var built = sim.Buildings.All.Where(b => b.Def.Id is not ("hub" or "wagon")).OrderBy(b => b.Id.Value).ToList();
        Assert.Equal(new[] { "pump", "warehouse" }.Concat(Enumerable.Repeat("levee", SurvivalScript.LeveeCount)),
            built.Select(b => b.Def.Id));
        Assert.All(built, b => Assert.Equal(BuildingState.Complete, b.State));
        Assert.All(events.Where(e => e.Event is BuildingCompleted), e => Assert.True(e.Tick <= Day, $"completed at {e.Tick}"));

        var pump = built[0];
        Assert.Equal(SurvivalScript.PumpOrigin, pump.Origin);
        Assert.False(pump.NoWater);
        Assert.True(sim.Water.GetLevel(BuildingShape.Intake(pump.Def, pump.Origin, pump.Rotation)) >= pump.Def.Producer!.MinIntakeLevel);

        int water = HubWater(sim);
        Assert.True(water > 30, $"hub water at tick {sim.Clock.Tick}: {water}");
        Assert.True(sim.Water.Stats.Pumped >= 64L * (water - 30), $"pumped {sim.Water.Stats.Pumped}");
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>Without commands the colony dies of thirst at tick 15,012 (M5-T5). With the pump working nobody dies
    /// of thirst: all 5 are alive at the end of day 7 with a full hub of water, and through day 10 no one is
    /// Dehydrated and the colony stands (the 40 starting berries run out first; farms are M6).</summary>
    [Fact]
    public void Seed1_PumpOutlastsStartingWater_NoOneDiesOfThirst()
    {
        var sim = WorldFactory.Create(SurvivalScript.Seed, TestContent.Db);
        var events = new List<(long Tick, SimEvent Event)>();
        Run(sim, 7 * Day, events);
        Assert.Equal(5, sim.Agents.All.Count(a => a.IsAlive));
        Assert.True(HubWater(sim) >= 90, $"hub water at day 7: {HubWater(sim)}");

        Run(sim, 3 * Day, events);
        Assert.DoesNotContain(sim.Agents.All, a => a.Death == DeathCause.Dehydrated);
        Assert.DoesNotContain(events, e => e.Event is ColonyLost);
        Assert.False(sim.Agents.ColonyLost);
    }
}
