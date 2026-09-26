using System.Numerics;
using Aurvangar.Sim;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Plants;
using Aurvangar.ViewCore.Meshing;

namespace Aurvangar.ViewCore.Entities;

/// <summary>Designation overlay (VIEW-11): a translucent orange cube around each dig-marked cell, an orange ring on
/// the ground around each tree marked for chopping. Marks the colonists gave up on (<c>DigUnreachable</c>,
/// <c>ChopUnreachable</c>) are red. One translucent mesh in world coordinates; marks above the slice are hidden.</summary>
public static class DesignationMesher
{
    /// <summary>How far a dig cube sticks out of its cell, so it is not z-fighting with the block faces.</summary>
    public const float DigInflate = 0.03f;
    public const float RingOuter = 0.65f;
    public const float RingInner = 0.45f;
    public const float RingHeight = 0.1f;
    /// <summary>Alpha of the chop ring (mostly opaque; it is thin).</summary>
    public const float RingAlpha = 0.9f;

    public static MeshData Build(Simulation sim, int sliceY, EntityColors colors)
    {
        var mesh = new MeshData();
        foreach (var (c, mark) in sim.Designations.All)
        {
            if (c.Y > sliceY || mark == DesignationMark.None) continue;
            var color = (mark == DesignationMark.DigUnreachable ? colors.Unreachable : colors.Dig) with { W = EntityColors.DigAlpha };
            var lo = new Vector3(c.X - DigInflate, c.Y - DigInflate, c.Z - DigInflate);
            var hi = new Vector3(c.X + 1 + DigInflate, c.Y + 1 + DigInflate, c.Z + 1 + DigInflate);
            MeshShapes.AddBox(mesh, lo, hi, color);
        }
        foreach (var p in sim.Plants.All)
        {
            if (p.Kind != PlantKind.Tree || !p.MarkedForChop || p.Base.Y > sliceY) continue;
            var color = (p.ChopUnreachable ? colors.Unreachable : colors.Chop) with { W = RingAlpha };
            AddRing(mesh, p.Base, color);
        }
        return mesh;
    }

    /// <summary>Cheap fingerprint of everything <see cref="Build"/> draws, so the view rebuilds the overlay only when
    /// a mark changes (designations emit no events).</summary>
    public static ulong Signature(Simulation sim)
    {
        var h = StateHasher.Create();
        foreach (var (c, mark) in sim.Designations.All) { h.Add(c); h.Add((byte)mark); }
        h.Add(-1);
        foreach (var p in sim.Plants.All)
            if (p.MarkedForChop) { h.Add(p.Id.Value); h.Add(p.ChopUnreachable); }
        return h.Value;
    }

    /// <summary>Square ring of four flat boxes around the tree base, sitting on the ground.</summary>
    private static void AddRing(MeshData m, Int3 b, Vector4 color)
    {
        float cx = b.X + 0.5f, cz = b.Z + 0.5f, y0 = b.Y + 0.01f, y1 = b.Y + 0.01f + RingHeight;
        float o = RingOuter, i = RingInner;
        MeshShapes.AddBox(m, new Vector3(cx - o, y0, cz - o), new Vector3(cx + o, y1, cz - i), color);   // north
        MeshShapes.AddBox(m, new Vector3(cx - o, y0, cz + i), new Vector3(cx + o, y1, cz + o), color);   // south
        MeshShapes.AddBox(m, new Vector3(cx - o, y0, cz - i), new Vector3(cx - i, y1, cz + i), color);   // west
        MeshShapes.AddBox(m, new Vector3(cx + i, y0, cz - i), new Vector3(cx + o, y1, cz + i), color);   // east
    }
}
