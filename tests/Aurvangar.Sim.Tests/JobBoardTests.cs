using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>JOB-03..08 (M4-T6): selection, claiming, reservations, steps, failure and retry, preemption.</summary>
public class JobBoardTests
{
    private static Simulation Flat(params Int3[] agents)
    {
        var b = new ScenarioBuilder().Ground(4);
        foreach (var a in agents) b.Agent(a);
        return b.Build();
    }

    private static Agent AgentN(Simulation sim, int n) => sim.Agents.All.ElementAt(n);

    /// <summary>GoTo(reach of cell) → Work(ticks), reserving the cell.</summary>
    private static Job PostWork(Simulation sim, JobKind kind, Int3 cell, int ticks = 10) =>
        sim.Jobs.Post(kind, cell, new[] { JobStep.GoTo(cell), JobStep.Work(cell, ticks) }, new[] { Reservation.OnCell(cell) });

    /// <summary>JOB-05 dig: GoTo(reach) → Work → Dig, reserving the cell.</summary>
    private static Job PostDig(Simulation sim, Int3 cell, int ticks) =>
        sim.Jobs.Post(JobKind.Dig, cell, new[] { JobStep.GoTo(cell), JobStep.Work(cell, ticks), JobStep.Dig(cell) },
            new[] { Reservation.OnCell(cell) });

    private static void RunUntil(Simulation sim, Func<bool> done, int max, Action? eachTick = null)
    {
        for (int i = 0; i < max && !done(); i++) { sim.Tick(); eachTick?.Invoke(); }
        Assert.True(done(), $"condition not reached within {max} ticks");
    }

    [Fact]
    public void OneJobTwoAgents_OnlyOneClaims()
    {
        var sim = Flat(new Int3(5, 5, 5), new Int3(5, 5, 7));
        var job = PostWork(sim, JobKind.Dig, new Int3(20, 5, 6), ticks: 30);
        Agent a1 = AgentN(sim, 0), a2 = AgentN(sim, 1);

        RunUntil(sim, () => job.IsClaimed, 20);
        var claimer = job.ClaimedBy == a1.Id ? a1 : a2;
        var other = claimer == a1 ? a2 : a1;
        Assert.Equal(job.Id, claimer.CurrentJob);
        Assert.Equal(AgentState.Working, claimer.State);

        RunUntil(sim, () => sim.Jobs.Count == 0, 400, () =>
        {
            Assert.False(other.CurrentJob.IsValid);
            Assert.Equal(AgentState.Idle, other.State);
        });
        Assert.Equal(1, sim.Counters.JobsCompleted);
        Assert.Equal(AgentState.Idle, claimer.State);
        Assert.False(sim.Jobs.IsCellReserved(new Int3(20, 5, 6)));
    }

    [Fact]
    public void Priority_ConstructBeforeHaul()
    {
        var sim = Flat(new Int3(5, 5, 5));
        var haul = PostWork(sim, JobKind.Haul, new Int3(7, 5, 5));          // near, priority 20
        var construct = PostWork(sim, JobKind.Construct, new Int3(25, 5, 25)); // far, priority 45
        var a = AgentN(sim, 0);

        RunUntil(sim, () => a.CurrentJob.IsValid, 20);
        Assert.Equal(construct.Id, a.CurrentJob);
        Assert.False(haul.IsClaimed);
    }

    [Fact]
    public void TieBreak_DistanceThenJobId()
    {
        var at = new Int3(15, 5, 15);
        var sim = Flat(at, at);
        var far = PostWork(sim, JobKind.Dig, new Int3(15, 5, 25), 200);    // J1, distance 10
        var nearA = PostWork(sim, JobKind.Chop, new Int3(15, 5, 20), 200); // J2, distance 5 (same priority 25)
        var nearB = PostWork(sim, JobKind.Dig, new Int3(20, 5, 15), 200);  // J3, distance 5

        RunUntil(sim, () => nearA.IsClaimed && nearB.IsClaimed, 20);
        Assert.Equal(AgentN(sim, 0).Id, nearA.ClaimedBy);   // distance beats id; equal distance → lower id
        Assert.Equal(AgentN(sim, 1).Id, nearB.ClaimedBy);
        Assert.False(far.IsClaimed);
    }

