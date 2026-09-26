using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Jobs;

/// <summary>M4-T14, G2 answer 1b (ADR-037): a dwarf does not take or finish a dig that would cut its standing cell
/// off from the Great Hall. The connectivity test is <see cref="Paths.DigTrial"/>; this class supplies the hall's
/// reach cells as anchors. Without a complete hall there is nothing to be cut off from, so the rule is off.</summary>
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
        if (!sim.Regions.IsDirty)
        {
            int region = sim.Regions.RegionOf(stand);
            bool together = false;
            if (region != Paths.Regions.None)
                foreach (var c in anchors)
                    if (sim.Regions.RegionOf(c) == region) { together = true; break; }
            if (!together) return false;
        }
        else if (!sim.DigTrial.ConnectedNow(stand, anchors)) return false;   // apart already: nothing to cut
        return sim.DigTrial.Cuts(target, stand, anchors);
    }
}
