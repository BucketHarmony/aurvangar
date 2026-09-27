using System.Numerics;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Tools;

/// <summary>A paint drag of single cells in one grid plane (VIEW-21, M9-T1, ADR-067; vertical since M10-T3). The block
/// tool and the Deconstruct tool use it. The first cell fixes the plane. Each later cursor position is a cell in that
/// plane, and every cell on the face-connected path from the previous cursor cell to it is painted once, in order.
/// Engine-neutral: the Godot layer feeds it a mouse ray (<see cref="MoveRay"/>) or a pick (<see cref="MoveTo"/>).
/// <list type="bullet">
/// <item>Horizontal (the default): the cells stay on the first cell's layer (<see cref="LayerY"/>). The cursor ray is
/// cut with the horizontal plane of the first picked face (<see cref="PlaneOf"/>): the face's own plane for a top or
/// bottom face, and mid-layer for a side face. So the cells stay under the cursor even where the ground below is higher
/// or lower than the layer.</item>
/// <item>Vertical (M10-T3, a drag started on a side face): the cells stay in the vertical plane of the first cell,
/// parallel to the picked face (X fixed for an east or west face, Z fixed for a north or south face). The cursor ray is
/// cut with the picked face's own plane, so a wall face is painted in one drag: a column, or a rectangle the cursor
/// sweeps over.</item>
/// <item>Paths are 4-connected in the plane (<see cref="Line4(Int3, Int3, int)"/>), so neighbouring painted cells share
/// a face: support (CON-09) passes along a painted course or up a painted column, and a painted wall holds water.</item>
/// </list></summary>
public sealed class PaintDrag
{
    /// <summary>At most this many cells in one drag; later cells are ignored.</summary>
    public const int MaxCells = 1024;

    /// <summary>A cursor jump longer than this (Chebyshev, in cells) is ignored: a ray almost parallel to the plane.</summary>
    public const int MaxStep = 64;

    private readonly List<Int3> _cells = new();
    private readonly HashSet<Int3> _set = new();
    private readonly int _fixed;   // the axis held constant: 0 = X, 1 = Y, 2 = Z
    private readonly int _fixedValue;
    private Int3 _cursor;

    /// <summary>Starts a drag at <paramref name="first"/> (painted at once), picked on the face of <paramref name="hit"/>.
    /// With <paramref name="vertical"/> and a side-face pick the drag paints the face's vertical plane (M10-T3);
    /// otherwise the first cell's layer.</summary>
    public PaintDrag(PickHit hit, Int3 first, bool vertical = false)
    {
        _fixed = vertical && hit.Normal.Y == 0 ? (hit.Normal.X != 0 ? 0 : 2) : 1;
        _fixedValue = Axis(first, _fixed);
        LayerY = first.Y;
        Plane = _fixed == 1 ? PlaneOf(hit) : FacePlane(Axis(hit.Cell, _fixed), Axis(hit.Normal, _fixed));
        _cursor = first;
        Add(first);
    }

    /// <summary>True for a drag in a vertical plane (M10-T3).</summary>
    public bool Vertical => _fixed != 1;

    /// <summary>The layer of the first cell; every cell of a horizontal drag is on it.</summary>
    public int LayerY { get; }

    /// <summary>The coordinate of the plane the cursor ray is cut with, on the fixed axis (Y for a horizontal drag, X or
    /// Z for a vertical one).</summary>
    public float Plane { get; }

    /// <summary>The painted cells, in paint order, each once.</summary>
    public IReadOnlyList<Int3> Cells => _cells;

    /// <summary>Changes whenever a cell is painted (for ghost caches).</summary>
    public int Version => _cells.Count;

    /// <summary>The horizontal cursor plane of a pick: the face plane of a top (y + 1) or bottom (y) face, else the
    /// middle of the picked cell's layer.</summary>
    public static float PlaneOf(PickHit hit) =>
        hit.Normal.Y > 0 ? hit.Cell.Y + 1 : hit.Normal.Y < 0 ? hit.Cell.Y : hit.Cell.Y + 0.5f;

