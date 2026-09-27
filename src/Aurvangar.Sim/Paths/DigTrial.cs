using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Paths;

/// <summary>The move-rule view of the world as if one solid block were dug out (M4-T14). Only the three cells of
/// the block's column (below, itself, above) can change flags (PTH-01 reads a cell, the one above and the one below),
/// and the block reads as air for the PTH-05 headroom tests.</summary>
internal readonly struct DigTrialCells : IMoveCells
{
    private readonly PathGrid _grid;
    private readonly int _x, _y, _z, _index;

    public DigTrialCells(PathGrid grid, Int3 dug)
    {
        _grid = grid;
        _x = dug.X; _y = dug.Y; _z = dug.Z;
        _index = grid.World.Index(dug);
    }

    public byte FlagsAt(int x, int y, int z) =>
        x == _x && z == _z && y >= _y - 1 && y <= _y + 1 ? _grid.FlagsIfAir(x, y, z, _index) : _grid.FlagsAt(x, y, z);

    public bool IsSolid(int x, int y, int z) => (x != _x || y != _y || z != _z) && _grid.World.IsSolid(x, y, z);
}

/// <summary>M4-T14 (ADR-037): would digging out one block cut a cell off from a set of anchor cells? Derived helper
/// (not state, not hashed, not saved); every answer is a pure function of the world, so it is deterministic.
/// <para>Digging a block only adds cells and moves (new standable cells, PTH-05 headroom) except for one loss: the
/// cell on top of it (<c>U</c>) loses its floor. So if <c>U</c> is not walkable nothing is lost, and if all of
/// <c>U</c>'s move neighbors stay connected to each other without it, nothing is cut. Both are cheap local tests.
/// Otherwise an exact two-sided flood (stand side and anchor side, alternating, on the what-if view) decides; it
/// costs about twice the smaller side, so a stranded pocket is found quickly.</para></summary>
public sealed partial class DigTrial
{
    /// <summary>Cells the local test may visit before it gives up and defers to the exact flood.</summary>
    public const int LocalBudget = 512;

    private readonly PathGrid _grid;
    private int[] _mark = Array.Empty<int>();
    private int[] _qa = new int[256], _qb = new int[256];
    private int _gen;

    // MaySplit cache: pure function of the world, so it is valid until the walkability version moves.
    private readonly Dictionary<int, bool> _splitCache = new();   // lookups only, never enumerated
    private long _cacheVersion = -1, _cacheDeepVersion = -1;

    public DigTrial(PathGrid grid) { _grid = grid; }

    /// <summary>Diagnostics: exact floods run. Never read by gameplay code.</summary>
    public long ExactChecks { get; private set; }

    /// <summary>True if, after <paramref name="dug"/> becomes air, <paramref name="stand"/> can reach none of
    /// <paramref name="anchors"/> by the PTH-04..08 moves. The caller decides whether they are connected now.</summary>
    public bool Cuts(Int3 dug, Int3 stand, IReadOnlyList<Int3> anchors) =>
        anchors.Count > 0 && MaySplit(dug) && !Connected(dug, stand, anchors);

    /// <summary>Local test: false when the dig provably cuts nothing (see the class summary). Cached per block.</summary>
    public bool MaySplit(Int3 dug)
    {
        _grid.SyncWorldChanges();
        // Keyed on both versions: a what-if view also reads the depth of cells that are not standable now.
        if (_cacheVersion != _grid.WalkabilityVersion || _cacheDeepVersion != _grid.DeepVersion)
        {
            _splitCache.Clear();
            _cutCache.Clear();
            _cacheVersion = _grid.WalkabilityVersion;
            _cacheDeepVersion = _grid.DeepVersion;
        }
        var world = _grid.World;
        if (!world.InBounds(dug)) return false;
        int key = world.Index(dug);
        if (_splitCache.TryGetValue(key, out var cached)) return cached;
        bool result = ComputeMaySplit(dug);
        _splitCache[key] = result;
        return result;
    }

    private bool ComputeMaySplit(Int3 dug)
    {
        var top = dug + Int3.Up;
        if (!World.InBounds(top) || (_grid.FlagsAt(top.X, top.Y, top.Z) & PathGrid.WalkableFlag) == 0) return false;
        Span<PathStep> steps = stackalloc PathStep[PathMoves.MaxMoves];
        int n = PathMoves.Steps(_grid, top.X, top.Y, top.Z, steps);
        if (n <= 1) return false;   // a dead end: removing it cuts nobody else off

        var trial = new DigTrialCells(_grid, dug);
        int sizeX = World.SizeX, layer = World.SizeX * World.SizeZ;
        int topIndex = World.Index(top);
        int gen = NextGen();
        Span<int> targets = stackalloc int[PathMoves.MaxMoves];
        for (int k = 0; k < n; k++) targets[k] = steps[k].X + steps[k].Z * sizeX + steps[k].Y * layer;

        // Flood from the first neighbor on the what-if view until every other neighbor is met.
        int found = 1, head = 0, tail = 0;
        _mark[targets[0]] = gen;
        Push(ref _qa, ref tail, targets[0]);
        while (head < tail && tail <= LocalBudget)
        {
            int ai = _qa[head++];
            int ay = ai / layer, rem = ai - ay * layer, az = rem / sizeX, ax = rem - az * sizeX;
            int m = PathMoves.Steps(trial, ax, ay, az, steps);
            for (int k = 0; k < m; k++)
            {
                int bi = steps[k].X + steps[k].Z * sizeX + steps[k].Y * layer;
                if (bi == topIndex || _mark[bi] == gen) continue;
                _mark[bi] = gen;
                for (int t = 1; t < n; t++)
                    if (targets[t] == bi && ++found == n) return false;
                Push(ref _qa, ref tail, bi);
            }
        }
        return true;   // a neighbor was not met within the budget (or cannot be met): let the exact flood decide
    }

