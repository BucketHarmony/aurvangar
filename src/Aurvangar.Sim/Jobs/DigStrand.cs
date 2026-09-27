using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Jobs;

/// <summary>M4-T14, G2 answer 1b (ADR-037): a dwarf does not take or finish a dig that would cut its standing cell
/// off from the Great Hall. M7-T6, G3 answer 8 (ADR-059): nor one that would cut any other living dwarf off. The
/// connectivity tests are <see cref="Paths.DigTrial"/>; this class supplies the hall's reach cells as anchors. Without
/// a complete hall there is nothing to be cut off from, so the rule is off.</summary>
public static class DigStrand
{
    /// <summary>The Great Hall's building definition id (data/buildings.json).</summary>
    public const string HallDefId = "hub";

    /// <summary>The lowest-id complete Great Hall, or null.</summary>
    public static Building? Hall(Simulation sim)
    {
        foreach (var b in sim.Buildings.All)
            if (b.Def.Id == HallDefId && b.State == BuildingState.Complete) return b;
        return null;
    }

    /// <summary>True if digging <paramref name="target"/> would leave <paramref name="stand"/> unable to reach the
    /// hall although it can now. When the regions are up to date and the stand is not in a region with the hall
    /// already, the dig is allowed (it cannot cut what is apart). Mid-tick, after a change, regions are stale, so a
    /// flood on the live world answers "apart already?" instead.</summary>
    public static bool Strands(Simulation sim, Int3 target, Int3 stand)
    {
        if (!sim.DigTrial.MaySplit(target)) return false;   // cheap, cached: most digs cut nothing
        var hall = Hall(sim);
        if (hall is null) return false;
        var anchors = JobGoals.For(sim, JobStep.GoToBuilding(hall.Id));
        if (anchors.Count == 0) return false;
        if (!ReachesHallNow(sim, stand, anchors)) return false;   // apart already: nothing to cut
        return sim.DigTrial.Cuts(target, stand, anchors);
    }

    /// <summary>M7-T6: true if digging <paramref name="target"/> would cut a living dwarf other than
    /// <paramref name="except"/> (the digger, whose stand cell <see cref="Strands"/> covers) off from the hall. A dwarf
    /// counts by its cell and, mid-step, the cell it steps into. Dwarves already apart from the hall do not count.</summary>
    public static bool StrandsOthers(Simulation sim, Int3 target, AgentId except)
    {
        if (!sim.DigTrial.MaySplit(target)) return false;   // cheap, cached: most digs cut nothing
        var hall = Hall(sim);
        if (hall is null) return false;
        List<Int3>? anchors = null;
        foreach (var a in sim.Agents.All)
        {
            if (!a.IsAlive || a.Id == except) continue;
            anchors ??= JobGoals.For(sim, JobStep.GoToBuilding(hall.Id));
            if (anchors.Count == 0) return false;
            if (CutsCell(sim, target, hall, anchors, a.Cell)) return true;
            if (a.NextCell != a.Cell && CutsCell(sim, target, hall, anchors, a.NextCell)) return true;
        }
        return false;
    }

    /// <summary>M7-T6 companion to <see cref="JobRunner"/>'s step-aside: an idle dwarf with nothing to do whose cell an
    /// open, unclaimed dig would cut off from the hall walks towards the hall, so the dig does not wait on it forever.
    /// Open digs are visited in ascending job id order. Returns true when it started walking.</summary>
    public static bool StepOut(Simulation sim, Agent a)
    {
        Building? hall = null;
        List<Int3>? anchors = null;
        foreach (var j in sim.Jobs.All)
        {
            if (j.Kind != JobKind.Dig || j.IsClaimed || !sim.DigTrial.MaySplit(j.Target)) continue;
            hall ??= Hall(sim);
            if (hall is null) return false;
            anchors ??= JobGoals.For(sim, JobStep.GoToBuilding(hall.Id));
            if (anchors.Count == 0) return false;
            if (!CutsCell(sim, j.Target, hall, anchors, a.Cell)) continue;
            return sim.Agents.MoveTo(sim, a, anchors) == Paths.PathStatus.Found && a.Move == MoveStatus.Moving;
        }
        return false;
    }

    private static bool CutsCell(Simulation sim, Int3 target, Building hall, IReadOnlyList<Int3> anchors, Int3 cell) =>
        sim.DigTrial.MayCut(target, anchors, hall.Id.Value, cell) && ReachesHallNow(sim, cell, anchors);

    /// <summary>Whether <paramref name="cell"/> reaches the hall's anchors now: by region when the regions are up to
    /// date, else by a flood on the live world.</summary>
    private static bool ReachesHallNow(Simulation sim, Int3 cell, IReadOnlyList<Int3> anchors)
    {
        if (sim.Regions.IsDirty) return sim.DigTrial.ConnectedNow(cell, anchors);
        int region = sim.Regions.RegionOf(cell);
        if (region == Paths.Regions.None) return false;
        foreach (var c in anchors)
            if (sim.Regions.RegionOf(c) == region) return true;
        return false;
    }
}
