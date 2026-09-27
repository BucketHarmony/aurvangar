using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Jobs;

/// <summary>CON-14 (M8-T2, ADR-062): no placement walls a dwarf in. The mirror of <see cref="DigStrand"/>, with the
/// same Great Hall anchors and <see cref="Paths.PlaceTrial"/> as the what-if test. Anchors equal to the placed cell or
/// the cell below it are left out of the what-if flood (they are not walkable on the what-if view, so the flood never
/// seeds from them). Without a complete hall the rule is off.</summary>
public static class PlaceStrand
{
    /// <summary>True if placing a block at <paramref name="target"/> would leave <paramref name="stand"/> unable to
    /// reach the hall although it can now.</summary>
    public static bool Strands(Simulation sim, Int3 target, Int3 stand)
    {
        if (!sim.PlaceTrial.MaySplit(target)) return false;   // cheap, cached: most placements cut nothing
        var anchors = Anchors(sim, out _);
        if (anchors is null) return false;
        if (!DigStrand.ReachesHallNow(sim, stand, anchors)) return false;   // apart already: nothing to cut
        return sim.PlaceTrial.Cuts(target, stand, anchors);
    }

    /// <summary>True if placing a block at <paramref name="target"/> would cut a living dwarf other than
    /// <paramref name="except"/> off from the hall. A dwarf counts by its cell and, mid-step, the cell it steps into.
    /// A dwarf in the placed cell or the one below it does not count: it blocks the placement instead (CON-13).</summary>
    public static bool StrandsOthers(Simulation sim, Int3 target, AgentId except)
    {
        if (!sim.PlaceTrial.MaySplit(target)) return false;
        List<Int3>? anchors = null;
        Building? hall = null;
        foreach (var a in sim.Agents.All)
        {
            if (!a.IsAlive || a.Id == except) continue;
            if (anchors is null)
            {
                anchors = Anchors(sim, out hall);
                if (anchors is null) return false;
            }
            if (CutsCell(sim, target, hall!, anchors, a.Cell)) return true;
            if (a.NextCell != a.Cell && CutsCell(sim, target, hall!, anchors, a.NextCell)) return true;
        }
        return false;
    }

    /// <summary>CON-13/14 companion to <see cref="JobRunner"/>'s step-aside: an idle dwarf whose cell a released entry's
    /// placement would cut off from the hall walks towards the hall, so the entry does not wait on it forever. Entries
    /// are scanned rather than jobs because such an entry is <c>WouldStrand</c> and never gets a job. Ascending cell
    /// index. Returns true when it started walking.</summary>
    public static bool StepOut(Simulation sim, Agent a)
    {
        if (sim.Plans.Count == 0) return false;
        Building? hall = null;
        List<Int3>? anchors = null;
        foreach (var (cell, e) in sim.Plans.All)
        {
            if (e.State != PlanState.Released || !sim.PlaceTrial.MaySplit(cell)) continue;
            if (anchors is null)
            {
                anchors = Anchors(sim, out hall);
                if (anchors is null) return false;
            }
            if (!CutsCell(sim, cell, hall!, anchors, a.Cell)) continue;
            return sim.Agents.MoveTo(sim, a, anchors) == Paths.PathStatus.Found && a.Move == MoveStatus.Moving;
        }
        return false;
    }

    /// <summary>The hall's reach cells, or null when there is no complete hall or it has none.</summary>
    private static List<Int3>? Anchors(Simulation sim, out Building? hall)
    {
        hall = DigStrand.Hall(sim);
        if (hall is null) return null;
        var anchors = JobGoals.For(sim, JobStep.GoToBuilding(hall.Id));
        return anchors.Count == 0 ? null : anchors;
    }

    private static bool CutsCell(Simulation sim, Int3 target, Building hall, IReadOnlyList<Int3> anchors, Int3 cell) =>
        sim.PlaceTrial.MayCut(target, anchors, hall.Id.Value, cell) && DigStrand.ReachesHallNow(sim, cell, anchors);
}
