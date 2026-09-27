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

    /// <summary>Footprint cells in world space, deterministic order (y, z, x).</summary>
    public IEnumerable<Int3> FootprintCells() => BuildingShape.Footprint(Def, Origin, Rotation);

    public Int3 EntranceCell => BuildingShape.Entrance(Def, Origin, Rotation);

    /// <summary>True when the cell is one of this building's footprint cells.</summary>
    public bool Covers(Int3 c)
    {
        foreach (var f in FootprintCells())
            if (f == c) return true;
        return false;
    }
}
