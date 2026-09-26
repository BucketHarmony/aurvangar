using Aurvangar.Sim.Jobs;

namespace Aurvangar.Sim.Agents;

/// <summary>WAT-14 flee and drowning (ADR-031), run for each living agent before its job step. An agent standing in
/// deep water drops whatever it was doing and flees along the <see cref="Paths.Pathfinder.FindFlee"/> path (a Flee
/// need job, JOB-05 priority 200). When no walkable cell is within 64 steps it is trapped: it takes 1 damage per tick
/// and searches again every <see cref="JobRunner.SearchInterval"/> ticks (a failed flee path searches at once), until the water drops, a way out opens, or it
/// dies (<see cref="DeathCause.Drowned"/>).</summary>
public static class FleeRules
{
    /// <summary>Returns true when the agent's turn is used up (trapped or dead); false lets its job step run.</summary>
    public static bool Tick(Simulation sim, Agent a)
    {
        if (!sim.PathGrid.IsStandable(a.Cell) || !sim.Water.IsDeep(a.Cell))
        {
            if (a.State == AgentState.Trapped) a.State = AgentState.Idle;   // the water dropped
            return false;
        }
        if (sim.Jobs.Get(a.CurrentJob) is { Kind: JobKind.Flee } flee && flee.ClaimedBy == a.Id) return false;

        // The first search is immediate (the idle job-search throttle does not apply); only a trapped agent waits.
        long now = sim.Clock.Tick;
        if (a.State != AgentState.Trapped || now >= a.NextJobSearchTick)
        {
            var r = sim.Pathfinder.FindFlee(a.Cell);
            if (r.Found)
            {
                var dry = r.Path[^1];
                if (JobRunner.AssignNeed(sim, a, JobKind.Flee, dry, new[] { JobStep.GoTo(dry, GoalMode.Exact) }) is null)
                    return false;                 // cannot happen today (a Flee job has no reservations)
                a.StepProgress = 1;               // the GoTo step's path is the flee path, already found
                AgentMovement.Begin(a, r.Path);
                return false;
            }
            a.NextJobSearchTick = now + JobRunner.SearchInterval;
        }

        // Trapped: nothing else happens this tick.
        JobRunner.ReleaseCurrent(sim, a);
        AgentMovement.Halt(a);
        a.State = AgentState.Trapped;
        a.Health--;
        if (a.Health <= 0) sim.Agents.Kill(sim, a, DeathCause.Drowned);
        return true;
    }
}
