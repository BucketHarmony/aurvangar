using System.Numerics;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Tools;

namespace Aurvangar.ViewCore.Entities;

/// <summary>The block tool's ghost (VIEW-21, M8-T5, M9-T1): one translucent cube per painted cell in the block's palette
/// colour where CON-08 allows it (<see cref="Build"/>). Cells it does not allow are a separate mesh
/// (<see cref="BuildInvalid"/>, M9-T3) in the <see cref="InvalidCellStyle"/>, which the view draws without a depth test,
/// so a red cell inside or behind a built block still shows. Slightly larger than a cell, so it shows over plan ghosts.</summary>
public static class BlockGhostMesher
{
    public const float Alpha = 0.55f;
    public const float PlanAlpha = 0.35f;
    public const float Inflate = 0.02f;

    /// <summary>The palette colour of a valid ghost cell (lightened in plan mode).</summary>
    public static Vector4 ColorOf(BlockGhost ghost, BlockColors blocks)
    {
        var c = blocks.Get(ghost.Block);
        return ghost.Plan
            ? Vector4.Lerp(c, Vector4.One, PlanGhostMesher.PlannedLighten) with { W = PlanAlpha }
            : c with { W = Alpha };
    }

    /// <summary>The valid cells: in the palette colour, or amber (<see cref="ShortCellStyle"/>, M10-T2) where the free
    /// stock does not cover them.</summary>
    public static MeshData Build(BlockGhost ghost, BlockColors blocks, EntityColors entities)
    {
        var mesh = new MeshData();
        var color = ColorOf(ghost, blocks);
        foreach (var g in ghost.Cells)
        {
            if (!g.Ok) continue;
            var c = g.Cell;
            if (g.Short) { ShortCellStyle.Add(mesh, c, Inflate, entities); continue; }
            MeshShapes.AddBox(mesh, new Vector3(c.X - Inflate, c.Y - Inflate, c.Z - Inflate),
                new Vector3(c.X + 1 + Inflate, c.Y + 1 + Inflate, c.Z + 1 + Inflate), color);
        }
        return mesh;
    }

    /// <summary>The invalid cells (M9-T3): red fill and dark outline, <see cref="InvalidCellStyle.Inflate"/> out of
    /// the cell.</summary>
    public static MeshData BuildInvalid(BlockGhost ghost, EntityColors entities)
    {
        var mesh = new MeshData();
        foreach (var g in ghost.Cells)
            if (!g.Ok) InvalidCellStyle.Add(mesh, g.Cell, InvalidCellStyle.Inflate, entities);
        return mesh;
    }

    /// <summary>Deconstruct marks (M9-T1): orange, at <see cref="Alpha"/>, over the built blocks a Deconstruct click or
    /// drag would take down.</summary>
    public static readonly Vector4 DeconstructColor = new(1f, 0.45f, 0.15f, Alpha);

    /// <summary>One inflated cube per cell in <paramref name="color"/>.</summary>
    public static MeshData Marks(IReadOnlyList<Aurvangar.Sim.Core.Int3> cells, Vector4 color)
    {
        var mesh = new MeshData();
        foreach (var c in cells)
            MeshShapes.AddBox(mesh, new Vector3(c.X - Inflate, c.Y - Inflate, c.Z - Inflate),
                new Vector3(c.X + 1 + Inflate, c.Y + 1 + Inflate, c.Z + 1 + Inflate), color);
        return mesh;
    }
}
