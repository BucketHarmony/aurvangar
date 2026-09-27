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
    /// <summary>WAT-14: a flee path is at most this many steps.</summary>
    public const int FleeMaxSteps = 64;
    public const int CostStraight = PathMoves.CostStraight, CostDiagonal = PathMoves.CostDiagonal,
        CostStepUp = PathMoves.CostStepUp, CostStepDown = PathMoves.CostStepDown, CostWade = PathMoves.CostWade; // PTH-07

    private static readonly PathResult InvalidStartResult = new(PathStatus.InvalidStart, Array.Empty<Int3>(), 0);
    private static readonly PathResult TooFarResult = new(PathStatus.TooFar, Array.Empty<Int3>(), 0);

    private readonly PathGrid _grid;
    private readonly PathHeap _open = new();
    private readonly List<Int3> _goals = new();
    private readonly List<int> _queue = new();
    private readonly PathStep[] _steps = new PathStep[PathMoves.MaxMoves];
    private readonly PathMove[] _moves = new PathMove[PathMoves.MaxMoves];

    // Pooled per-cell search state (PTH-09). Seen == _gen: G and CameFrom are valid; Closed == _gen: expanded.
    private struct Node { public int Seen, G, CameFrom, Closed; }   // 16 bytes: one cache line access per cell
    private Node[] _nodes = Array.Empty<Node>();
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
        bool oneGoal = _goals.Count == 1;
        Int3 goal0 = _goals[0];

        _open.Clear();
        _grid.SyncWorldChanges();                   // once per search; the fast flag reads below do not sync
        int sizeX = world.SizeX, layer = world.SizeX * world.SizeZ;
        int si = world.Index(start);
        var nodes = _nodes;
        nodes[si].Seen = _gen; nodes[si].G = 0; nodes[si].CameFrom = -1;
        int h0 = Heuristic(start.X, start.Y, start.Z);
        _open.Push(h0, h0, si);

        int expanded = 0;
        while (_open.Count > 0)
        {
            int ci = _open.PopIndex();
            ref var cn = ref nodes[ci];
            if (cn.Closed == _gen) continue;                // stale heap entry
            if (_goalMark[ci] == _gen)
            {
                LastExpanded = expanded;
                return Build(ci, cn.G);
            }
            if (expanded >= MaxExpanded) { LastExpanded = expanded; return TooFarResult; }  // PTH-10
            cn.Closed = _gen;
            expanded++;

            int cy = ci / layer, rem = ci - cy * layer, cz = rem / sizeX, cx = rem - cz * sizeX;
            int gc = cn.G;
            int n = PathMoves.Steps(_grid, cx, cy, cz, _steps);
            for (int m = 0; m < n; m++)
            {
                ref readonly var st = ref _steps[m];
                int ni = st.X + st.Z * sizeX + st.Y * layer;
                ref var nn = ref nodes[ni];
                if (nn.Closed == _gen) continue;
                int ng = gc + st.Cost;
                if (nn.Seen == _gen && ng >= nn.G) continue;
                nn.Seen = _gen; nn.G = ng; nn.CameFrom = ci;
                int h = oneGoal ? PathMoves.Heuristic(st.X, st.Y, st.Z, goal0) : Heuristic(st.X, st.Y, st.Z);
                _open.Push(ng + h, h, ni);
            }
        }
        LastExpanded = expanded;
        return PathResult.None;
    }

    /// <summary>WAT-14 flee (ADR-031): breadth-first over PTH-04..08 moves in swim mode (deep standable cells are
    /// passable) from <paramref name="start"/> to the walkable cell with the fewest steps, ties by the fixed
    /// <see cref="Int3.Horizontal8"/> expansion order. No path longer than <paramref name="maxSteps"/> steps.
    /// A walkable start returns <c>[start]</c>. The result's Cost is the step count.</summary>
    public PathResult FindFlee(Int3 start, int maxSteps = FleeMaxSteps)
    {
        Searches++;
        LastExpanded = 0;
        var world = _grid.World;
        if (!_grid.IsStandable(start)) return InvalidStartResult;
        if (_grid.IsWalkable(start)) return new PathResult(PathStatus.Found, new[] { start }, 0);

        EnsureArrays(world.CellCount);
        NextGeneration();
        _queue.Clear();
        int si = world.Index(start);
        _nodes[si].Seen = _gen; _nodes[si].G = 0; _nodes[si].CameFrom = -1;
        _queue.Add(si);
        for (int head = 0; head < _queue.Count; head++)
        {
            int ci = _queue[head];
            int depth = _nodes[ci].G;
            if (depth >= maxSteps) continue;
            LastExpanded++;
            int n = PathMoves.From(_grid, world.CellOf(ci), _moves, swim: true);
            for (int m = 0; m < n; m++)
            {
                var to = _moves[m].To;
                int ni = world.Index(to);
                ref var nn = ref _nodes[ni];
                if (nn.Seen == _gen) continue;
                nn.Seen = _gen; nn.G = depth + 1; nn.CameFrom = ci;
                if (_grid.IsWalkable(to)) return Build(ni, depth + 1);
                _queue.Add(ni);
            }
        }
        return PathResult.None;
    }

    /// <summary>Minimum heuristic over the goals (consistent, since each term is).</summary>
    private int Heuristic(int x, int y, int z)
    {
        int best = int.MaxValue;
        for (int k = 0; k < _goals.Count; k++)
        {
            int h = PathMoves.Heuristic(x, y, z, _goals[k]);
            if (h < best) best = h;
        }
        return best;
    }

    private PathResult Build(int endIndex, int cost)
    {
        var world = _grid.World;
        int len = 0;
        for (int i = endIndex; i >= 0; i = _nodes[i].CameFrom) len++;
        var path = new Int3[len];
        int p = len;
        for (int i = endIndex; i >= 0; i = _nodes[i].CameFrom) path[--p] = world.CellOf(i);
        return new PathResult(PathStatus.Found, path, cost);
    }

    private void EnsureArrays(int cells)
    {
        if (_nodes.Length == cells) return;
        // The packed heap key (PathHeap) holds the cell index in 28 bits and h in 16 bits.
        var w = _grid.World;
        if (cells > PathHeap.MaxIndex + 1 || 14 * Math.Max(w.SizeX, w.SizeZ) + 2 * w.SizeY > PathHeap.MaxH)
            throw new InvalidOperationException($"world {w.SizeX}x{w.SizeY}x{w.SizeZ} is too large for the A* heap key");
        _nodes = new Node[cells];
        _goalMark = new int[cells];
        _gen = 0;
    }

    private void NextGeneration()
    {
        if (_gen == int.MaxValue)
        {
            Array.Clear(_nodes); Array.Clear(_goalMark);
            _gen = 0;
        }
        _gen++;
    }
}
