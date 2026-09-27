using System.Numerics;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Meshing;

namespace Aurvangar.ViewCore.Entities;

/// <summary>How a block-tool cell beyond the free stock is drawn (M10-T2, VIEW-21, ADR-072): an amber fill
/// (`designations.short`) with a darker amber outline. It is short, not invalid: unlike <see cref="InvalidCellStyle"/>
/// it is depth-tested (it sits in the ghost's normal mesh), and its colour is kept apart from red, the deconstruct
/// orange and the palette block colours.</summary>
public static class ShortCellStyle
{
    public const float FillAlpha = 0.6f;
    /// <summary>How far the outline colour is from the fill toward black.</summary>
    public const float EdgeDarken = 0.45f;
    public const float EdgeWidth = 0.07f;
    /// <summary>A fill box (6 quads) and 12 edge bars (6 quads each).</summary>
    public const int QuadsPerCell = 6 + 12 * 6;

    public static Vector4 Fill(EntityColors entities) => entities.Short with { W = FillAlpha };

    public static Vector4 Edge(EntityColors entities) =>
        Vector4.Lerp(entities.Short, new Vector4(0, 0, 0, 1), EdgeDarken) with { W = 1f };

    /// <summary>Adds one short cell, its box grown by <paramref name="pad"/>.</summary>
    public static void Add(MeshData mesh, Int3 c, float pad, EntityColors entities)
    {
        var lo = new Vector3(c.X - pad, c.Y - pad, c.Z - pad);
        var hi = new Vector3(c.X + 1 + pad, c.Y + 1 + pad, c.Z + 1 + pad);
        MeshShapes.AddBox(mesh, lo, hi, Fill(entities));
        MeshShapes.AddEdges(mesh, lo, hi, EdgeWidth, Edge(entities));
    }
}
