using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;

namespace Aurvangar.Sim.Jobs;

/// <summary>Job selection, claiming and step execution for one agent per call (JOB-06..08, ADR-028). Called by
/// <see cref="AgentSystem.Tick"/> in ascending agent id order. Every step that touches the world goes through
/// <see cref="WorldActions"/>; a non-Ok result (or a failed GoTo) fails the job.</summary>
public static class JobRunner
{
    /// <summary>JOB-06: an idle agent looks for a job at most once per this many ticks.</summary>
    public const int SearchInterval = 5;

    /// <summary>DSG-08: ticks a Dig step waits for another agent to leave the block's top before the job fails.</summary>
    public const int DigDeferLimit = 200;

    /// <summary>One tick for a living agent: pick a job if idle, then run the current step.</summary>
    public static void Tick(Simulation sim, Agent a)
    {
        var job = Current(sim, a);
        if (job is null)
        {
            AgentMovement.Advance(sim.PathGrid, sim.Pathfinder, a);   // a move started outside a job
            long now = sim.Clock.Tick;
            if (now < a.NextJobSearchTick) return;
            a.NextJobSearchTick = now + SearchInterval;
            // A stack left over from a failed drop goes down before the agent takes new work.
            if (!a.Carried.IsEmpty && sim.Actions.Drop(a.Id, a.Cell) != ActionResult.Ok) return;
            job = Select(sim, a);
            if (job is null) { StepAside(sim, a); return; }
            Claim(sim, a, job);
        }
        RunStep(sim, a, job);
    }

    /// <summary>JOB-06: among open, unclaimed, non-need jobs past their cooldown, the one with max priority, then min
    /// Manhattan distance from the agent to the job target, then min id, that is region-reachable and whose
    /// reservations (its preconditions) can be taken. Filters run only for a job that would beat the current best,
    /// so the result is the same as filter-then-choose.</summary>
    public static Job? Select(Simulation sim, Agent a)
    {
        long now = sim.Clock.Tick;
        int region = sim.Regions.RegionOf(a.Cell);
        Job? best = null;
        int bestDist = 0;
        foreach (var job in sim.Jobs.All)
        {
            if (job.IsClaimed || job.IsNeed || now < job.RetryAfterTick) continue;
            int dist = Manhattan(a.Cell, job.Target);
            if (best is not null && (job.Priority < best.Priority || (job.Priority == best.Priority && dist >= bestDist)))
                continue;   // ascending id: an equal key never beats an earlier job
            if (!Reachable(sim, job, region) || !sim.Jobs.CanReserve(sim, job)) continue;
            if (job.Kind == JobKind.Dig && sim.Agents.AnyHolds(job.Target + Int3.Up, a.Id)) continue;   // DSG-08
            best = job;
            bestDist = dist;
        }
        return best;
    }

    /// <summary>PTH-13 filter: every GoTo step of the job has a goal cell in the agent's region (an agent never
    /// leaves its region, so one unreachable leg dooms the job). No A* runs here.</summary>
    public static bool Reachable(Simulation sim, Job job, int agentRegion)
    {
        foreach (var step in job.Steps)
        {
            if (step.Kind != StepKind.GoTo) continue;
            if (agentRegion == Paths.Regions.None) return false;
            bool any = false;
            foreach (var g in JobGoals.For(sim, step))
                if (sim.Regions.RegionOf(g) == agentRegion) { any = true; break; }
            if (!any) return false;
        }
        return true;
    }

    /// <summary>Claims a job for an agent: takes its reservations and starts at step 0.</summary>
    public static void Claim(Simulation sim, Agent a, Job job)
    {
        sim.Jobs.TakeReservations(job);
        job.ClaimedBy = a.Id;
        a.CurrentJob = job.Id;
        a.StepIndex = 0;
        a.StepProgress = 0;
        a.State = AgentState.Working;
    }

