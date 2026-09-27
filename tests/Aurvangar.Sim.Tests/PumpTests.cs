using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;
using static Aurvangar.Sim.Tests.Scenarios.PumpScenarioTests;

namespace Aurvangar.Sim.Tests;

/// <summary>M5-T4: the pump cycle through <see cref="WorldActions.Work"/> (BLD-13), taking from its buffer (BLD-14),
/// and deconstructing a pump that is being worked. World as in <see cref="Scenarios.PumpScenarioTests"/>.</summary>
[Trait("Category", "Unit")]
public class PumpTests
{
    private static ItemId Water => TestContent.Db.Item("water");

    [Fact]
    public void Work_CycleOf30_Makes1Water_Takes64_FullBufferRefuses()
    {
        var sim = BasinWorld(WaterGrid.Full, agents: 0).Storage("pump", PumpOrigin).Build();
        var pump = Pump(sim);
        var worker = sim.Agents.Spawn(pump.EntranceCell, "Worker");
        var work = WorkTarget.AtBuilding(pump.Id);
        for (int i = 0; i < 29; i++) Assert.Equal(ActionResult.Ok, sim.Actions.Work(worker.Id, work));
        Assert.Equal(0, Buffer(sim));
        Assert.Equal(29, pump.Progress);
        Assert.Equal(ActionResult.Ok, sim.Actions.Work(worker.Id, work));
        Assert.Equal(1, Buffer(sim));
        Assert.Equal(0, pump.Progress);
        Assert.Equal(WaterGrid.Full - 64, sim.Water.GetLevel(Intake));
        Assert.Equal(64, sim.Water.Stats.Pumped);

        pump.Stored[Water.Value] = 10;
        Assert.Equal(ActionResult.StorageFull, sim.Actions.Work(worker.Id, work));
        Assert.Equal(0, pump.Progress);

        // BLD-14: the buffer gives out its output only.
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.PickUpFromStorage(worker.Id, pump.Id, TestContent.Db.Item("log"), 1));
        Assert.Equal(ActionResult.Ok, sim.Actions.PickUpFromStorage(worker.Id, pump.Id, Water, 6));
        Assert.Equal(4, Buffer(sim));
        Assert.Equal(new ItemStack(Water, 6), worker.Carried);
        // A pump does not take deliveries: it is not storage.
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.DeliverTo(worker.Id, pump.Id));
    }

    /// <summary>BLD-14 (M8-T6, ADR-066): the buffer haul runs at the OperatePump priority, above Dig (and Build, Chop):
    /// the only dwarf takes the full buffer to the hub before it starts on an open dig, so a busy colony keeps its
    /// pump running.</summary>
    [Fact]
    public void BufferHaul_OutranksDigAndBuild()
    {
        Assert.Equal(Job.DefaultPriority(JobKind.OperatePump), Pumps.BufferHaulPriority);
        Assert.True(Pumps.BufferHaulPriority > Job.DefaultPriority(JobKind.Dig));
        Assert.True(Pumps.BufferHaulPriority > Job.DefaultPriority(JobKind.Build));

        var sim = BasinWorld(WaterGrid.Full, agents: 1).Storage("pump", PumpOrigin).Build();
        var pump = Pump(sim);
        pump.Stored[Water.Value] = pump.Def.Producer!.Buffer;
        sim.Enqueue(new DesignateDig(new Int3(12, Bank - 1, 5), new Int3(14, Bank - 1, 7)));
        sim.Tick();
        Assert.Contains(sim.Jobs.All, j => j.Kind == JobKind.Dig);
        var haul = Assert.Single(sim.Jobs.All, Pumps.IsBufferHaul);
        Assert.Equal(Pumps.BufferHaulPriority, haul.Priority);
        var dwarf = sim.Agents.All.Single();
        RunUntil(sim, () => dwarf.CurrentJob.IsValid, 50);
        Assert.Equal(JobKind.Haul, sim.Jobs.Get(dwarf.CurrentJob)!.Kind);
    }

    [Fact]
    public void Deconstruct_WorkedPump_StopsWorker_DropsBuffer_WaterReachesHub()
    {
        var sim = BasinWorld(WaterGrid.Full, agents: 1).Storage("pump", PumpOrigin).Build();
        var pump = Pump(sim);
        RunUntil(sim, () => Buffer(sim) == 3, 1000);
        Assert.Contains(sim.Jobs.All, j => j.Kind == JobKind.OperatePump && j.IsClaimed);

        sim.Enqueue(new Deconstruct(pump.Id));
        sim.Tick();
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.OperatePump);
        Assert.Equal(BuildingState.Deconstructing, pump.State);
        long pumped = sim.Water.Stats.Pumped;

        RunUntil(sim, () => sim.Buildings.Get(pump.Id) is null, 1000);
        Assert.Equal(pumped, sim.Water.Stats.Pumped);
        RunUntil(sim, () => Stored(Hub(sim), "water") == 3, 1000);
        Assert.Equal(0, PileTotal(sim, "water"));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }
    /// <summary>Review fix: a buffer haul claimed before its pump is deconstructed is withdrawn (the hauler has not
    /// picked up yet), never left on the board for a building that no longer exists; the buffer ends in the hub.</summary>
    [Fact]
    public void Deconstruct_PumpWithClaimedHaul_LeavesNoOrphanHaul()
    {
        var sim = BasinWorld(0, agents: 1).Storage("pump", PumpOrigin).Build();
        var pump = Pump(sim);
        pump.Stored[Water.Value] = 6;
        RunUntil(sim, () => sim.Jobs.All.Any(j => j.Kind == JobKind.Haul && j.IsClaimed), 100);
        Assert.True(pump.NoWater);

        sim.Enqueue(new Deconstruct(pump.Id));
        sim.Tick();
        Assert.False(pump.NoWater);
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Haul && j.Steps[1].Target == pump.Id.Value);
        RunUntil(sim, () => sim.Buildings.Get(pump.Id) is null, 1000);
        RunUntil(sim, () => Stored(Hub(sim), "water") == 6, 1000, () =>
            Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Haul && j.Steps[1].Kind == StepKind.PickUpFromStorage));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }
}
