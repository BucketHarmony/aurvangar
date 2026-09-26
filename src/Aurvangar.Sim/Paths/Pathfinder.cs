using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Paths;

public enum PathStatus { Found, NoPath, TooFar, InvalidStart }

/// <summary>Result of a path query. Path includes start and end cells (PTH-12).</summary>
public sealed record PathResult(PathStatus Status, Int3[] Path, int Cost)
{
    public static readonly PathResult None = new(PathStatus.NoPath, Array.Empty<Int3>(), 0);
    public bool Found => Status == PathStatus.Found;
}

/// <summary>A* over PathGrid. Spec: PTH-04..12 (ADR-024).
/// Neighbor rules live in <see cref="PathMoves"/>. Node arrays are sized to the world, allocated on the first
/// search and reused; a generation stamp replaces clearing them. The start only has to be standable (an agent in
/// deep water may path out, WAT-14); goals that are not walkable are ignored.</summary>
public sealed class Pathfinder
{
    public const int MaxExpanded = 20_000;          // PTH-10
    public const int CostStraight = PathMoves.CostStraight, CostDiagonal = PathMoves.CostDiagonal,
        CostStepUp = PathMoves.CostStepUp, CostStepDown = PathMoves.CostStepDown, CostWade = PathMoves.CostWade; // PTH-07

    private static readonly PathResult InvalidStartResult = new(PathStatus.InvalidStart, Array.Empty<Int3>(), 0);
    private static readonly PathResult TooFarResult = new(PathStatus.TooFar, Array.Empty<Int3>(), 0);

    private readonly PathGrid _grid;
    private readonly PathHeap _open = new();
    private readonly List<Int3> _goals = new();
    private readonly PathMove[] _moves = new PathMove[PathMoves.MaxMoves];

    // Pooled per-cell search state (PTH-09). _seen[i] == _gen: _g and _cameFrom are valid; _closed[i] == _gen: expanded.
    private int[] _g = Array.Empty<int>();
    private int[] _cameFrom = Array.Empty<int>();
    private int[] _seen = Array.Empty<int>();
    private int[] _closed = Array.Empty<int>();
    private int[] _goalMark = Array.Empty<int>();
    private int _gen;

    public Pathfinder(PathGrid grid) { _grid = grid; }

    /// <summary>Total searches run. Used to prove region filtering avoids A* (JOB scenario 5).</summary>
    public long Searches { get; private set; }

    /// <summary>Nodes expanded by the last search. Diagnostics only.</summary>
    public int LastExpanded { get; private set; }

    public PathResult FindPath(Int3 start, Int3 goal) => FindPath(start, new[] { goal });

    /// <summary>PTH-11: path to the cheapest of several goals.</summary>
    public PathResult FindPath(Int3 start, IReadOnlyList<Int3> goals)
    {
        Searches++;
        LastExpanded = 0;
        var world = _grid.World;
        if (!_grid.IsStandable(start)) return InvalidStartResult;

        EnsureArrays(world.CellCount);
        NextGeneration();

        _goals.Clear();
        for (int k = 0; k < goals.Count; k++)
        {
            var g = goals[k];
            if (g == start) return new PathResult(PathStatus.Found, new[] { start }, 0);
            if (!_grid.IsWalkable(g)) continue;
            int gi = world.Index(g);
            if (_goalMark[gi] == _gen) continue;
            _goalMark[gi] = _gen;
            _goals.Add(g);
        }
        if (_goals.Count == 0) return PathResult.None;

        _open.Clear();
        int si = world.Index(start);
        _seen[si] = _gen; _g[si] = 0; _cameFrom[si] = -1;
        int h0 = Heuristic(start);
        _open.Push(h0, h0, si);

        int expanded = 0;
        while (_open.Count > 0)
        {
            int ci = _open.PopIndex();
            if (_closed[ci] == _gen) continue;              // stale heap entry
            if (_goalMark[ci] == _gen)
            {
                LastExpanded = expanded;
                return Build(ci, _g[ci]);
            }
            if (expanded >= MaxExpanded) { LastExpanded = expanded; return TooFarResult; }  // PTH-10
            _closed[ci] = _gen;
            expanded++;

            var c = world.CellOf(ci);
            int gc = _g[ci];
            int n = PathMoves.From(_grid, c, _moves);
            for (int m = 0; m < n; m++)
            {
                var mv = _moves[m];
                int ni = world.Index(mv.To);
                if (_closed[ni] == _gen) continue;
                int ng = gc + mv.Cost;
                if (_seen[ni] == _gen && ng >= _g[ni]) continue;
                _seen[ni] = _gen; _g[ni] = ng; _cameFrom[ni] = ci;
                int h = Heuristic(mv.To);
                _open.Push(ng + h, h, ni);
            }
        }
        LastExpanded = expanded;
        return PathResult.None;
    }

    /// <summary>Minimum heuristic over the goals (consistent, since each term is).</summary>
    private int Heuristic(Int3 c)
    {
        int best = int.MaxValue;
        for (int k = 0; k < _goals.Count; k++)
        {
            int h = PathMoves.Heuristic(c, _goals[k]);
            if (h < best) best = h;
        }
        return best;
    }

    private PathResult Build(int endIndex, int cost)
    {
        var world = _grid.World;
        int len = 0;
        for (int i = endIndex; i >= 0; i = _cameFrom[i]) len++;
        var path = new Int3[len];
        int p = len;
        for (int i = endIndex; i >= 0; i = _cameFrom[i]) path[--p] = world.CellOf(i);
        return new PathResult(PathStatus.Found, path, cost);
    }

    private void EnsureArrays(int cells)
    {
        if (_g.Length == cells) return;
        _g = new int[cells];
        _cameFrom = new int[cells];
        _seen = new int[cells];
        _closed = new int[cells];
        _goalMark = new int[cells];
        _gen = 0;
    }

    private void NextGeneration()
    {
        if (_gen == int.MaxValue)
        {
            Array.Clear(_seen); Array.Clear(_closed); Array.Clear(_goalMark);
            _gen = 0;
        }
        _gen++;
    }
}
