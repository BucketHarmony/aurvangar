using System.Numerics;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Tools;

/// <summary>A paint drag of single cells on one layer (VIEW-21, M9-T1, ADR-067). The block tool and the Deconstruct
/// tool use it. The first cell fixes the layer (<see cref="LayerY"/>). Each later cursor position is a cell on that
/// layer, and every cell on the face-connected path from the previous cursor cell to it is painted once, in order.
/// Engine-neutral: the Godot layer feeds it a mouse ray (<see cref="MoveRay"/>) or a pick (<see cref="MoveTo"/>).
/// <list type="bullet">
/// <item>The cursor ray is cut with the horizontal plane of the first picked face (<see cref="PlaneOf"/>): the face's
/// own plane for a top or bottom face, and mid-layer for a side face. So the cells stay under the cursor even where
/// the ground below is higher or lower than the layer.</item>
/// <item>Paths are 4-connected (<see cref="Line4"/>), so neighbouring painted cells share a face: support (CON-09)
/// passes along a painted course, and a painted wall holds water.</item>
/// </list></summary>
public sealed class PaintDrag
{
    /// <summary>At most this many cells in one drag; later cells are ignored.</summary>
    public const int MaxCells = 1024;

    /// <summary>A cursor jump longer than this (Chebyshev, in cells) is ignored: a ray almost parallel to the plane.</summary>
    public const int MaxStep = 64;

    private readonly List<Int3> _cells = new();
    private readonly HashSet<Int3> _set = new();
    private Int3 _cursor;

    /// <summary>Starts a drag at <paramref name="first"/> (painted at once), picked on the face of <paramref name="hit"/>.</summary>
    public PaintDrag(PickHit hit, Int3 first)
    {
        LayerY = first.Y;
        PlaneY = PlaneOf(hit);
        _cursor = first;
        Add(first);
    }

    /// <summary>The layer every painted cell is on.</summary>
    public int LayerY { get; }

    /// <summary>The height of the horizontal plane the cursor ray is cut with.</summary>
    public float PlaneY { get; }

    /// <summary>The painted cells, in paint order, each once.</summary>
    public IReadOnlyList<Int3> Cells => _cells;

    /// <summary>Changes whenever a cell is painted (for ghost caches).</summary>
    public int Version => _cells.Count;

    /// <summary>The cursor plane of a pick: the face plane of a top (y + 1) or bottom (y) face, else the middle of the
    /// picked cell's layer.</summary>
    public static float PlaneOf(PickHit hit) =>
        hit.Normal.Y > 0 ? hit.Cell.Y + 1 : hit.Normal.Y < 0 ? hit.Cell.Y : hit.Cell.Y + 0.5f;

    /// <summary>The cell on the layer under a ray: where it meets <see cref="PlaneY"/>. Null when the ray is parallel to
    /// the plane or points away from it.</summary>
    public Int3? OnPlane(Vector3 origin, Vector3 direction)
    {
        if (MathF.Abs(direction.Y) < 1e-5f) return null;
        float t = (PlaneY - origin.Y) / direction.Y;
        if (t < 0 || float.IsNaN(t) || float.IsInfinity(t)) return null;
        var p = origin + direction * t;
        if (MathF.Abs(p.X) > 1e6f || MathF.Abs(p.Z) > 1e6f) return null;
        return new Int3((int)MathF.Floor(p.X), LayerY, (int)MathF.Floor(p.Z));
    }

    /// <summary>The cursor ray moved: paints along the layer to the cell under it. True when a cell was painted.</summary>
    public bool MoveRay(Vector3 origin, Vector3 direction) =>
        OnPlane(origin, direction) is { } cell && MoveTo(cell);

    /// <summary>The cursor is over <paramref name="target"/> (its Y is ignored: the drag stays on its layer). Paints
    /// every cell of the face-connected path from the last cursor cell. True when a cell was painted.</summary>
    public bool MoveTo(Int3 target)
    {
        var t = target with { Y = LayerY };
        if (Math.Max(Math.Abs(t.X - _cursor.X), Math.Abs(t.Z - _cursor.Z)) > MaxStep) return false;
        bool changed = false;
        foreach (var c in Line4(_cursor, t)) changed |= Add(c);
        _cursor = t;
        return changed;
    }

    private bool Add(Int3 c)
    {
        if (_cells.Count >= MaxCells || !_set.Add(c)) return false;
        _cells.Add(c);
        return true;
    }

    /// <summary>The 4-connected grid path on the XZ plane from <paramref name="a"/> (excluded) to <paramref name="b"/>
    /// (included), at <paramref name="a"/>'s Y. Each step moves one cell along X or Z; there are |dx| + |dz| steps.
    /// At each step it moves along the axis whose next cell border the straight line crosses first (ties: Z).</summary>
    public static IEnumerable<Int3> Line4(Int3 a, Int3 b)
    {
        int nx = Math.Abs(b.X - a.X), nz = Math.Abs(b.Z - a.Z);
        int sx = Math.Sign(b.X - a.X), sz = Math.Sign(b.Z - a.Z);
        int x = a.X, z = a.Z;
        for (int ix = 0, iz = 0; ix < nx || iz < nz;)
        {
            // Compare (0.5 + ix) / nx with (0.5 + iz) / nz without division.
            if (iz >= nz || (ix < nx && (1 + 2 * ix) * nz < (1 + 2 * iz) * nx)) { x += sx; ix++; }
            else { z += sz; iz++; }
            yield return new Int3(x, a.Y, z);
        }
    }
}
