using Aurvangar.Sim.Core;
using Aurvangar.Sim.Plants;

namespace Aurvangar.Sim.Designations;

/// <summary>M4-T15 (G2 answer 2, ADR-038): dig marks around a tree that stands on a dig-marked floor. The floor itself
/// waits until the plant is gone (DSG-03). So that the tree stays in reach of a chopper (ARCH-07: one level up or
/// down), digs below the floor wait too while the tree is marked for chopping and not given up: a cell at horizontal
/// (Chebyshev) distance r ≥ 1 from the floor is held when its y ≤ floor.y − r. What may be dug meanwhile forms 1-high
/// steps down from the floor, so a dwarf can always climb to a cell beside the tree's base.</summary>
public static class TreeFloors
{
    /// <summary>Floors of standing trees marked for chopping (not given up) whose floor holds a <c>Dig</c> mark, in
    /// ascending plant id order. Null when there are none.</summary>
    public static List<Int3>? Waiting(Simulation sim)
    {
        List<Int3>? floors = null;
        foreach (var p in sim.Plants.All)
        {
            if (p.Kind != PlantKind.Tree || !p.MarkedForChop || p.ChopUnreachable) continue;
            var floor = p.Base + Int3.Down;
            if (sim.Designations.Get(floor) == DesignationMark.Dig) (floors ??= new List<Int3>()).Add(floor);
        }
        return floors;
    }

    /// <summary>True when a dig of <paramref name="c"/> must wait for one of the <paramref name="floors"/>' trees.</summary>
    public static bool Holds(List<Int3>? floors, Int3 c)
    {
        if (floors is null) return false;
        foreach (var f in floors)
        {
            int r = Math.Max(Math.Abs(c.X - f.X), Math.Abs(c.Z - f.Z));
            if (r >= 1 && c.Y <= f.Y - r) return true;
        }
        return false;
    }
}
