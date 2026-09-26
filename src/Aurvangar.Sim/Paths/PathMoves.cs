using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Paths;

/// <summary>One legal step from a cell: destination and its PTH-07 cost.</summary>
public readonly record struct PathMove(Int3 To, int Cost);

/// <summary>Neighbor and cost rules shared by A* and region flood fill. Spec: PTH-04..08.</summary>
public static class PathMoves
{
    public const int CostStraight = 10, CostDiagonal = 14, CostStepUp = 6, CostStepDown = 2, CostWade = 8; // PTH-07

    /// <summary>At most one move per horizontal direction (the three dy options are mutually exclusive).</summary>
    public const int MaxMoves = 8;

    /// <summary>Writes the legal moves out of <paramref name="a"/> into <paramref name="moves"/> in the fixed
    /// <see cref="Int3.Horizontal8"/> order and returns how many. The caller decides whether <paramref name="a"/>
    /// itself may be left (A* allows leaving a deep start cell, WAT-14 flee). Assumes <paramref name="a"/> is standable.</summary>
    public static int From(PathGrid grid, Int3 a, Span<PathMove> moves)
    {
        var world = grid.World;
        int n = 0;
        bool headroomUp = !world.IsSolid(a.X, a.Y + 2, a.Z);      // PTH-05 climb headroom
        var dirs = Int3.Horizontal8;
        for (int d = 0; d < dirs.Length; d++)
        {
            var dir = dirs[d];
            bool diagonal = dir.X != 0 && dir.Z != 0;
            var flat = a + dir;
            int dy;
            // PTH-04. Standable cells in one column at y-1, y, y+1 are mutually exclusive, so the first walkable wins.
            if (grid.IsWalkable(flat)) dy = 0;
            else if (headroomUp && grid.IsWalkable(flat + Int3.Up)) dy = 1;
            else if (grid.IsWalkable(flat + Int3.Down) && !world.IsSolid(flat.X, flat.Y + 1, flat.Z)) dy = -1; // PTH-05: b+up+up = flat+up
            else continue;                                          // PTH-08: nothing reachable within one step

            if (diagonal)
            {
                // PTH-06: both orthogonal intermediates walkable at a.y (flat, down) or b.y (up).
                int iy = dy == 1 ? a.Y + 1 : a.Y;
                if (!grid.IsWalkable(new Int3(a.X + dir.X, iy, a.Z)) || !grid.IsWalkable(new Int3(a.X, iy, a.Z + dir.Z)))
                    continue;
            }

            var b = new Int3(flat.X, flat.Y + dy, flat.Z);
            int cost = diagonal ? CostDiagonal : CostStraight;
            if (dy == 1) cost += CostStepUp;
            else if (dy == -1) cost += CostStepDown;
            if (grid.IsWet(b)) cost += CostWade;                    // walkable + wet = wadeable (WAT-14)
            moves[n++] = new PathMove(b, cost);
        }
        return n;
    }

    /// <summary>PTH-09 heuristic: octile distance on (x,z) plus 2 per level. Admissible and consistent with PTH-07.</summary>
    public static int Heuristic(Int3 a, Int3 b)
    {
        int dx = Math.Abs(a.X - b.X), dz = Math.Abs(a.Z - b.Z);
        int lo = Math.Min(dx, dz), hi = Math.Max(dx, dz);
        return CostDiagonal * lo + CostStraight * (hi - lo) + 2 * Math.Abs(a.Y - b.Y);
    }
}