    /// <summary>JOB-07: posts a need job for this agent and claims it at once, preempting a non-need job (released
    /// back to the board, carried item dropped). Returns null (nothing posted) when the agent already runs a need
    /// job or the job's reservations cannot be taken.</summary>
    public static Job? AssignNeed(Simulation sim, Agent a, JobKind kind, Int3 target, IEnumerable<JobStep> steps,
        IEnumerable<Reservation>? reservations = null)
    {
        var current = Current(sim, a);
        if (!a.IsAlive) return null;
        if (kind is not (JobKind.Drink or JobKind.Eat or JobKind.Flee))
            throw new ArgumentException($"JOB-07: {kind} is not a need job", nameof(kind));
        // A need job is not preempted by another need job, except that Flee (WAT-14) preempts Drink and Eat.
        if (current is { IsNeed: true } && (kind != JobKind.Flee || current.Kind == JobKind.Flee)) return null;
        var job = sim.Jobs.Post(kind, target, steps, reservations);
        if (!sim.Jobs.CanReserve(sim, job)) { sim.Jobs.Remove(job); return null; }
        if (current is not null) Release(sim, a, current);
        Claim(sim, a, job);
        return job;
    }

    /// <summary>JOB-07 preemption: returns the agent's job to the board (a need job, which belongs to this agent
    /// alone, is removed instead) without counting a failure; the agent stops, drops its stack and goes idle.
    /// No-op when the agent has no job.</summary>
    public static void ReleaseCurrent(Simulation sim, Agent a)
    {
        var job = Current(sim, a);
        if (job is not null) Release(sim, a, job);
    }

    /// <summary>DSG-06: removes a job; a claimed job is released first and its agent goes idle.</summary>
    public static void Cancel(Simulation sim, Job job)
    {
        var a = sim.Agents.Get(job.ClaimedBy);
        if (a is not null) Unclaim(sim, a, job);
        else if (job.IsClaimed) { sim.Jobs.ReleaseReservations(job); job.ClaimedBy = default; }
        sim.Jobs.Remove(job);
    }

    private static Job? Current(Simulation sim, Agent a)
    {
        if (!a.CurrentJob.IsValid) return null;
        var job = sim.Jobs.Get(a.CurrentJob);
        if (job is not null && job.ClaimedBy == a.Id) return job;
        ResetAgent(a);
        return null;
    }

    private static void RunStep(Simulation sim, Agent a, Job job)
    {
        var step = job.Steps[a.StepIndex];
        var act = sim.Actions;
        ActionResult r;
        switch (step.Kind)
        {
            case StepKind.GoTo:
                if (a.StepProgress == 0)
                {
                    a.StepProgress = 1;
                    var goals = JobGoals.For(sim, step);
                    if (goals.Count == 0) { Fail(sim, a, job); return; }
                    sim.Agents.MoveTo(sim, a, goals);
                }
                else AgentMovement.Advance(sim.PathGrid, sim.Pathfinder, a, swim: job.Kind == JobKind.Flee);
                if (a.Move == MoveStatus.Arrived) NextStep(sim, a, job);
                else if (a.Move != MoveStatus.Moving) Fail(sim, a, job);   // PTH-16 step failure
                return;
            case StepKind.Work:
                if (job.Kind == JobKind.Dig && a.StepProgress == 0 && DigStrand.Strands(sim, step.Cell, a.Cell))
                {
                    StandDown(sim, a, job);   // M4-T14: the world changed on the way; do not start a stranding dig
                    return;
                }
                if (job.Kind is JobKind.Construct or JobKind.Deconstruct && Buildings.Construction.IsSiteJob(job))
                {
                    BuildWork(sim, a, job, step);
                    return;
                }
                if (Buildings.Pumps.IsOperate(job))
                {
                    PumpWork(sim, a, job, step);
                    return;
                }
                var target = step.Goal == GoalMode.Building
                    ? WorkTarget.AtBuilding(new BuildingId(step.Target)) : WorkTarget.AtCell(step.Cell);
                r = act.Work(a.Id, target);
                if (r == ActionResult.Ok && ++a.StepProgress < step.Ticks) return;
                break;
            case StepKind.Dig:
                if (sim.Agents.AnyHolds(step.Cell + Int3.Up, a.Id))
                {
                    // DSG-08: someone stands on the block; wait for them to move on (idle agents step aside).
                    // Two diggers waiting on each other's floors would wait forever: this one gives way (ADR-043).
                    if (WaitsOnMe(sim, a, step.Cell + Int3.Up)) { StandDown(sim, a, job); return; }
                    if (++a.StepProgress <= DigDeferLimit) return;
                    Fail(sim, a, job);
                    return;
                }
                if (DigStrand.Strands(sim, step.Cell, a.Cell)) { StandDown(sim, a, job); return; }   // M4-T14
                r = act.Dig(a.Id, step.Cell);
                break;
            case StepKind.Chop: r = act.Chop(a.Id, new PlantId(step.Target)); break;
            case StepKind.PickUp: r = act.PickUp(a.Id, step.Cell, step.Item, step.Count); break;
            case StepKind.PickUpFromStorage: r = act.PickUpFromStorage(a.Id, new BuildingId(step.Target), step.Item, step.Count); break;
            case StepKind.DeliverTo: r = act.DeliverTo(a.Id, new BuildingId(step.Target)); break;
            case StepKind.Consume: ConsumeStep(sim, a, job, step); return;
            case StepKind.Drop: r = act.Drop(a.Id, step.Cell); break;
            default: throw new InvalidOperationException($"unknown step kind {step.Kind}");
        }
        if (r != ActionResult.Ok) Fail(sim, a, job);
        else NextStep(sim, a, job);
    }