    private static float FacePlane(int cell, int normal) => normal > 0 ? cell + 1 : cell;

    /// <summary>The cell in the drag's plane under a ray: where it meets <see cref="Plane"/>. Null when the ray is
    /// parallel to the plane or points away from it.</summary>
    public Int3? OnPlane(Vector3 origin, Vector3 direction)
    {
        float d = Axis(direction, _fixed);
        if (MathF.Abs(d) < 1e-5f) return null;
        float t = (Plane - Axis(origin, _fixed)) / d;
        if (t < 0 || float.IsNaN(t) || float.IsInfinity(t)) return null;
        var p = origin + direction * t;
        if (MathF.Abs(p.X) > 1e6f || MathF.Abs(p.Y) > 1e6f || MathF.Abs(p.Z) > 1e6f) return null;
        var cell = new Int3((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y), (int)MathF.Floor(p.Z));
        return With(cell, _fixed, _fixedValue);
    }

    /// <summary>The cursor ray moved: paints along the plane to the cell under it. True when a cell was painted.</summary>
    public bool MoveRay(Vector3 origin, Vector3 direction) =>
        OnPlane(origin, direction) is { } cell && MoveTo(cell);

    /// <summary>The cursor is over <paramref name="target"/> (its coordinate on the fixed axis is ignored: the drag stays
    /// in its plane). Paints every cell of the face-connected path from the last cursor cell. True when a cell was
    /// painted.</summary>
    public bool MoveTo(Int3 target)
    {
        var t = With(target, _fixed, _fixedValue);
        if (Math.Max(Math.Max(Math.Abs(t.X - _cursor.X), Math.Abs(t.Y - _cursor.Y)), Math.Abs(t.Z - _cursor.Z)) > MaxStep)
            return false;
        bool changed = false;
        foreach (var c in Line4(_cursor, t, _fixed)) changed |= Add(c);
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
    public static IEnumerable<Int3> Line4(Int3 a, Int3 b) => Line4(a, b, 1);

    /// <summary>The 4-connected grid path in the plane where axis <paramref name="fixedAxis"/> (0 = X, 1 = Y, 2 = Z)
    /// keeps <paramref name="a"/>'s value, from <paramref name="a"/> (excluded) to <paramref name="b"/> (included). On
    /// the XZ plane ties step along Z; on a vertical plane they step along Y.</summary>
    public static IEnumerable<Int3> Line4(Int3 a, Int3 b, int fixedAxis)
    {
        // u, v: the free axes. Horizontal: u = X, v = Z. Vertical: u = the horizontal free axis, v = Y.
        int u = fixedAxis == 0 ? 2 : 0, v = fixedAxis == 1 ? 2 : 1;
        int nu = Math.Abs(Axis(b, u) - Axis(a, u)), nv = Math.Abs(Axis(b, v) - Axis(a, v));
        int su = Math.Sign(Axis(b, u) - Axis(a, u)), sv = Math.Sign(Axis(b, v) - Axis(a, v));
        var c = a;
        for (int iu = 0, iv = 0; iu < nu || iv < nv;)
        {
            // Compare (0.5 + iu) / nu with (0.5 + iv) / nv without division.
            if (iv >= nv || (iu < nu && (1 + 2 * iu) * nv < (1 + 2 * iv) * nu)) { c = With(c, u, Axis(c, u) + su); iu++; }
            else { c = With(c, v, Axis(c, v) + sv); iv++; }
            yield return c;
        }
    }

    private static int Axis(Int3 c, int axis) => axis == 0 ? c.X : axis == 1 ? c.Y : c.Z;

    private static float Axis(Vector3 c, int axis) => axis == 0 ? c.X : axis == 1 ? c.Y : c.Z;

    private static Int3 With(Int3 c, int axis, int value) =>
        axis == 0 ? c with { X = value } : axis == 1 ? c with { Y = value } : c with { Z = value };
}
