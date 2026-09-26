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
            if (job is null) return;
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
        if (current is { IsNeed: true } || !a.IsAlive) return null;
        if (kind is not (JobKind.Drink or JobKind.Eat or JobKind.Flee))
            throw new ArgumentException($"JOB-07: {kind} is not a need job", nameof(kind));
        var job = sim.Jobs.Post(kind, target, steps, reservations);
        if (!sim.Jobs.CanReserve(sim, job)) { sim.Jobs.Remove(job); return null; }
        if (current is not null) Unclaim(sim, a, current);
        Claim(sim, a, job);
        return job;
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
                else AgentMovement.Advance(sim.PathGrid, sim.Pathfinder, a);
                if (a.Move == MoveStatus.Arrived) NextStep(sim, a, job);
                else if (a.Move != MoveStatus.Moving) Fail(sim, a, job);   // PTH-16 step failure
                return;
            case StepKind.Work:
                var target = step.Goal == GoalMode.Building
                    ? WorkTarget.AtBuilding(new BuildingId(step.Target)) : WorkTarget.AtCell(step.Cell);
                r = act.Work(a.Id, target);
                if (r == ActionResult.Ok && ++a.StepProgress < step.Ticks) return;
                break;
            case StepKind.Dig: r = act.Dig(a.Id, step.Cell); break;
            case StepKind.Chop: r = act.Chop(a.Id, new PlantId(step.Target)); break;
            case StepKind.PickUp: r = act.PickUp(a.Id, step.Cell, step.Item, step.Count); break;
            case StepKind.PickUpFromStorage: r = act.PickUpFromStorage(a.Id, new BuildingId(step.Target), step.Item, step.Count); break;
            case StepKind.DeliverTo: r = act.DeliverTo(a.Id, new BuildingId(step.Target)); break;
            case StepKind.Consume: r = act.Consume(a.Id, new BuildingId(step.Target), step.Item); break;
            case StepKind.Drop: r = act.Drop(a.Id, step.Cell); break;
            default: throw new InvalidOperationException($"unknown step kind {step.Kind}");
        }
        if (r != ActionResult.Ok) Fail(sim, a, job);
        else NextStep(sim, a, job);
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
        if (job.Failures >= Job.MaxFailures)
        {
            sim.Jobs.Remove(job);   // cancelled
            if (job.Kind == JobKind.Dig) sim.Designations.MarkUnreachable(job.Target);
            // M4-T7: a chop job that gives up marks its tree (DSG-05 has no unreachable state yet).
        }
        else job.RetryAfterTick = sim.Clock.Tick + Job.RetryCooldown;
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

    private static void ResetAgent(Agent a)
    {
        a.CurrentJob = default;
        a.StepIndex = 0;
        a.StepProgress = 0;
        a.State = AgentState.Idle;
    }

    private static int Manhattan(Int3 a, Int3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
}
