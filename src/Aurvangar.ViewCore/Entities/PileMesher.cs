using System.Numerics;
using Aurvangar.Sim;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Items;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Entities;

/// <summary>Item piles (VIEW-10): a small stack of cubes in the item's palette color, one cube per
/// <see cref="ItemsPerCube"/> items (at least 1, at most <see cref="MaxCubes"/>), laid out 2×2 per layer on the
/// floor of the pile's cell. All piles go into one mesh in world coordinates. The count is shown as a hover label.</summary>
public static class PileMesher
{
    public const int ItemsPerCube = 5;
    public const int MaxCubes = 8;
    public const float CubeSize = 0.3f;
    /// <summary>Offset of a cube center from the cell center on X and Z.</summary>
    public const float CubeOffset = 0.17f;

    public static int CubesFor(int count) => Math.Clamp((count + ItemsPerCube - 1) / ItemsPerCube, 1, MaxCubes);

    /// <summary>Piles whose cell is above <paramref name="sliceY"/> are hidden (VIEW-04).</summary>
    public static MeshData Build(ItemPiles piles, int sliceY, EntityColors colors)
    {
        var mesh = new MeshData();
        foreach (var (cell, stack) in piles.All)
        {
            if (cell.Y > sliceY || stack.IsEmpty) continue;
            var color = colors.Item(stack.Item);
            int cubes = CubesFor(stack.Count);
            for (int i = 0; i < cubes; i++)
            {
                int layer = i / 4, slot = i % 4;
                float cx = cell.X + 0.5f + (slot % 2 == 0 ? -CubeOffset : CubeOffset);
                float cz = cell.Z + 0.5f + (slot / 2 == 0 ? -CubeOffset : CubeOffset);
                float y0 = cell.Y + layer * CubeSize;
                float h = CubeSize / 2f;
                MeshShapes.AddBox(mesh, new Vector3(cx - h, y0, cz - h), new Vector3(cx + h, y0 + CubeSize, cz + h), color);
            }
        }
        return mesh;
    }

    /// <summary>The pile under a pick: on top of the picked block, else in front of the picked face. Null if none
    /// or if its cell is above the slice.</summary>
    public static (Int3 Cell, ItemStack Stack)? AtPick(ItemPiles piles, PickHit hit, int sliceY)
    {
        foreach (var c in new[] { hit.Cell + Int3.Up, hit.Adjacent })
        {
            if (c.Y > sliceY) continue;
            var s = piles.At(c);
            if (!s.IsEmpty) return (c, s);
        }
        return null;
    }

    /// <summary>Hover label, e.g. "Stone ×12".</summary>
    public static string Label(ContentDb content, ItemStack stack) =>
        $"{content.ItemDef(stack.Item).Name} ×{stack.Count}";
}
