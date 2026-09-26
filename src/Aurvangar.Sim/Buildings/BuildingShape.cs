using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Buildings;

/// <summary>BLD-01 geometry of a building definition at an origin and rotation, before or after it is placed.
/// Offsets are defined at rotation 0 and rotated about the origin cell (<see cref="Int3.RotateY"/>).</summary>
public static class BuildingShape
{
    /// <summary>BLD-01: the only valid rotations.</summary>
    public static bool IsValidRotation(int rotation) => rotation is 0 or 90 or 180 or 270;

    /// <summary>Footprint cells in world space, deterministic order (local y, z, x).</summary>
    public static IEnumerable<Int3> Footprint(BuildingDef def, Int3 origin, int rotation)
    {
        int fx = def.Footprint[0], fy = def.Footprint[1], fz = def.Footprint[2];
        for (int y = 0; y < fy; y++)
            for (int z = 0; z < fz; z++)
                for (int x = 0; x < fx; x++)
                    yield return origin + new Int3(x, y, z).RotateY(rotation);
    }

    /// <summary>Footprint cells of the bottom layer only (local y = 0), same order as <see cref="Footprint"/>.</summary>
    public static IEnumerable<Int3> BottomLayer(BuildingDef def, Int3 origin, int rotation)
    {
        int fx = def.Footprint[0], fz = def.Footprint[2];
        for (int z = 0; z < fz; z++)
            for (int x = 0; x < fx; x++)
                yield return origin + new Int3(x, 0, z).RotateY(rotation);
    }

    /// <summary>The standable cell agents use (BLD-01: the entrance offset rotates with the building).</summary>
    public static Int3 Entrance(BuildingDef def, Int3 origin, int rotation) =>
        origin + new Int3(def.Entrance[0], def.Entrance[1], def.Entrance[2]).RotateY(rotation);

    /// <summary>BLD-03: the cell in front of the intake side, the side opposite the entrance. At rotation 0 the
    /// entrance lies outside the footprint along one axis; the front cell is on the far side of the footprint along
    /// that axis, level with the bottom layer, with the other coordinate clamped into the footprint (ADR-040).</summary>
    public static Int3 IntakeFront(BuildingDef def, Int3 origin, int rotation)
    {
        int fx = def.Footprint[0], fz = def.Footprint[2];
        int ex = def.Entrance[0], ez = def.Entrance[2];
        Int3 local;
        if (ez < 0) local = new Int3(Math.Clamp(ex, 0, fx - 1), 0, fz);
        else if (ez >= fz) local = new Int3(Math.Clamp(ex, 0, fx - 1), 0, -1);
        else if (ex < 0) local = new Int3(fx, 0, Math.Clamp(ez, 0, fz - 1));
        else if (ex >= fx) local = new Int3(-1, 0, Math.Clamp(ez, 0, fz - 1));
        else throw new InvalidOperationException($"Building '{def.Id}': entrance {ex},{ez} lies inside its footprint.");
        return origin + local.RotateY(rotation);
    }

    /// <summary>BLD-03: the intake cell is the front cell one level down; the pump reads its water level.</summary>
    public static Int3 Intake(BuildingDef def, Int3 origin, int rotation) => IntakeFront(def, origin, rotation) + Int3.Down;
}