    /// <summary>M5-T2 (BLD-08/09): building work runs until the site is complete or the building is gone, not for a
    /// fixed count (progress lives on the building, so a new worker carries on). A deconstruction's last tick stands
    /// down when removing the building would cut the worker off from the Great Hall (M4-T14), and waits like DSG-08
    /// while an agent is on top of it.</summary>
    private static void BuildWork(Simulation sim, Agent a, Job job, JobStep step)
    {
        var b = sim.Buildings.Get(new BuildingId(step.Target));
        if (job.Kind == JobKind.Deconstruct && b is { State: Buildings.BuildingState.Deconstructing }
            && b.Progress + 1 >= Buildings.Construction.DeconstructTicks(b.Def)
            && Buildings.Construction.WouldStrand(sim, b, a.Cell))
        {
            StandDown(sim, a, job);
            return;
        }
        var r = sim.Actions.Work(a.Id, WorkTarget.AtBuilding(new BuildingId(step.Target)));
        if (r == ActionResult.Blocked && job.Kind == JobKind.Deconstruct && ++a.StepProgress <= DigDeferLimit) return;
        if (r != ActionResult.Ok) { Fail(sim, a, job); return; }
        a.StepProgress = 0;   // progress lives on the building; StepProgress only counts a blocked wait
        if (Buildings.Construction.WorkDone(sim, job)) NextStep(sim, a, job);
    }

    /// <summary>M5-T4 (BLD-13): the worker runs production cycles until the pump's buffer is full, its intake is dry
    /// or it stops being a complete pump (<see cref="Buildings.Pumps.WorkDone"/>, checked before every tick); the job
    /// is then done. Cycle progress lives on the building.</summary>
    private static void PumpWork(Simulation sim, Agent a, Job job, JobStep step)
    {
        if (Buildings.Pumps.WorkDone(sim, job)) { NextStep(sim, a, job); return; }
        var r = sim.Actions.Work(a.Id, WorkTarget.AtBuilding(new BuildingId(step.Target)));
        if (r != ActionResult.Ok) { Fail(sim, a, job); return; }
        if (Buildings.Pumps.WorkDone(sim, job)) NextStep(sim, a, job);
    }

    /// <summary>DSG-08 deadlock: an agent standing on <paramref name="top"/> is itself at the Dig step of a job whose
    /// block is the floor of <paramref name="a"/>.</summary>
    private static bool WaitsOnMe(Simulation sim, Agent a, Int3 top)
    {
        foreach (var other in sim.Agents.All)
        {
            if (!other.IsAlive || other.Id == a.Id || other.Cell != top) continue;
            if (sim.Jobs.Get(other.CurrentJob) is { Kind: JobKind.Dig } j && j.ClaimedBy == other.Id
                && j.Steps[other.StepIndex].Kind == StepKind.Dig && j.Steps[other.StepIndex].Cell == a.Cell + Int3.Down)
                return true;
        }
        return false;
    }

    /// <summary>ECO-05: one unit per tick until the need is sated; when the storage runs out after at least one unit
    /// the job simply ends. A first unit that cannot be had fails the job (JOB-08; NeedsSystem tries again later).</summary>
    private static void ConsumeStep(Simulation sim, Agent a, Job job, JobStep step)
    {
        var b = new BuildingId(step.Target);
        var r = sim.Actions.Consume(a.Id, b, step.Item);
        if (r == ActionResult.Ok)
        {
            sim.Jobs.UseStorageOut(job, b, step.Item, 1);
            a.StepProgress++;
            if (NeedsSystem.IsSated(a, job.Kind)) NextStep(sim, a, job);
            return;
        }
        if (r == ActionResult.NotEnoughItems && a.StepProgress > 0) NextStep(sim, a, job);
        else Fail(sim, a, job);
    }

