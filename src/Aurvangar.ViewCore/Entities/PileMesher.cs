using System.Globalization;
using System.Numerics;
using Aurvangar.Sim;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Items;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Entities;

/// <summary>One visible item pile (VIEW-10): where it is, what and how many, the text and anchor of its count
/// label, and how many air cells lie under it (<see cref="PostDepth"/>, piles do not fall when their floor is dug).</summary>
public readonly record struct PileMarker(Int3 Cell, ItemId Item, int Count, Vector4 Color, int PostDepth,
    Vector3 LabelAnchor, string Text);

/// <summary>Item piles (VIEW-10, M4-T16, gate G2 answer 3): every pile is one fixed-size marker, whatever its count:
/// a crate most of a cell wide in the item's palette color with a lighter cap, readable at the default hub zoom. A
/// count label floats above it (Godot draws it as a fixed-screen-size billboard while
/// <see cref="LabelsVisible"/> says so). A pile whose floor was dug stays in its cell (ECO-08), so it gets a thin post
/// down to the first solid block below (at most <see cref="MaxPostDepth"/> cells). All piles go into one mesh in
/// world coordinates.</summary>
public static class PileMesher
{
    public const float MarkerInset = 0.13f;
    public const float MarkerHeight = 0.45f;
    public const float CapInset = 0.24f;
    public const float CapHeight = 0.1f;
    public const float PostHalfWidth = 0.08f;
    public const int MaxPostDepth = 8;
    /// <summary>Height of the count label's anchor above the pile cell's floor.</summary>
    public const float LabelHeight = MarkerHeight + CapHeight + 0.35f;
    /// <summary>Count labels show up to this camera distance (the default is 60, the overview 120).</summary>
    public const float LabelMaxDistance = 80f;
    /// <summary>How far the cap's color moves toward white.</summary>
    public const float CapLighten = 0.4f;
    public const float PostDarken = 0.6f;

    public static bool LabelsVisible(float cameraDistance) => cameraDistance <= LabelMaxDistance;

    public static Vector4 CapColor(Vector4 c) =>
        new(c.X + (1 - c.X) * CapLighten, c.Y + (1 - c.Y) * CapLighten, c.Z + (1 - c.Z) * CapLighten, c.W);

    public static Vector4 PostColor(Vector4 c) => new(c.X * PostDarken, c.Y * PostDarken, c.Z * PostDarken, c.W);

    /// <summary>Visible piles in ascending cell index order. Piles whose cell is above <paramref name="sliceY"/> are
    /// hidden (VIEW-04).</summary>
    public static List<PileMarker> Markers(Simulation sim, int sliceY, EntityColors colors)
    {
        var list = new List<PileMarker>();
        foreach (var (cell, stack) in sim.Piles.All)
        {
            if (cell.Y > sliceY || stack.IsEmpty) continue;
            var anchor = new Vector3(cell.X + 0.5f, cell.Y + LabelHeight, cell.Z + 0.5f);
            list.Add(new PileMarker(cell, stack.Item, stack.Count, colors.Item(stack.Item), PostDepth(sim, cell),
                anchor, stack.Count.ToString(CultureInfo.InvariantCulture)));
        }
        return list;
    }

    /// <summary>Air (non-solid) cells straight under the pile, up to <see cref="MaxPostDepth"/>.</summary>
    public static int PostDepth(Simulation sim, Int3 cell)
    {
        int d = 0;
        while (d < MaxPostDepth)
        {
            var below = new Int3(cell.X, cell.Y - d - 1, cell.Z);
            if (below.Y < 0 || sim.World.IsSolid(below)) break;
            d++;
        }
        return d;
    }

    public static MeshData Build(IEnumerable<PileMarker> markers)
    {
        var mesh = new MeshData();
        foreach (var m in markers)
        {
            float x = m.Cell.X, y = m.Cell.Y, z = m.Cell.Z;
            MeshShapes.AddBox(mesh, new Vector3(x + MarkerInset, y, z + MarkerInset),
                new Vector3(x + 1 - MarkerInset, y + MarkerHeight, z + 1 - MarkerInset), m.Color);
            MeshShapes.AddBox(mesh, new Vector3(x + CapInset, y + MarkerHeight, z + CapInset),
                new Vector3(x + 1 - CapInset, y + MarkerHeight + CapHeight, z + 1 - CapInset), CapColor(m.Color));
            if (m.PostDepth > 0)
                MeshShapes.AddBox(mesh, new Vector3(x + 0.5f - PostHalfWidth, y - m.PostDepth, z + 0.5f - PostHalfWidth),
                    new Vector3(x + 0.5f + PostHalfWidth, y, z + 0.5f + PostHalfWidth), PostColor(m.Color));
        }
        return mesh;
    }

    /// <summary>The pile under a pick: on top of the picked block (or, over a dug floor, up the column of air above
    /// it, where a floating pile's post stands), else in front of the picked face. Null if none or if its cell is
    /// above the slice.</summary>
    public static (Int3 Cell, ItemStack Stack)? AtPick(Simulation sim, PickHit hit, int sliceY)
    {
        var c = hit.Cell + Int3.Up;
        for (int i = 0; i <= MaxPostDepth && c.Y <= sliceY && sim.World.InBounds(c); i++, c += Int3.Up)
        {
            var s = sim.Piles.At(c);
            if (!s.IsEmpty) return (c, s);
            if (sim.World.IsSolid(c)) break;
        }
        if (hit.Adjacent.Y <= sliceY)
        {
            var s = sim.Piles.At(hit.Adjacent);
            if (!s.IsEmpty) return (hit.Adjacent, s);
        }
        return null;
    }

    /// <summary>Hover label, e.g. "Stone ×12".</summary>
    public static string Label(ContentDb content, ItemStack stack) =>
        $"{content.ItemDef(stack.Item).Name} ×{stack.Count}";
}
