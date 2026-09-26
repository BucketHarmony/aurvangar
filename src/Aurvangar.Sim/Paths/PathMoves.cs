using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Paths;

/// <summary>One legal step from a cell: destination and its PTH-07 cost.</summary>
public readonly record struct PathMove(Int3 To, int Cost);

/// <summary>A <see cref="PathMove"/> as raw coordinates, for the search loops.</summary>
internal readonly struct PathStep
{
    public readonly int X, Y, Z, Cost;
    public PathStep(int x, int y, int z, int cost) { X = x; Y = y; Z = z; Cost = cost; }
}

/// <summary>Neighbor and cost rules shared by A* and region flood fill. Spec: PTH-04..08.</summary>
public static class PathMoves
{
    public const int CostStraight = 10, CostDiagonal = 14, CostStepUp = 6, CostStepDown = 2, CostWade = 8; // PTH-07

    /// <summary>At most one move per horizontal direction (the three dy options are mutually exclusive).</summary>
    public const int MaxMoves = 8;

    /// <summary>Writes the legal moves out of <paramref name="a"/> into <paramref name="moves"/> in the fixed
    /// <see cref="Int3.Horizontal8"/> order and returns how many. The caller decides whether <paramref name="a"/>
    /// itself may be left (A* allows leaving a deep start cell, WAT-14 flee). Assumes <paramref name="a"/> is standable.
    /// <paramref name="swim"/> (WAT-14 flee, ADR-031): deep standable cells count as passable too, so a fleeing agent
    /// can cross deep water; every other rule is unchanged.</summary>
    public static int From(PathGrid grid, Int3 a, Span<PathMove> moves, bool swim = false)
    {
        grid.SyncWorldChanges();
        Span<PathStep> steps = stackalloc PathStep[MaxMoves];
        int n = Steps(grid, a.X, a.Y, a.Z, steps, swim);
        for (int k = 0; k < n; k++) moves[k] = new PathMove(new Int3(steps[k].X, steps[k].Y, steps[k].Z), steps[k].Cost);
        return n;
    }

    // Horizontal8 as separate component tables for the hot loop (same order).
    private static readonly int[] DirX = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly int[] DirZ = { -1, -1, 0, 1, 1, 1, 0, -1 };

    /// <summary>The rules of <see cref="From"/> on raw coordinates, for A* and region fill (M4-T12). The caller must
    /// have called <see cref="PathGrid.SyncWorldChanges"/> since the last block change. Same moves, same order.</summary>
    internal static int Steps(PathGrid grid, int ax, int ay, int az, Span<PathStep> steps, bool swim = false)
    {
        var world = grid.World;
        byte pass = swim ? PathGrid.StandableFlag : PathGrid.WalkableFlag;
        int n = 0;
        bool headroomUp = !world.IsSolid(ax, ay + 2, az);           // PTH-05 climb headroom
        for (int d = 0; d < 8; d++)
        {
            int dx = DirX[d], dz = DirZ[d];
            int fx = ax + dx, fz = az + dz;
            int dy;
            byte fb;
            // PTH-04. Standable cells in one column at y-1, y, y+1 are mutually exclusive, so the first walkable wins.
            if (((fb = grid.FlagsAt(fx, ay, fz)) & pass) != 0) dy = 0;
            else if (headroomUp && ((fb = grid.FlagsAt(fx, ay + 1, fz)) & pass) != 0) dy = 1;
            else if (((fb = grid.FlagsAt(fx, ay - 1, fz)) & pass) != 0 && !world.IsSolid(fx, ay + 1, fz)) dy = -1; // PTH-05: b+up+up = flat+up
            else continue;                                          // PTH-08: nothing reachable within one step

            bool diagonal = dx != 0 && dz != 0;
            if (diagonal)
            {
                // PTH-06: both orthogonal intermediates walkable at a.y (flat, down) or b.y (up).
                int iy = dy == 1 ? ay + 1 : ay;
                if ((grid.FlagsAt(fx, iy, az) & pass) == 0 || (grid.FlagsAt(ax, iy, fz) & pass) == 0) continue;
            }

            int cost = diagonal ? CostDiagonal : CostStraight;
            if (dy == 1) cost += CostStepUp;
            else if (dy == -1) cost += CostStepDown;
            if ((fb & PathGrid.WetFlag) != 0) cost += CostWade;     // walkable + wet = wadeable (WAT-14)
            steps[n++] = new PathStep(fx, ay + dy, fz, cost);
        }
        return n;
    }

    /// <summary>PTH-09 heuristic: octile distance on (x,z) plus 2 per level. Admissible and consistent with PTH-07.</summary>
    public static int Heuristic(Int3 a, Int3 b) => Heuristic(a.X, a.Y, a.Z, b);

    internal static int Heuristic(int ax, int ay, int az, Int3 b)
    {
        int dx = Math.Abs(ax - b.X), dz = Math.Abs(az - b.Z);
        int lo = Math.Min(dx, dz), hi = Math.Max(dx, dz);
        return CostDiagonal * lo + CostStraight * (hi - lo) + 2 * Math.Abs(ay - b.Y);
    }
}