    /// <summary>Exact: alternating floods from the stand and from the anchors on the what-if view. Connected when
    /// they meet; cut when either side runs out first.</summary>
    private bool Connected(Int3 dug, Int3 stand, IReadOnlyList<Int3> anchors)
    {
        ExactChecks++;
        _grid.SyncWorldChanges();
        return Connected(new DigTrialCells(_grid, dug), stand, anchors);
    }

    /// <summary>The same two-sided flood on the world as it is: can <paramref name="stand"/> reach an anchor now?
    /// Used when regions are stale mid-tick (M4-T14).</summary>
    public bool ConnectedNow(Int3 stand, IReadOnlyList<Int3> anchors)
    {
        _grid.SyncWorldChanges();
        return Connected(new LiveCells(_grid), stand, anchors);
    }

    private bool Connected<TCells>(TCells trial, Int3 stand, IReadOnlyList<Int3> anchors) where TCells : struct, IMoveCells =>
        Flood(trial, stand, anchors, out _, out _, out _, out _) == FloodEnd.Met;

    /// <summary>How a two-sided flood ended.</summary>
    private enum FloodEnd : byte { Met, StandSideOut, AnchorSideOut, StandNotWalkable }

    /// <summary>The alternating two-sided flood behind <see cref="Connected{TCells}"/>. When one side runs out, every
    /// cell it reached is in its queue (<c>_qa[0..tailA)</c> for the stand side, <c>_qb[0..tailB)</c> for the anchor
    /// side) and marked with its generation: that side's whole component on the view (M7-T6 reads it).</summary>
    private FloodEnd Flood<TCells>(TCells trial, Int3 stand, IReadOnlyList<Int3> anchors,
        out int genA, out int genB, out int tailA, out int tailB) where TCells : struct, IMoveCells
    {
        var world = World;
        int sizeX = world.SizeX, layer = world.SizeX * world.SizeZ;
        int standIndex = world.Index(stand);
        genA = 0; genB = 0; tailA = 0; tailB = 0;
        if ((trial.FlagsAt(stand.X, stand.Y, stand.Z) & PathGrid.WalkableFlag) == 0) return FloodEnd.StandNotWalkable;
        genA = NextGen(); genB = NextGen();
        int headA = 0, headB = 0;
        _mark[standIndex] = genA;
        Push(ref _qa, ref tailA, standIndex);
        foreach (var c in anchors)
        {
            if (!world.InBounds(c) || (trial.FlagsAt(c.X, c.Y, c.Z) & PathGrid.WalkableFlag) == 0) continue;
            int i = world.Index(c);
            if (_mark[i] == genA) return FloodEnd.Met;
            if (_mark[i] == genB) continue;
            _mark[i] = genB;
            Push(ref _qb, ref tailB, i);
        }
        Span<PathStep> steps = stackalloc PathStep[PathMoves.MaxMoves];
        while (headA < tailA && headB < tailB)
        {
            if (Expand(trial, ref _qa, ref headA, ref tailA, genA, genB, steps, sizeX, layer)) return FloodEnd.Met;
            if (Expand(trial, ref _qb, ref headB, ref tailB, genB, genA, steps, sizeX, layer)) return FloodEnd.Met;
        }
        return headA >= tailA ? FloodEnd.StandSideOut : FloodEnd.AnchorSideOut;
    }

    /// <summary>Expands one cell of a side. True when it touches a cell of the other side.</summary>
    private bool Expand<TCells>(TCells trial, ref int[] queue, ref int head, ref int tail, int own, int other,
        Span<PathStep> steps, int sizeX, int layer) where TCells : struct, IMoveCells
    {
        int ai = queue[head++];
        int ay = ai / layer, rem = ai - ay * layer, az = rem / sizeX, ax = rem - az * sizeX;
        int m = PathMoves.Steps(trial, ax, ay, az, steps);
        for (int k = 0; k < m; k++)
        {
            int bi = steps[k].X + steps[k].Z * sizeX + steps[k].Y * layer;
            int mk = _mark[bi];
            if (mk == other) return true;
            if (mk == own) continue;
            _mark[bi] = own;
            Push(ref queue, ref tail, bi);
        }
        return false;
    }

    private VoxelWorld World => _grid.World;

    private int NextGen()
    {
        if (_mark.Length != _grid.World.CellCount) _mark = new int[_grid.World.CellCount];
        if (_gen == int.MaxValue) { Array.Clear(_mark); _gen = 0; }
        return ++_gen;
    }

    private static void Push(ref int[] queue, ref int tail, int index)
    {
        if (tail == queue.Length) Array.Resize(ref queue, queue.Length * 2);
        queue[tail++] = index;
    }
}