    [Fact]
    public void UnreachableJob_NoAStarRun()
    {
        // Surface at y = 11. A sealed air pocket at y = 5..6 has walkable floor cells in another region.
        var sim = new ScenarioBuilder().Ground(10)
            .FillBox(new Int3(14, 5, 14), new Int3(16, 6, 16), BlockId.Air)
            .Agent(new Int3(5, 11, 5)).Build();
        var pocketWall = new Int3(17, 5, 15);   // stone, reachable only from inside the pocket
        var buried = new Int3(5, 3, 5);         // stone with no standable cell in reach at all
        var j1 = PostDig(sim, pocketWall, 60);
        var j2 = PostDig(sim, buried, 60);
        long before = sim.Pathfinder.Searches;

        sim.RunTicks(200);
        var a = AgentN(sim, 0);
        int agentRegion = sim.Regions.RegionOf(a.Cell), pocketRegion = sim.Regions.RegionOf(new Int3(16, 5, 15));
        Assert.NotEqual(0, agentRegion);
        Assert.NotEqual(0, pocketRegion);
        Assert.NotEqual(agentRegion, pocketRegion);
        Assert.False(j1.IsClaimed || j2.IsClaimed);
        Assert.Equal(0, j1.Failures + j2.Failures);
        Assert.Equal(before, sim.Pathfinder.Searches);   // the region filter ran no A*

        // Control: a reachable job is claimed and pathed to.
        var j3 = PostWork(sim, JobKind.Dig, new Int3(12, 11, 12));
        RunUntil(sim, () => j3.IsClaimed, 10);
        sim.Tick();
        Assert.True(sim.Pathfinder.Searches > before);
    }

    [Fact]
    public void FailedStep_ReleasesAndRetriesAfterCooldown()
    {
        var bump = new Int3(10, 5, 10);
        var sim = new ScenarioBuilder().Ground(4).FillBox(bump, bump, BlockId.Stone).Agent(new Int3(5, 5, 5)).Build();
        var a = AgentN(sim, 0);
        var job = PostDig(sim, bump, 60);

        RunUntil(sim, () => a.StepIndex == 1 && a.StepProgress > 5, 200);   // working on the block
        Assert.True(sim.Jobs.IsCellReserved(bump));
        sim.World.SetBlock(bump, BlockId.Air);                           // target becomes invalid mid-way (scenario 6)

        RunUntil(sim, () => job.Failures == 1, 100);
        long failTick = sim.Clock.Tick - 1;
        Assert.False(job.IsClaimed);
        Assert.Equal(failTick + Job.RetryCooldown, job.RetryAfterTick);
        Assert.False(sim.Jobs.IsCellReserved(bump));
        Assert.False(a.CurrentJob.IsValid);
        Assert.Equal(AgentState.Idle, a.State);
        Assert.Equal(1, sim.Counters.JobsFailed);
        Assert.Same(job, sim.Jobs.Get(job.Id));                         // back on the board

        sim.World.SetBlock(bump, BlockId.Stone);                         // valid again
        RunUntil(sim, () => job.IsClaimed, 100);
        long claimTick = sim.Clock.Tick - 1;
        Assert.InRange(claimTick, job.RetryAfterTick, job.RetryAfterTick + JobRunner.SearchInterval - 1);

        RunUntil(sim, () => sim.Jobs.Count == 0, 300);
        Assert.Equal(BlockId.Air, sim.World.GetBlock(bump));
        Assert.Equal(TestContent.Db.Item("stone"), sim.Piles.At(bump).Item);
        Assert.Equal(1, sim.Counters.JobsCompleted);
        Assert.Equal(1, sim.Counters.JobsFailed);
    }

    [Fact]
    public void FiveFailures_CancelsAndMarksUnreachable()
    {
        var bump = new Int3(10, 5, 10);
        var sim = new ScenarioBuilder().Ground(4).FillBox(bump, bump, BlockId.Bedrock).Agent(new Int3(5, 5, 5)).Build();
        sim.Designations.Set(bump, DesignationMark.Dig);
        var job = PostDig(sim, bump, 5);   // Dig on bedrock → InvalidTarget every time
        var failTicks = new List<long>();
        int seen = 0;

        RunUntil(sim, () => sim.Jobs.Count == 0, 3000, () =>
        {
            if (job.Failures != seen) { seen = job.Failures; failTicks.Add(sim.Clock.Tick - 1); }
        });
        Assert.Equal(Job.MaxFailures, failTicks.Count);
        for (int i = 1; i < failTicks.Count; i++) Assert.True(failTicks[i] - failTicks[i - 1] >= Job.RetryCooldown);
        Assert.Equal(Job.MaxFailures, sim.Counters.JobsFailed);
        Assert.Equal(0, sim.Counters.JobsCompleted);
        Assert.Null(sim.Jobs.Get(job.Id));
        Assert.Equal(DesignationMark.DigUnreachable, sim.Designations.Get(bump));
        Assert.False(sim.Jobs.IsCellReserved(bump));
        Assert.Equal(AgentState.Idle, AgentN(sim, 0).State);
    }

