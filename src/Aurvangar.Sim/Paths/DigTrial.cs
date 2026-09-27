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
    private readonly TrialFlood _f;

    // MaySplit cache: pure function of the world, so it is valid until the walkability version moves.
    private readonly Dictionary<int, bool> _splitCache = new();   // lookups only, never enumerated
    private long _cacheVersion = -1, _cacheDeepVersion = -1;

    public DigTrial(PathGrid grid) { _grid = grid; _f = new TrialFlood(grid); }

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
        Span<int> targets = stackalloc int[PathMoves.MaxMoves];
        for (int k = 0; k < n; k++) targets[k] = steps[k].X + steps[k].Z * sizeX + steps[k].Y * layer;
        // Flood from the first neighbor on the what-if view until every other neighbor is met. A neighbor not met
        // within the budget (or not at all) leaves it to the exact flood.
        return _f.MayMiss(trial, targets[..n], World.Index(top), LocalBudget);
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
        _f.Flood(trial, stand, anchors, out _, out _, out _, out _) == FloodEnd.Met;

    private VoxelWorld World => _grid.World;
}
