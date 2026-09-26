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
    public IEnumerable<Int3> FootprintCells()
    {
        int fx = Def.Footprint[0], fy = Def.Footprint[1], fz = Def.Footprint[2];
        for (int y = 0; y < fy; y++)
            for (int z = 0; z < fz; z++)
                for (int x = 0; x < fx; x++)
                    yield return Origin + new Int3(x, y, z).RotateY(Rotation);
    }

    public Int3 EntranceCell => Origin + new Int3(Def.Entrance[0], Def.Entrance[1], Def.Entrance[2]).RotateY(Rotation);
}
