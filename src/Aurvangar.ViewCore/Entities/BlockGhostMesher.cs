using System.Numerics;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Tools;

namespace Aurvangar.ViewCore.Entities;

/// <summary>The block tool's ghost (VIEW-21, M8-T5): one translucent cube per shape cell, in the block's palette colour
/// where CON-08 allows it and red where it does not. Slightly larger than a cell, so it shows over plan ghosts.</summary>
public static class BlockGhostMesher
{
    public const float Alpha = 0.55f;
    public const float PlanAlpha = 0.35f;
    public const float BadAlpha = 0.5f;
    public const float Inflate = 0.02f;

    public static Vector4 ColorOf(GhostCell cell, BlockGhost ghost, BlockColors blocks, EntityColors entities)
    {
        if (!cell.Ok) return entities.Unreachable with { W = BadAlpha };
        var c = blocks.Get(ghost.Command.Block);
        return ghost.Command.Plan
            ? Vector4.Lerp(c, Vector4.One, PlanGhostMesher.PlannedLighten) with { W = PlanAlpha }
            : c with { W = Alpha };
    }

    public static MeshData Build(BlockGhost ghost, BlockColors blocks, EntityColors entities)
    {
        var mesh = new MeshData();
        foreach (var g in ghost.Cells)
        {
            var c = g.Cell;
            MeshShapes.AddBox(mesh, new Vector3(c.X - Inflate, c.Y - Inflate, c.Z - Inflate),
                new Vector3(c.X + 1 + Inflate, c.Y + 1 + Inflate, c.Z + 1 + Inflate), ColorOf(g, ghost, blocks, entities));
        }
        return mesh;
    }
}