    [Fact]
    public void CellReservation_OneClaimAtATime()
    {
        var sim = Flat(new Int3(5, 5, 5), new Int3(5, 5, 7));
        var shared = new Int3(12, 5, 6);
        var steps = new[] { JobStep.GoTo(shared), JobStep.Work(shared, 20) };
        var j1 = sim.Jobs.Post(JobKind.Dig, shared, steps, new[] { Reservation.OnCell(shared) });
        var j2 = sim.Jobs.Post(JobKind.Dig, shared, steps, new[] { Reservation.OnCell(shared) });

        RunUntil(sim, () => sim.Jobs.Count == 0, 600, () => Assert.False(j1.IsClaimed && j2.IsClaimed));
        Assert.Equal(2, sim.Counters.JobsCompleted);
    }

    [Fact]
    public void PileAndStorageReservations_ArePreconditions()
    {
        var sim = Flat(new Int3(5, 5, 5), new Int3(5, 5, 7));
        var log = TestContent.Db.Item("log");
        var water = TestContent.Db.Item("water");
        var stone = TestContent.Db.Item("stone");
        var pile = new Int3(8, 5, 8);
        sim.Piles.Add(pile, log, 3);
        var hub = sim.Buildings.PlacePrebuilt(TestContent.Db.Building("hub"), new Int3(20, 5, 20), 0);
        sim.Tick();   // regions
        var a1 = AgentN(sim, 0);

        var take2 = new[] { JobStep.GoTo(pile), JobStep.PickUp(pile, log, 2) };
        var p1 = sim.Jobs.Post(JobKind.Haul, pile, take2, new[] { Reservation.FromPile(pile, log, 2) });
        var p2 = sim.Jobs.Post(JobKind.Haul, pile, take2, new[] { Reservation.FromPile(pile, log, 2) });
        JobRunner.Claim(sim, a1, p1);
        Assert.Equal(2, sim.Jobs.ReservedFromPile(pile));
        Assert.False(sim.Jobs.CanReserve(sim, p2));   // 3 - 2 < 2

        var drink = sim.Jobs.Post(JobKind.Haul, hub.EntranceCell, new[] { JobStep.GoToBuilding(hub.Id) },
            new[] { Reservation.OutOfStorage(hub.Id, water, 1) });
        Assert.False(sim.Jobs.CanReserve(sim, drink));   // hub holds no water
        hub.Stored[water.Value] = 1;
        Assert.True(sim.Jobs.CanReserve(sim, drink));

        hub.Stored[stone.Value] = 99;                    // per-item cap 100
        var fill = sim.Jobs.Post(JobKind.Haul, hub.EntranceCell, new[] { JobStep.GoToBuilding(hub.Id) },
            new[] { Reservation.IntoStorage(hub.Id, stone, 2) });
        Assert.False(sim.Jobs.CanReserve(sim, fill));
        hub.Stored[stone.Value] = 98;
        Assert.True(sim.Jobs.CanReserve(sim, fill));
    }

    [Fact]
    public void NeedJob_PreemptsReleasesAndDropsCarried()
    {
        var sim = Flat(new Int3(5, 5, 5));
        var a = AgentN(sim, 0);
        var work = PostWork(sim, JobKind.Haul, new Int3(25, 5, 25));
        RunUntil(sim, () => work.IsClaimed && a.Move == MoveStatus.Moving, 20);
        sim.RunTicks(9);
        var log = TestContent.Db.Item("log");
        a.Carried = new ItemStack(log, 3);

        var need = JobRunner.AssignNeed(sim, a, JobKind.Drink, new Int3(5, 5, 5),
            new[] { JobStep.GoTo(new Int3(5, 5, 5), GoalMode.Exact), JobStep.Work(new Int3(5, 5, 5), 3) });
        Assert.NotNull(need);
        Assert.Equal(need!.Id, a.CurrentJob);
        Assert.Equal(a.Id, need.ClaimedBy);
        Assert.False(work.IsClaimed);
        Assert.Equal(0, work.Failures);
        Assert.Equal(0, work.RetryAfterTick);
        Assert.False(sim.Jobs.IsCellReserved(new Int3(25, 5, 25)));
        Assert.True(a.Carried.IsEmpty);
        Assert.Equal(new ItemStack(log, 3), sim.Piles.At(a.Cell));
        Assert.Equal(a.Cell, a.NextCell);

        Assert.Null(JobRunner.AssignNeed(sim, a, JobKind.Eat, a.Cell, new[] { JobStep.Work(a.Cell, 1) }));
        RunUntil(sim, () => sim.Jobs.Get(need.Id) is null, 200);
        RunUntil(sim, () => work.IsClaimed, 20);          // the released job is picked up again
        Assert.Equal(1, sim.Counters.JobsCompleted);
    }

