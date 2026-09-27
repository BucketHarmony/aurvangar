using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Blocks;

/// <summary>CON-09: no floating blocks. Placement support is local (a solid block below or beside); plan support
/// widens "solid" to cells that have an entry or are pending in the same command, transitively.</summary>
public static class Support
{
    /// <summary>CON-09 cap on the cells one plan-support flood visits; over it only direct support counts.</summary>
    public const int PlanFloodCap = 16384;

    private static readonly Int3[] DownAndSides = { Int3.Down, new(-1, 0, 0), new(1, 0, 0), new(0, 0, -1), new(0, 0, 1) };
    private static readonly Int3[] UpAndSides = { Int3.Up, new(-1, 0, 0), new(1, 0, 0), new(0, 0, -1), new(0, 0, 1) };

    /// <summary>CON-09 placement support: the cell's down neighbor or one of its 4 horizontal neighbors is solid.</summary>
    public static bool Placement(VoxelWorld world, Int3 c)
    {
        foreach (var d in DownAndSides)
            if (world.IsSolid(c + d)) return true;
        return false;
    }

    /// <summary>CON-09 plan support for a command's pending cells, in one flood: the pending cells from which a
    /// down/sideways path through pending cells and existing entries (either state) reaches a solid block. First the
    /// flood collects every planned cell reachable from the pending ones by down/sideways steps; then support spreads
    /// back from the planned cells that touch a solid block, by up/sideways steps. Over
    /// <see cref="PlanFloodCap"/> collected cells nothing counts as supported (CON-09). Returns cell indices.</summary>
    public static HashSet<int> PlanSupported(Simulation sim, IReadOnlyList<Int3> pending)
    {
        var world = sim.World;
        var plans = sim.Plans;
        var pendingSet = new HashSet<int>();   // lookups only
        foreach (var c in pending)
            if (world.InBounds(c)) pendingSet.Add(world.Index(c));

        var universe = new HashSet<int>();     // lookups only; the queue gives the (deterministic) order
        var queue = new List<int>();
        foreach (var c in pending)
        {
            if (!world.InBounds(c)) continue;
            int i = world.Index(c);
            if (universe.Add(i)) queue.Add(i);
        }
        bool over = false;
        for (int head = 0; head < queue.Count && !over; head++)
        {
            var c = world.CellOf(queue[head]);
            foreach (var d in DownAndSides)
            {
                var n = c + d;
                if (!world.InBounds(n) || world.IsSolid(n)) continue;
                int ni = world.Index(n);
                if (universe.Contains(ni) || !(pendingSet.Contains(ni) || plans.HasAt(ni))) continue;
                universe.Add(ni);
                queue.Add(ni);
                if (queue.Count > PlanFloodCap) { over = true; break; }
            }
        }

        var supported = new HashSet<int>();
        if (over) return supported;   // CON-09: over the cap counts as unsupported
        var spread = new List<int>();
        foreach (int i in queue)
            if (Placement(world, world.CellOf(i)) && supported.Add(i)) spread.Add(i);
        for (int head = 0; head < spread.Count; head++)
        {
            var c = world.CellOf(spread[head]);
            foreach (var d in UpAndSides)
            {
                var n = c + d;
                if (!world.InBounds(n)) continue;
                int ni = world.Index(n);
                if (!universe.Contains(ni) || !supported.Add(ni)) continue;
                spread.Add(ni);
            }
        }
        supported.IntersectWith(pendingSet);
        return supported;
    }
}
