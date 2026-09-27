using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;

namespace Aurvangar.Sim.Buildings;

/// <summary>M5-T4 pump upkeep (BLD-13, BLD-14; ADR-042), ARCH-01 step 7 after <see cref="Construction"/>. For every
/// complete producer it refreshes NoWater from the intake level, keeps one OperatePump job while the buffer has room
/// and the intake has water, and keeps one buffer Haul job while the unpromised buffer holds at least <c>haulAt</c>.
/// The cycle itself is <see cref="WorldActions.Work"/> on the building. Stateless: the state is on the buildings
/// (<see cref="Building.Progress"/> = cycle ticks, <see cref="Building.Stored"/> = buffer) and the job board.</summary>
public static class Pumps
{
    public static void Tick(Simulation sim)
    {
        var operate = new SortedDictionary<int, Job>();   // pump id -> its OperatePump job (lowest job id)
        var haul = new SortedDictionary<int, Job>();      // pump id -> its buffer haul (lowest job id)
        List<Job>? cancel = null;
        foreach (var j in sim.Jobs.All)
        {
            bool op = IsOperate(j), bh = IsBufferHaul(j);
            if (!op && !bh) continue;
            var pump = sim.Buildings.Get(PumpOf(j));
            bool live = pump is { State: BuildingState.Complete, Def.Producer: not null };
            var map = op ? operate : haul;
            // A worker on a pump that is gone or being torn down stops, and so does a haul that has not picked up yet;
            // a haul already carrying the water finishes its delivery.
            if (!live && (op || !j.IsClaimed || NotPickedUp(sim, j))) { (cancel ??= new()).Add(j); continue; }
            if (!live) continue;
            if (map.ContainsKey(pump!.Id.Value)) { if (!j.IsClaimed) (cancel ??= new()).Add(j); continue; }
            map[pump.Id.Value] = j;
        }
        if (cancel is not null)
            foreach (var j in cancel) JobRunner.Cancel(sim, j);

        foreach (var b in sim.Buildings.All)
        {
            if (b.Def.Producer is not { } p) continue;
            if (b.State != BuildingState.Complete) { b.NoWater = false; continue; }   // flagged only while it can run
            b.NoWater = sim.Water.GetLevel(BuildingShape.Intake(b.Def, b.Origin, b.Rotation)) < p.MinIntakeLevel;
            KeepOperate(sim, b, p, operate.TryGetValue(b.Id.Value, out var o) ? o : null);
            KeepHaul(sim, b, p, haul.TryGetValue(b.Id.Value, out var h) ? h : null);
        }
    }

    /// <summary>The OperatePump Work step is over: the pump is gone or no longer complete, its buffer is full, or its
    /// intake is dry (NoWater). Checked before each work tick, so a worker never works a pump in any other state.</summary>
    public static bool WorkDone(Simulation sim, Job job)
    {
        var b = sim.Buildings.Get(PumpOf(job));
        if (b is not { State: BuildingState.Complete, Def.Producer: { } p }) return true;
        return b.NoWater || WorldActions.StoredCount(b, sim.Content.Item(p.Output)) >= p.Buffer;
    }

    /// <summary>An OperatePump job: <c>GoTo(entrance) → Work(pump)</c>.</summary>
    public static bool IsOperate(Job job) =>
        job.Kind == JobKind.OperatePump && job.Steps.Count == 2 && job.Steps[1].Kind == StepKind.Work
        && job.Steps[1].Goal == GoalMode.Building;

    /// <summary>A buffer haul: <c>GoTo(pump) → PickUpFromStorage(pump) → GoTo(storage) → DeliverTo(storage)</c>. Only
    /// this class posts Haul jobs that pick up from a building (pile hauls pick up from a cell, construction deliveries
    /// are Deliver jobs), so the shape alone identifies it, also after its pump is gone.</summary>
    public static bool IsBufferHaul(Job job) =>
        job.Kind == JobKind.Haul && job.Steps.Count == 4 && job.Steps[1].Kind == StepKind.PickUpFromStorage
        && job.Steps[3].Kind == StepKind.DeliverTo;

