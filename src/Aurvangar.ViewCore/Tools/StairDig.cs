using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Tools;

/// <summary>The dig tool's modes (VIEW-24, M11-T8): a box (VIEW-13) or a staircase down.</summary>
public enum DigMode : byte { Box, StairDown }

/// <summary>VIEW-24 (M11-T8, G6 follow-up "How do I dig down deeper than 1 tile?"): a 1-wide staircase from the first
/// picked cell down to the view level. Step k (k = 0..n) lies k cells along the drag's main axis and k levels below the
/// first cell; its column is dug from the step's own cell up two more (headroom, PTH-01, and the PTH-05 clearance to
/// step between neighbouring steps), so each step is a 1-level move from the one before and every dug cell stays
/// reachable. n = first.Y - bottom, where bottom is the second pick's cell clamped to the slice level (as VIEW-13).</summary>
public static class StairDig
{
    /// <summary>Cells dug per step column: the step, its headroom and the step-down clearance.</summary>
    public const int ColumnHeight = 3;

    /// <summary>The horizontal step direction: the sign of the drag's larger X/Z component (X on a tie), +X for a
    /// drag that does not move.</summary>
    public static Int3 Direction(PickHit first, PickHit second)
    {
        int dx = second.Cell.X - first.Cell.X, dz = second.Cell.Z - first.Cell.Z;
        if (dx == 0 && dz == 0) return new Int3(1, 0, 0);
        return Math.Abs(dx) >= Math.Abs(dz) ? new Int3(Math.Sign(dx), 0, 0) : new Int3(0, 0, Math.Sign(dz));
    }

    /// <summary>The level of the lowest step: the second pick's cell clamped to <paramref name="sliceY"/>, never above
    /// the first cell.</summary>
    public static int BottomY(PickHit first, PickHit second, int sliceY) =>
        Math.Min(first.Cell.Y, Math.Min(second.Cell.Y, sliceY));

    /// <summary>The step cells (the cell each step's floor is dug down to), first cell first.</summary>
    public static List<Int3> Steps(PickHit first, PickHit second, int sliceY)
    {
        var a = first.Cell;
        var d = Direction(first, second);
        int n = a.Y - BottomY(first, second, sliceY);
        var steps = new List<Int3>(n + 1);
        for (int k = 0; k <= n; k++) steps.Add(new Int3(a.X + d.X * k, a.Y - k, a.Z + d.Z * k));
        return steps;
    }

    /// <summary>One <see cref="DesignateDig"/> per step column, top step first.</summary>
    public static List<ICommand> Commands(PickHit first, PickHit second, int sliceY)
    {
        var list = new List<ICommand>();
        foreach (var s in Steps(first, second, sliceY))
            list.Add(new DesignateDig(s, s + new Int3(0, ColumnHeight - 1, 0)));
        return list;
    }

    /// <summary>The solid cells the staircase would dig, for the preview: each column bottom-up, steps in order.</summary>
    public static List<Int3> SolidCells(VoxelWorld world, PickHit first, PickHit second, int sliceY)
    {
        var cells = new List<Int3>();
        foreach (var s in Steps(first, second, sliceY))
            for (int dy = 0; dy < ColumnHeight; dy++)
            {
                var c = s + new Int3(0, dy, 0);
                if (world.InBounds(c) && c.Y > 0 && world.IsSolid(c)) cells.Add(c);
            }
        return cells;
    }

    /// <summary>The mouse label while a stair drag is held.</summary>
    public static string DragText(PickHit first, PickHit second, int sliceY)
    {
        int bottom = BottomY(first, second, sliceY);
        int n = first.Cell.Y - bottom;
        if (n == 0) return "Stair down: lower the view level (PageDown or [) to set how deep it goes";
        return $"Stair down {n} level{(n == 1 ? "" : "s")} to level {bottom}";
    }
}
