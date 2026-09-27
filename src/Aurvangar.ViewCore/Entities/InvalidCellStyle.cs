using System.Numerics;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Meshing;

namespace Aurvangar.ViewCore.Entities;

/// <summary>How a cell that cannot be built is drawn (M9-T3): the block tool's red ghost cells (VIEW-21) and stuck
/// plan ghosts (VIEW-22). A strong red fill in the unreachable colour plus a dark, opaque outline along the cube's 12
/// edges, so the cell reads at monument camera distance and against light stone. The fill reaches
/// <see cref="Inflate"/> past its cell, so it shows over a solid block that makes the cell invalid.</summary>
public static class InvalidCellStyle
{
    public const float FillAlpha = 0.8f;
    /// <summary>How far the outline colour is from the fill toward black (0 = fill, 1 = black).</summary>
    public const float EdgeDarken = 0.65f;
    /// <summary>Edge bar thickness (cells).</summary>
    public const float EdgeWidth = 0.1f;
    /// <summary>The tool ghost's red cube reaches this far out of its cell.</summary>
    public const float Inflate = 0.06f;
    /// <summary>A fill box (6 quads) and 12 edge bars (6 quads each).</summary>
    public const int QuadsPerCell = 6 + 12 * 6;

    public static Vector4 Fill(EntityColors entities) => entities.Unreachable with { W = FillAlpha };

    public static Vector4 Edge(EntityColors entities) =>
        Vector4.Lerp(entities.Unreachable, new Vector4(0, 0, 0, 1), EdgeDarken) with { W = 1f };

    /// <summary>Adds one invalid cell, its box grown by <paramref name="pad"/> (negative insets it).</summary>
    public static void Add(MeshData mesh, Int3 c, float pad, EntityColors entities)
    {
        var lo = new Vector3(c.X - pad, c.Y - pad, c.Z - pad);
        var hi = new Vector3(c.X + 1 + pad, c.Y + 1 + pad, c.Z + 1 + pad);
        MeshShapes.AddBox(mesh, lo, hi, Fill(entities));
        MeshShapes.AddEdges(mesh, lo, hi, EdgeWidth, Edge(entities));
    }
}