    private static void NextStep(Simulation sim, Agent a, Job job)
    {
        a.StepIndex++;
        a.StepProgress = 0;
        if (a.StepIndex < job.Steps.Count) return;
        sim.Jobs.ReleaseReservations(job);
        job.ClaimedBy = default;
        sim.Jobs.Remove(job);
        if (job.Kind == JobKind.Dig && sim.Designations.Get(job.Target) == DesignationMark.Dig)
            sim.Designations.Set(job.Target, DesignationMark.None);   // DSG-07
        sim.Counters.JobsCompleted++;
        ResetAgent(a);
    }

    /// <summary>JOB-08: release, count the failure, and either return the job with a cooldown or, at the fifth
    /// failure, cancel it and mark its designation unreachable.</summary>
    private static void Fail(Simulation sim, Agent a, Job job)
    {
        sim.Counters.JobsFailed++;
        job.Failures++;
        Unclaim(sim, a, job);
        if (job.IsNeed)
        {
            sim.Jobs.Remove(job);   // ADR-031: per-agent need jobs are re-posted by their owner, never retried
            NeedsSystem.OnFailed(sim, a, job.Kind);
        }
        else if (job.Failures >= Job.MaxFailures)
        {
            sim.Jobs.Remove(job);   // cancelled
            if (job.Kind == JobKind.Dig) sim.Designations.MarkUnreachable(job.Target);
            else if (job.Kind == JobKind.Chop && sim.Plants.Get(DesignationSystem.ChopTree(job)) is { } tree)
                tree.ChopUnreachable = true;
        }
        else job.RetryAfterTick = sim.Clock.Tick + Job.RetryCooldown;
    }

    /// <summary>M4-T14: the dig would now cut this agent off from the Great Hall. The job goes back to the board
    /// without a failure (it is not broken, it has to wait) and with the JOB-08 cooldown, so it is not re-taken at
    /// once; <see cref="JobGoals"/> no longer offers this stand cell. DesignationSystem marks it unreachable once no
    /// other dig can help.</summary>
    private static void StandDown(Simulation sim, Agent a, Job job)
    {
        Unclaim(sim, a, job);
        job.RetryAfterTick = sim.Clock.Tick + Job.RetryCooldown;
    }

    private static void Release(Simulation sim, Agent a, Job job)
    {
        Unclaim(sim, a, job);
        if (job.IsNeed) sim.Jobs.Remove(job);
    }

    /// <summary>Returns the job to the board unclaimed with its reservations released; the agent stops, drops what it
    /// carries on its cell (ECO-08 spiral; if that is blocked it keeps the stack and retries when it next looks for
    /// work) and goes idle.</summary>
    private static void Unclaim(Simulation sim, Agent a, Job job)
    {
        sim.Jobs.ReleaseReservations(job);
        job.ClaimedBy = default;
        AgentMovement.Halt(a);
        if (!a.Carried.IsEmpty) sim.Actions.Drop(a.Id, a.Cell);
        ResetAgent(a);
    }

    /// <summary>DSG-08 companion (ADR-029): an idle agent with nothing to do that stands on a block marked for digging
    /// walks to a nearby cell of its region (5×3×5 box) whose floor is not marked, so the dig is not deferred forever.</summary>
    private static void StepAside(Simulation sim, Agent a)
    {
        if (a.Move == MoveStatus.Moving || sim.Designations.Get(a.Cell + Int3.Down) != DesignationMark.Dig) return;
        int region = sim.Regions.RegionOf(a.Cell);
        if (region == Paths.Regions.None) return;
        var goals = new List<Int3>();
        for (int dy = -1; dy <= 1; dy++)
            for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    var c = a.Cell + new Int3(dx, dy, dz);
                    if (c == a.Cell || !sim.PathGrid.IsWalkable(c) || sim.Regions.RegionOf(c) != region) continue;
                    if (sim.Designations.Get(c + Int3.Down) != DesignationMark.Dig) goals.Add(c);
                }
        if (goals.Count > 0) sim.Agents.MoveTo(sim, a, goals);
    }

    private static void ResetAgent(Agent a)
    {
        a.CurrentJob = default;
        a.StepIndex = 0;
        a.StepProgress = 0;
        a.State = AgentState.Idle;
    }

    private static int Manhattan(Int3 a, Int3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
}
