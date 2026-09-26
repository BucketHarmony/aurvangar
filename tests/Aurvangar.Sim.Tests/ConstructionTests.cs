using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests;

/// <summary>M5-T2: the PlaceBuilding and Deconstruct commands, BLD-04 stack order, dig marks under blueprints and
/// save/load of construction sites.</summary>
[Trait("Category", "Unit")]
public class ConstructionTests
{
    private static Simulation Flat(Action<ScenarioBuilder>? more = null)
    {
        var b = new ScenarioBuilder().Ground(G - 1);
        more?.Invoke(b);
        return b.Build();
    }

    [Fact]
    public void PlaceBuilding_RejectsWithReason_PlacesWithEvent()
    {
        var sim = Flat(b => b.Hub(new Int3(20, G, 20)));
        sim.Enqueue(new PlaceBuilding("warehouse", WhOrigin, 0));
        sim.Enqueue(new PlaceBuilding("warehouse", WhOrigin + new Int3(1, 0, 0), 0));
        sim.Enqueue(new PlaceBuilding("hub", new Int3(3, G, 3), 0));
        sim.Enqueue(new PlaceBuilding("castle", new Int3(3, G, 3), 0));
        sim.Enqueue(new PlaceBuilding("levee", new Int3(3, G, 3), 45));
        sim.Tick();
        var events = sim.Events.Drain();
        var placed = events.OfType<BuildingPlaced>().ToList();
        Assert.Single(placed);
        Assert.Equal(Site(sim).Id, placed[0].Building);
        var rejected = events.OfType<CommandRejected>().ToList();
        Assert.All(rejected, r => Assert.Equal("PlaceBuilding", r.Command));
        Assert.Equal(new[] { "Overlaps", "PrebuiltOnly", "UnknownBuilding", "BadRotation" }, rejected.Select(r => r.Reason));
        Assert.Equal(2, sim.Buildings.All.Count());
    }

    [Fact]
    public void Deconstruct_Rejections()
    {
        var sim = Flat(b => b.Hub(new Int3(20, G, 20)));
        var hub = Hub(sim);
        var levee = TestContent.Db.Building("levee");
        sim.Buildings.PlacePrebuilt(levee, new Int3(5, G, 5), 0);
        var lower = sim.Buildings.All.Last();
        Assert.Equal(PlacementResult.Ok, sim.Buildings.TryPlaceBlueprint(levee, new Int3(5, G + 1, 5), 0, out var upper));
        sim.Enqueue(new Deconstruct(hub.Id));
        sim.Enqueue(new Deconstruct(new BuildingId(999)));
        sim.Enqueue(new Deconstruct(lower.Id));      // an upper levee stands on it
        sim.Enqueue(new Deconstruct(upper!.Id));     // cancelled (nothing delivered)
        sim.Enqueue(new Deconstruct(lower.Id));      // now free
        sim.Enqueue(new Deconstruct(lower.Id));      // already being deconstructed
        sim.Tick();
        var rejected = sim.Events.Drain().OfType<CommandRejected>().ToList();
        Assert.All(rejected, r => Assert.Equal("Deconstruct", r.Command));
        Assert.Equal(new[] { "PrebuiltOnly", "UnknownBuilding", "BuildingOnTop", "AlreadyDeconstructing" },
            rejected.Select(r => r.Reason));
        Assert.Null(sim.Buildings.Get(upper.Id));
        Assert.Equal(BuildingState.Deconstructing, lower.State);
        Assert.Equal(BuildingState.Complete, hub.State);
    }

