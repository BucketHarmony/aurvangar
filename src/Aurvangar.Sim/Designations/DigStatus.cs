using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;

namespace Aurvangar.Sim.Designations;

/// <summary>DSG-10 (M11-T8): why a dig-marked cell is (not yet) being dug. <see cref="None"/>: no live mark.</summary>
public enum DigWait : byte
{
    None,
    /// <summary>An open dig job a dwarf can take.</summary>
    Queued,
    /// <summary>A dwarf has claimed the dig.</summary>
    Digging,
    /// <summary>Not exposed (DSG-03) and the cell above is marked too: it waits for the cell above.</summary>
    CellAbove,
    /// <summary>Not exposed (DSG-03): it waits for a neighbouring cell to be dug.</summary>
    Neighbour,
    /// <summary>The strand rule (DSG-09, ADR-037/059): digging it now would cut a dwarf off from the Great Hall. Also a
    /// <c>DigUnreachable</c> mark that was given up for that reason.</summary>
    WouldTrap,
    /// <summary>A plant stands on it, or a tree marked for chopping holds it (DSG-03).</summary>
    Plant,
    /// <summary>A built block rests on it (CON-10).</summary>
    Support,
    /// <summary>No living dwarf can reach a cell to dig it from, or it was given up (JOB-08).</summary>
    Unreachable,
}

/// <summary>DSG-10 (M11-T8, G6 follow-up "no explanation"): the reason a dig mark waits, for the view's hover text. A
/// pure query over the simulation: it reads the marks, the job board, regions and the strand trials (whose caches are
/// derived, never hashed), and changes no sim state.</summary>
public static class DigStatus
{
    public static DigWait Of(Simulation sim, Int3 cell)
    {
        var world = sim.World;
        var mark = sim.Designations.Get(cell);
        if (mark == DesignationMark.None || !world.IsSolid(cell)) return DigWait.None;
        if (mark == DesignationMark.DigUnreachable)
            return JobGoals.DigBlockedByStrand(sim, cell) ? DigWait.WouldTrap : DigWait.Unreachable;

        Job? job = null;
        foreach (var j in sim.Jobs.All)
            if (j.Kind == JobKind.Dig && j.Target == cell) { job = j; break; }
        if (job is { IsClaimed: true }) return DigWait.Digging;
        if (job is null)
        {
            if (!DesignationSystem.Exposed(world, cell))
                return sim.Designations.Get(cell + Int3.Up) == DesignationMark.Dig ? DigWait.CellAbove : DigWait.Neighbour;
            if (sim.Plants.IsOccupied(cell + Int3.Up) || TreeFloors.Holds(TreeFloors.Waiting(sim), cell)) return DigWait.Plant;
            if (Blocks.Support.Depends(sim, cell)) return DigWait.Support;
            return DigWait.Queued;   // posted at the next designation step
        }
        if (JobGoals.DigBlockedByStrand(sim, cell) || DigStrand.StrandsOthers(sim, cell, default)) return DigWait.WouldTrap;
        if (Blocks.Support.Depends(sim, cell)) return DigWait.Support;
        if (!AnyAgentReaches(sim, job)) return DigWait.Unreachable;
        return DigWait.Queued;
    }

    /// <summary>PTH-13: some living dwarf's region holds a goal cell of the dig's GoTo step.</summary>
    private static bool AnyAgentReaches(Simulation sim, Job job)
    {
        var regions = new SortedSet<int>();
        foreach (var a in sim.Agents.All)
            if (a.IsAlive && sim.Regions.RegionOf(a.Cell) is var r && r != Paths.Regions.None) regions.Add(r);
        foreach (var g in JobGoals.For(sim, job.Steps[0]))
            if (regions.Contains(sim.Regions.RegionOf(g))) return true;
        return false;
    }
}
