using System.Numerics;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.ViewCore.Picking;

/// <summary>A picked solid cell and the face that was hit (VIEW-05).</summary>
public readonly record struct PickHit(Int3 Cell, Int3 Normal)
{
    /// <summary>The empty cell in front of the hit face (where a block would be placed).</summary>
    public Int3 Adjacent => Cell + Normal;
}

/// <summary>Turns a ray hit on chunk collision into a cell and face normal (VIEW-05). The engine does the ray cast;
/// this snaps the hit normal to the dominant axis and steps <see cref="Depth"/> back along it, into the solid cell. The
/// step is less than a 0.25 m sub-cell, so a hit on a fine shape's inner face (a slab's top at 0.5, a stair's riser; VIEW-27)
/// resolves to the shaped cell.</summary>
public static class PickResolver
{
    /// <summary>How far behind the hit point the solid cell is sampled (cells).</summary>
    public const float Depth = 0.1f;

    /// <summary>Returns null when the cell is outside the world or above <paramref name="sliceY"/> (slicing on:
    /// picks above the view level are ignored).</summary>
    public static PickHit? Resolve(VoxelWorld world, Vector3 point, Vector3 normal, int sliceY)
    {
        var n = SnapNormal(normal);
        var inside = point - new Vector3(n.X, n.Y, n.Z) * Depth;
        var cell = new Int3((int)MathF.Floor(inside.X), (int)MathF.Floor(inside.Y), (int)MathF.Floor(inside.Z));
        if (!world.InBounds(cell) || cell.Y > sliceY) return null;
        return new PickHit(cell, n);
    }

    /// <summary>The axis unit vector closest to <paramref name="normal"/> (ties prefer Y, then X).</summary>
    public static Int3 SnapNormal(Vector3 normal)
    {
        float ax = MathF.Abs(normal.X), ay = MathF.Abs(normal.Y), az = MathF.Abs(normal.Z);
        if (ay >= ax && ay >= az) return normal.Y >= 0 ? Int3.Up : Int3.Down;
        if (ax >= az) return normal.X >= 0 ? Int3.East : Int3.West;
        return normal.Z >= 0 ? Int3.South : Int3.North;
    }
}
