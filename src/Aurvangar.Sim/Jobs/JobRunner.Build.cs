using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Jobs;

/// <summary>CON-13/14 (M8-T2): running Build jobs.</summary>
public static partial class JobRunner
{
    /// <summary>CON-13 skipping: when the GoTo of a cell starts, the cell must still have a released entry of the job's
    /// block whose status, ignoring the job's own hold, is Ready, Occupied or NoMaterial. Otherwise its GoTo, Work and
    /// Place steps are skipped (not a failure; the job may complete with nothing placed). True when skipped.</summary>
    private static bool SkipsCell(Simulation sim, Agent a, Job job, JobStep step)
    {
        var block = BlockBuildSystem.BlockOf(job);
        bool keep = false;
        if (sim.Plans.Get(step.Cell) is { State: PlanState.Released } e && e.Block == block)
        {
            var status = BlockPlans.StatusOf(new BuildScan(sim, held: null, self: job), step.Cell, e, a.Id, out _);
            keep = status is BuildStatus.Ready or BuildStatus.Occupied or BuildStatus.NoMaterial;
        }
        if (keep) return false;
        a.StepIndex += 2;   // onto the cell's Place step; NextStep moves past it
        NextStep(sim, a, job);
        return true;
    }

    /// <summary>CON-15: only a Build job that placed at least one of its blocks counts as a success for its give-up
    /// mark; one whose cells were all skipped does not clear it.</summary>
    private static bool PlacedAny(Simulation sim, Job job)
    {
        foreach (var s in job.Steps)
            if (s.Kind == StepKind.Place && sim.World.GetBlock(s.Cell) == (BlockId)s.Target) return true;
        return false;
    }

    /// <summary>CON-13 re-goal (ADR-062): a Build GoTo whose move failed because its goal cell stopped being walkable
    /// (typically another builder's block went into it) starts its step again: the next tick re-checks the cell and
    /// picks fresh stand cells. Anything else fails as usual. True when restarted.</summary>
    private static bool RestartsBuildGoTo(Simulation sim, Agent a, JobStep step, Int3? goal)
    {
        if (step.Goal != GoalMode.Build || goal is not { } g || sim.PathGrid.IsWalkable(g)) return false;
        AgentMovement.Halt(a);
        a.StepProgress = 0;
        return true;
    }

    /// <summary>CON-13 Place: the strand rule first (stand down, no failure); an agent in the cell or its headroom is
    /// waited for up to <see cref="DigDeferLimit"/> ticks, then the job fails; any other non-Ok result fails it. Ok
    /// removes the entry.</summary>
    private static void PlaceStep(Simulation sim, Agent a, Job job, JobStep step)
    {
        if (PlaceStrandsAny(sim, a, step.Cell)) { StandDown(sim, a, job); return; }
        var r = sim.Actions.PlaceBlock(a.Id, step.Cell, (BlockId)step.Target);
        if (r == ActionResult.Blocked
            && (sim.Agents.AnyHolds(step.Cell, a.Id) || sim.Agents.AnyHolds(step.Cell + Int3.Down, a.Id)))
        {
            if (++a.StepProgress <= DigDeferLimit) return;
            Fail(sim, a, job);
            return;
        }
        if (r != ActionResult.Ok) { Fail(sim, a, job); return; }
        sim.Plans.Remove(step.Cell);
        NextStep(sim, a, job);
    }

    /// <summary>CON-14 at Work start and at Place: the block would cut the builder or another dwarf off from the hall.</summary>
    private static bool PlaceStrandsAny(Simulation sim, Agent a, Int3 cell) =>
        PlaceStrand.Strands(sim, cell, a.Cell) || PlaceStrand.StrandsOthers(sim, cell, a.Id);

    /// <summary>CON-14, the last filter of <see cref="Select"/>: a held cell of the job would cut another dwarf off.</summary>
    private static bool BuildStrandsOthers(Simulation sim, Job job, Agent a)
    {
        foreach (var s in job.Steps)
            if (s.Kind == StepKind.Place && PlaceStrand.StrandsOthers(sim, s.Cell, a.Id)) return true;
        return false;
    }

    /// <summary>CON-13 step-aside: the cell is a released entry's cell or the cell below one (its headroom).</summary>
    private static bool InEntryWay(Simulation sim, Int3 c)
    {
        if (sim.Plans.Count == 0) return false;
        return sim.Plans.Get(c) is { State: PlanState.Released } || sim.Plans.Get(c + Int3.Up) is { State: PlanState.Released };
    }
}