    /// <summary>A claimed haul whose agent has not done the pick-up step yet.</summary>
    private static bool NotPickedUp(Simulation sim, Job job) =>
        sim.Agents.Get(job.ClaimedBy) is not { } a || a.CurrentJob != job.Id || a.StepIndex <= 1;

    private static BuildingId PumpOf(Job job) => new(job.Steps[1].Target);

    /// <summary>BLD-13: one OperatePump job while the buffer has room and the intake has water. An unclaimed one is
    /// withdrawn otherwise; a claimed one ends by <see cref="WorkDone"/>.</summary>
    private static void KeepOperate(Simulation sim, Building b, ProducerDef p, Job? job)
    {
        bool want = !b.NoWater && WorldActions.StoredCount(b, sim.Content.Item(p.Output)) < p.Buffer
            && !sim.GiveUps.IsGivenUp(GiveUpSource.Pump, b.Id.Value);   // JOB-12
        var stand = Construction.StandCell(sim, b);   // the entrance, or the cell above it on a stepped bank (ADR-055)
        if (job is not null && !job.IsClaimed && (!want || job.Steps[0].Cell != stand))
        {
            JobRunner.Cancel(sim, job);   // not wanted, or the stand cell moved (the bank step was dug): post it anew
            job = null;
        }
        if (job is null && want)
            sim.Jobs.Post(JobKind.OperatePump, stand,
                new[] { JobStep.GoTo(stand, GoalMode.Exact), JobStep.WorkOn(b.Id, p.CycleTicks) });
    }

    /// <summary>BLD-14: one Haul job while the buffer stock not promised to a claimed haul is at least <c>haulAt</c>.
    /// It takes all of that stock (at most <see cref="Agent.CarryCapacity"/>, and at most the destination's room) to
    /// the nearest complete storage that accepts the output and has room (Manhattan from the pump entrance, ties by
    /// lower id). An unclaimed haul is re-planned every tick and withdrawn when that is no longer possible.</summary>
    private static void KeepHaul(Simulation sim, Building b, ProducerDef p, Job? job)
    {
        if (job is { IsClaimed: true }) return;
        var item = sim.Content.Item(p.Output);
        int stock = sim.Jobs.StorageStock(b, item);
        bool givenUp = sim.GiveUps.IsGivenUp(GiveUpSource.PumpHaul, b.Id.Value);   // JOB-12
        Building? to = stock >= p.HaulAt && !givenUp ? Destination(sim, b, item) : null;
        if (to is null)
        {
            if (job is not null) JobRunner.Cancel(sim, job);
            return;
        }
        int n = Math.Min(Math.Min(stock, Agent.CarryCapacity), sim.Jobs.StorageRoom(to, item));
        var steps = new[]
        {
            JobStep.GoToBuilding(b.Id), JobStep.PickUpFromStorage(b.Id, item, n), JobStep.GoToBuilding(to.Id), JobStep.DeliverTo(to.Id),
        };
        var res = new[] { Reservation.OutOfStorage(b.Id, item, n), Reservation.IntoStorage(to.Id, item, n) };
        if (job is null) { sim.Jobs.Post(JobKind.Haul, b.EntranceCell, steps, res); return; }
        if (job.Steps[1].Count == n && job.Steps[3].Target == to.Id.Value && job.Reservations.SequenceEqual(res)) return;   // ADR-048
        job.Steps.Clear();
        job.Steps.AddRange(steps);
        job.Reservations.Clear();
        job.Reservations.AddRange(res);
    }

    private static Building? Destination(Simulation sim, Building pump, ItemId item)
    {
        Building? best = null;
        int bestDist = int.MaxValue;
        foreach (var s in sim.Buildings.All)   // ascending id: strict < keeps the lower id on a tie
        {
            if (s.State != BuildingState.Complete || s.Def.Storage is null || !sim.Actions.Accepts(s, item)) continue;
            if (sim.Jobs.StorageRoom(s, item) <= 0) continue;
            int d = Manhattan(pump.EntranceCell, s.EntranceCell);
            if (d < bestDist) { best = s; bestDist = d; }
        }
        return best;
    }

    private static int Manhattan(Int3 a, Int3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
}