    [Fact]
    public void Cancel_ClaimedJob_AgentGoesIdle()
    {
        var sim = Flat(new Int3(5, 5, 5));
        var a = AgentN(sim, 0);
        var cell = new Int3(20, 5, 20);
        var job = PostWork(sim, JobKind.Dig, cell);
        RunUntil(sim, () => job.IsClaimed, 20);

        JobRunner.Cancel(sim, job);
        Assert.Null(sim.Jobs.Get(job.Id));
        Assert.False(sim.Jobs.IsCellReserved(cell));
        Assert.False(a.CurrentJob.IsValid);
        Assert.Equal(AgentState.Idle, a.State);
        Assert.Equal(MoveStatus.None, a.Move);
        sim.RunTicks(20);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    [Fact]
    public void IdleAgent_SearchesAtMostOncePer5Ticks()
    {
        var sim = Flat(new Int3(5, 5, 5));
        var a = AgentN(sim, 0);
        sim.RunTicks(3);                          // searched at tick 0 (nothing), next search at tick 5
        Assert.Equal(5, a.NextJobSearchTick);
        var job = PostWork(sim, JobKind.Dig, new Int3(8, 5, 5));
        sim.Tick();                               // tick 3
        sim.Tick();                               // tick 4
        Assert.False(job.IsClaimed);
        sim.Tick();                               // tick 5
        Assert.True(job.IsClaimed);
    }

    /// <summary>BLD-10 with a total cap (warehouse 150): room reserved for one item counts against every item.</summary>
    [Fact]
    public void StorageInReservations_CountAgainstTotalCapacity()
    {
        var sim = Flat(new Int3(5, 5, 5));
        var a = AgentN(sim, 0);
        var wh = sim.Buildings.PlacePrebuilt(TestContent.Db.Building("warehouse"), new Int3(20, 5, 20), 0);
        var log = TestContent.Db.Item("log");
        var stone = TestContent.Db.Item("stone");
        var berries = TestContent.Db.Item("berries");
        wh.Stored[log.Value] = 140;
        Job Into(ItemId item, int n) => sim.Jobs.Post(JobKind.Haul, wh.EntranceCell,
            new[] { JobStep.GoToBuilding(wh.Id) }, new[] { Reservation.IntoStorage(wh.Id, item, n) });

        JobRunner.Claim(sim, a, Into(stone, 6));
        Assert.Equal(6, sim.Jobs.ReservedInTotal(wh.Id));
        Assert.Equal(4, sim.Jobs.StorageRoom(wh, berries));
        Assert.False(sim.Jobs.CanReserve(sim, Into(berries, 5)));
        Assert.True(sim.Jobs.CanReserve(sim, Into(berries, 4)));
    }

    [Fact]
    public void JobBoard_IsInStateHash()
    {
        var s1 = Flat(new Int3(5, 5, 5));
        var s2 = Flat(new Int3(5, 5, 5));
        Assert.Equal(s1.StateHash(), s2.StateHash());
        PostWork(s1, JobKind.Dig, new Int3(8, 5, 5));
        Assert.NotEqual(s1.StateHash(), s2.StateHash());
        PostWork(s2, JobKind.Dig, new Int3(8, 5, 5));
        Assert.Equal(s1.StateHash(), s2.StateHash());
        s1.Designations.Set(new Int3(3, 3, 3), DesignationMark.Dig);
        Assert.NotEqual(s1.StateHash(), s2.StateHash());
    }

    /// <summary>BLD-10 (M6-T3 fix): a PickUpFromStorage step uses up its job's StorageOut promise, so the items still in
    /// the building are free for other jobs while this one carries its stack on. Before the fix the promise stayed
    /// until the job ended and a second job reserving the rest failed its pickup.</summary>
    [Fact]
    public void PickUpFromStorage_UsesUpItsStorageOutReservation()
    {
        var sim = new ScenarioBuilder().Ground(4).Hub(new Int3(2, 5, 20)).Stock("log", 4).Agent(new Int3(6, 5, 16)).Build();
        var hub = sim.Buildings.All.First();
        var log = TestContent.Db.Item("log");
        var far = new Int3(20, 5, 10);
        var job = sim.Jobs.Post(JobKind.Haul, hub.EntranceCell,
            new[] { JobStep.GoToBuilding(hub.Id), JobStep.PickUpFromStorage(hub.Id, log, 2), JobStep.GoTo(far), JobStep.Work(far, 500) },
            new[] { Reservation.OutOfStorage(hub.Id, log, 2) });
        var a = AgentN(sim, 0);
        RunUntil(sim, () => a.Carried.Count == 2, 300);
        Assert.Equal(job.Id, a.CurrentJob);
        Assert.Equal(2, WorldActions.StoredCount(hub, log));
        Assert.DoesNotContain(job.Reservations, r => r.Kind == ReservationKind.StorageOut);
        Assert.Equal(2, sim.Jobs.StorageStock(hub, log));
    }
}
