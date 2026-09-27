using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Buildings;

public enum BuildingState : byte { Blueprint, UnderConstruction, Complete, Deconstructing }

public enum PlacementResult : byte
{
    Ok,
    OutOfBounds,
    Overlaps,
    NotOnGround,
    FootprintBlocked,
    EntranceBlocked,
    NeedsWaterEdge,
    PrebuiltOnly,
    BadRotation,
    /// <summary>CON-08 (M8-T2): a footprint, entrance or stand cell holds a block plan entry.</summary>
    PlannedBlocks,
    /// <summary>ADR-076 (M11-T1): a building with no entrance has no standable cell in reach of its footprint.</summary>
    NoStandCell,
}

/// <summary>A placed building instance. Spec: docs/specs/buildings.md.</summary>
public sealed class Building
{
    public BuildingId Id { get; init; }
    public BuildingDef Def { get; init; } = null!;
    public Int3 Origin { get; init; }
    public int Rotation { get; init; }
    public BuildingState State { get; set; }
    public int Progress { get; set; }

    /// <summary>Delivered construction materials by ItemId.Value (sorted for hashing).</summary>
    public SortedDictionary<int, int> Delivered { get; } = new();

    /// <summary>Stored items by ItemId.Value (BLD-10).</summary>
    public SortedDictionary<int, int> Stored { get; } = new();

    public bool NoWater { get; set; }

    /// <summary>CRF-06 (M11-T4): a workshop's orders, at most one per recipe, in ascending recipe index. Empty for any
    /// other building.</summary>
    public List<WorkshopOrder> Orders { get; } = new();

    /// <summary>The workshop's order for this recipe index, or null.</summary>
    public WorkshopOrder? OrderFor(int recipe)
    {
        foreach (var o in Orders)
            if (o.Recipe == recipe) return o;
        return null;
    }

    /// <summary>Footprint cells in world space, deterministic order (y, z, x).</summary>
    public IEnumerable<Int3> FootprintCells() => BuildingShape.Footprint(Def, Origin, Rotation);

    /// <summary>The entrance cell (BLD-01). Only for a building whose definition has one; see <see cref="JobCell"/>.</summary>
    public Int3 EntranceCell => BuildingShape.Entrance(Def, Origin, Rotation);

    public bool HasEntrance => Def.HasEntrance;

    /// <summary>The cell the building's jobs are posted at and distances are measured from: the entrance, or the origin
    /// for a building with no entrance (the levee, ADR-076).</summary>
    public Int3 JobCell => Def.HasEntrance ? EntranceCell : Origin;

    /// <summary>True when the cell is one of this building's footprint cells.</summary>
    public bool Covers(Int3 c)
    {
        foreach (var f in FootprintCells())
            if (f == c) return true;
        return false;
    }
}