    /// <summary>BLD-04: an upper levee gets no Deliver job until the levee below it is complete; both end complete.</summary>
    [Fact]
    public void StackedLevee_WaitsForTheOneBelow()
    {
        var sim = Flat(b => b.Hub(new Int3(20, G, 20)).Stock("log", 10).Agent(new Int3(8, G, 8)));
        var lowerAt = new Int3(10, G, 10);
        sim.Enqueue(new PlaceBuilding("levee", lowerAt, 0));
        sim.Enqueue(new PlaceBuilding("levee", lowerAt + Int3.Up, 0));
        sim.Tick();
        var lower = sim.Buildings.BuildingAt(lowerAt)!;
        var upper = sim.Buildings.BuildingAt(lowerAt + Int3.Up)!;
        Assert.NotEqual(lower.Id, upper.Id);
        RunUntil(sim, () => upper.State == BuildingState.Complete, 2000, () =>
        {
            if (lower.State != BuildingState.Complete)
            {
                Assert.Equal(BuildingState.Blueprint, upper.State);
                Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Deliver && j.Target == upper.EntranceCell);
            }
        });
        Assert.Equal(BlockId.BuildingSolid, sim.World.GetBlock(lowerAt));
        Assert.Equal(BlockId.BuildingSolid, sim.World.GetBlock(lowerAt + Int3.Up));
        Assert.Equal(6, Stored(Hub(sim), "log"));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>Placing a blueprint clears dig marks on the ground under it (DSG-02 never marks a building's floor),
    /// cancelling their jobs; a dig under a building is refused.</summary>
    [Fact]
    public void Blueprint_ClearsDigMarksOnItsGround()
    {
        var sim = Flat();
        sim.Enqueue(new DesignateDig(new Int3(9, G - 1, 9), new Int3(12, G - 1, 12)));
        sim.Tick();
        Assert.Equal(16, sim.Designations.Count);
        Assert.Contains(sim.Jobs.All, j => j.Kind == JobKind.Dig && j.Target == new Int3(10, G - 1, 10));
        sim.Enqueue(new PlaceBuilding("warehouse", WhOrigin, 0));
        sim.Tick();
        Assert.Equal(BuildingState.Blueprint, Site(sim).State);
        Assert.Equal(12, sim.Designations.Count);
        foreach (var c in Site(sim).FootprintCells().Where(c => c.Y == G))
        {
            Assert.Equal(DesignationMark.None, sim.Designations.Get(c + Int3.Down));
            Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Dig && j.Target == c + Int3.Down);
        }
        var a = sim.Agents.Spawn(new Int3(9, G, 10), "A");
        Assert.Equal(Actions.ActionResult.Blocked, sim.Actions.Dig(a.Id, new Int3(10, G - 1, 10)));
    }

    /// <summary>SAV-03 with a construction site mid-build: the load has the same hash, the footprint is still not
    /// walkable, and both runs stay equal afterwards.</summary>
    [Fact]
    public void SaveLoad_MidConstruction_SameHashAndBlocking()
    {
        var sim = Flat(b => b.Hub(new Int3(20, G, 20)).Stock("log", 40).Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15)));
        sim.Enqueue(new PlaceBuilding("warehouse", WhOrigin, 0));
        sim.Tick();
        var wh = Site(sim);
        RunUntil(sim, () => wh.State == BuildingState.UnderConstruction, 2000);
        sim.RunTicks(20);

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        foreach (var c in Site(loaded).FootprintCells()) Assert.False(loaded.PathGrid.IsWalkable(c));
        Assert.Equal(sim.Regions.RegionOf(new Int3(10, G, 8)), loaded.Regions.RegionOf(new Int3(10, G, 8)));

        for (int i = 0; i < 1500; i++)
        {
            sim.Tick();
            loaded.Tick();
            Assert.Equal(sim.StateHash(), loaded.StateHash());
        }
        Assert.Equal(BuildingState.Complete, Site(loaded).State);
    }

    [Fact]
    public void Commands_RoundTripThroughTheSaveLog()
    {
        var sim = Flat(b => b.Hub(new Int3(20, G, 20)));
        sim.Enqueue(new PlaceBuilding("levee", new Int3(5, G, 5), 90));
        sim.Tick();
        sim.Enqueue(new Deconstruct(sim.Buildings.All.Last().Id));
        sim.Enqueue(new PlaceBuilding("warehouse", WhOrigin, 270));   // pending at save time
        sim.Tick();
        sim.Enqueue(new PlaceBuilding("warehouse", WhOrigin, 270));
        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.Commands.Log, loaded.Commands.Log);
        Assert.Equal(1, loaded.Commands.PendingCount);
    }

    /// <summary>ADR-048: a Deliver job released after its pickup (its StorageOut promise used up) gets its reservation
    /// back when it is re-planned, so it is not claimed again without stock to take.</summary>
    [Fact]
    public void DeliverReleasedAfterPickup_GetsItsReservationBack()
    {
        var sim = Flat(b => b.Hub(new Int3(20, G, 20)).Stock("log", 40).Agent(new Int3(15, G, 15)));
        sim.Enqueue(new PlaceBuilding("warehouse", WhOrigin, 0));
        var a = sim.Agents.All.First();
        RunUntil(sim, () => !a.Carried.IsEmpty && sim.Jobs.Get(a.CurrentJob) is { Kind: JobKind.Deliver }, 2000);
        var job = sim.Jobs.Get(a.CurrentJob)!;
        Assert.DoesNotContain(job.Reservations, r => r.Kind == ReservationKind.StorageOut);

        JobRunner.ReleaseCurrent(sim, a);
        Assert.False(job.IsClaimed);
        sim.Tick();   // re-planned at step 7; the agent may take it again at step 10, from step 0
        Assert.Same(job, sim.Jobs.Get(job.Id));
        Assert.Contains(job.Reservations, r => r.Kind == ReservationKind.StorageOut);
        Assert.All(sim.Jobs.All.Where(j => j.Kind == JobKind.Deliver && sim.Agents.Get(j.ClaimedBy) is not { StepIndex: > 1 }),
            j => Assert.Contains(j.Reservations, r => r.Kind == ReservationKind.StorageOut));
    }
}
