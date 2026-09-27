using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Physics;

/// <summary>M11-T9 gravity for piles and buildings (docs/specs/gravity.md, ADR-079). ARCH-01 step 10b: finds the
/// buildings that no longer stand and the piles that no longer rest; the writes are
/// <see cref="Actions.WorldActions.Collapse"/> and <see cref="Actions.WorldActions.FallPile"/>. Stateless: falls are
/// instant, so nothing is saved or hashed (GRV-10).</summary>
public static class Gravity
{
    /// <summary>GRV-02. Buildings first, in ascending id, until a pass finds none that fall (GRV-08 cascade); then piles
    /// in ascending cell index.</summary>
    public static void Tick(Simulation sim)
    {
        while (true)
        {
            List<Building>? fall = null;
            foreach (var b in sim.Buildings.All)
                if (!Anchored(b.Def) && !Stands(sim, b, null)) (fall ??= new List<Building>()).Add(b);
            if (fall is null) break;
            foreach (var b in fall) sim.Actions.Collapse(b);
        }

        if (sim.Piles.Count == 0) return;
        List<Int3>? falling = null;
        foreach (var (cell, _) in sim.Piles.All)
            if (!Rests(sim, cell)) (falling ??= new List<Int3>()).Add(cell);
        if (falling is null) return;
        foreach (var c in falling) sim.Actions.FallPile(c);
    }

    /// <summary>GRV-01: the cell below is solid, or the cell is at y = 0.</summary>
    public static bool Rests(Simulation sim, Int3 c) => c.Y == 0 || sim.World.IsSolid(c + Int3.Down);

    /// <summary>GRV-05: the Great Hall (prebuilt-only and not removable) never collapses and is never undermined.</summary>
    public static bool Anchored(BuildingDef def) => def.PrebuiltOnly && !def.RemovableWhenEmpty;

    /// <summary>GRV-05/06: a dig may not remove this building's floor: it is anchored, or not complete (a blueprint, a
    /// site, or a building being deconstructed).</summary>
    public static bool HoldsFloor(Building b) => Anchored(b.Def) || b.State != BuildingState.Complete;

    /// <summary>GRV-06: the cell is the floor of a building whose floor may not be dug.</summary>
    public static bool HoldsFloor(Simulation sim, Int3 cell) =>
        sim.Buildings.BuildingAt(cell + Int3.Up) is { } over && !over.Covers(cell) && HoldsFloor(over);

    /// <summary>GRV-01: some cell under the bottom layer is solid or covered by another building. Cells in
    /// <paramref name="gone"/> (cell indices, a what-if removal) count as neither.</summary>
    public static bool Stands(Simulation sim, Building b, IReadOnlySet<int>? gone)
    {
        var world = sim.World;
        foreach (var c in BuildingShape.BottomLayer(b.Def, b.Origin, b.Rotation))
        {
            var below = c + Int3.Down;
            if (!world.InBounds(below)) return true;
            if (gone is not null && gone.Contains(world.Index(below))) continue;
            if (world.IsSolid(below)) return true;
            if (sim.Buildings.BuildingAt(below) is { } under && under != b) return true;
        }
        return false;
    }

    /// <summary>GRV-09 what-if: the buildings (not anchored) that would collapse if the solid cell
    /// <paramref name="removed"/> turned to air, with the GRV-08 cascade, in the order they would fall. Empty (and
    /// cheap) unless a building stands right on the cell.</summary>
    public static List<Building> WouldCollapse(Simulation sim, Int3 removed)
    {
        var result = new List<Building>();
        var world = sim.World;
        if (!world.InBounds(removed) || sim.Buildings.BuildingAt(removed + Int3.Up) is null) return result;
        var gone = new HashSet<int> { world.Index(removed) };   // lookups only
        var frontier = new List<Int3> { removed };
        while (frontier.Count > 0)
        {
            var next = new List<Int3>();
            foreach (var c in frontier)
            {
                if (sim.Buildings.BuildingAt(c + Int3.Up) is not { } over || over.Covers(c) || result.Contains(over)) continue;
                if (Anchored(over.Def) || Stands(sim, over, gone)) continue;
                result.Add(over);
                foreach (var f in over.FootprintCells())
                    if (world.InBounds(f) && gone.Add(world.Index(f))) next.Add(f);
            }
            frontier = next;
        }
        return result;
    }
}
