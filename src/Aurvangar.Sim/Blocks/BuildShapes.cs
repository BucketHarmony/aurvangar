using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Blocks;

/// <summary>CON-07 shapes of a <see cref="Commands.DesignateBuild"/>. Never renumber: logged and saved as a byte.</summary>
public enum BuildShape : byte { Single, Line, Wall, Floor, HollowBox, Stair }

/// <summary>CON-07: the cells of a build shape. A pure function shared by the sim and the view (the tool's ghost).
/// All shapes are axis-aligned; <c>A</c> is the anchor and <c>A.Y</c> the base level; <c>B.Y</c> is ignored.</summary>
public static class BuildShapes
{
    /// <summary>CON-07 rejection bound: shapes with more cells are <c>TooLarge</c>.</summary>
    public const int MaxCells = 4096;

    public const int MinHeight = 1, MaxHeight = 32;

    /// <summary>Whether the shape uses <paramref name="height"/> (Wall and HollowBox); the others use 1.</summary>
    public static bool UsesHeight(BuildShape shape) => shape is BuildShape.Wall or BuildShape.HollowBox;

    /// <summary>The cell count without building the list (long arithmetic: a huge drag cannot overflow).</summary>
    public static long Count(BuildShape shape, Int3 a, Int3 b, int height)
    {
        long w = Math.Abs((long)b.X - a.X) + 1, d = Math.Abs((long)b.Z - a.Z) + 1;
        long h = UsesHeight(shape) ? Math.Max(height, 0) : 1;
        long line = Math.Max(w, d);
        return shape switch
        {
            BuildShape.Single => 1,
            BuildShape.Line or BuildShape.Stair => line,
            BuildShape.Wall => line * h,
            BuildShape.Floor => w * d,
            BuildShape.HollowBox => (w == 1 || d == 1 ? w * d : 2 * (w + d) - 4) * h,
            _ => 0,
        };
    }

    /// <summary>The shape's cells in ascending (y, z, x) order, which is ascending (y, index) order. The caller bounds
    /// the size with <see cref="Count"/> first.</summary>
    public static List<Int3> Cells(BuildShape shape, Int3 a, Int3 b, int height)
    {
        var cells = new List<Int3>();
        int h = UsesHeight(shape) ? height : 1;
        int x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X), z0 = Math.Min(a.Z, b.Z), z1 = Math.Max(a.Z, b.Z);
        bool alongX = Math.Abs(b.X - a.X) >= Math.Abs(b.Z - a.Z);   // ties go to X
        switch (shape)
        {
            case BuildShape.Single:
                cells.Add(a);
                return cells;
            case BuildShape.Line:
            case BuildShape.Wall:
                for (int y = a.Y; y < a.Y + h; y++)
                    if (alongX) for (int x = x0; x <= x1; x++) cells.Add(new Int3(x, y, a.Z));
                    else for (int z = z0; z <= z1; z++) cells.Add(new Int3(a.X, y, z));
                return cells;
            case BuildShape.Floor:
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++) cells.Add(new Int3(x, a.Y, z));
                return cells;
            case BuildShape.HollowBox:
                for (int y = a.Y; y < a.Y + h; y++)
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                            if (x == x0 || x == x1 || z == z0 || z == z1) cells.Add(new Int3(x, y, z));
                return cells;
            case BuildShape.Stair:
            {
                int n = alongX ? x1 - x0 + 1 : z1 - z0 + 1;
                int step = alongX ? Math.Sign(b.X - a.X) : Math.Sign(b.Z - a.Z);
                for (int i = 0; i < n; i++)   // one cell per level, rising: already in (y, index) order
                    cells.Add(alongX ? new Int3(a.X + i * step, a.Y + i, a.Z) : new Int3(a.X, a.Y + i, a.Z + i * step));
                return cells;
            }
            default:
                return cells;
        }
    }
}
