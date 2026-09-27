using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Paths;

/// <summary>M8-T2 (CON-14): the move-rule view of the world as if one air cell held a solid block (a placement). Only
/// the three cells of its column (below, itself, above) can change flags, and the cell reads as solid for the PTH-05
/// headroom tests.</summary>
internal readonly struct PlaceTrialCells : IMoveCells
{
    private readonly PathGrid _grid;
    private readonly int _x, _y, _z, _index;

    public PlaceTrialCells(PathGrid grid, Int3 placed)
    {
        _grid = grid;
        _x = placed.X; _y = placed.Y; _z = placed.Z;
        _index = grid.World.Index(placed);
    }

    public byte FlagsAt(int x, int y, int z) =>
        x == _x && z == _z && y >= _y - 1 && y <= _y + 1 ? _grid.FlagsIfSolid(x, y, z, _index) : _grid.FlagsAt(x, y, z);

    public bool IsSolid(int x, int y, int z) => (x == _x && y == _y && z == _z) || _grid.World.IsSolid(x, y, z);
}

/// <summary>M8-T2 (CON-14, ADR-062): would placing one block cut a cell off from a set of anchor cells? The mirror of
/// <see cref="DigTrial"/>. Derived helper (not state, not hashed, not saved); every answer is a pure function of the
/// world.
/// <para>A placement at P removes P and P+down (it loses its headroom) as walkable cells, and the moves that need
/// headroom through P or pass P / P+down as a diagonal intermediate. The ends of every lost move lie in the 3x3
/// columns around P, at most three levels below it. If all ends of lost moves, and all neighbors of removed cells,
/// still meet each other on the what-if view, nothing is cut (the local test). Otherwise exact floods decide.</para></summary>
public sealed class PlaceTrial
{
    /// <summary>Cells the local test may visit before it gives up and defers to the exact flood.</summary>
    public const int LocalBudget = 512;

    private const int MaxTargets = 128;

    private readonly PathGrid _grid;
    private readonly TrialFlood _f;
    private readonly Dictionary<int, bool> _splitCache = new();          // lookups only, never enumerated
    private readonly Dictionary<int, TrialCutOff> _cutCache = new();     // lookups only, never enumerated
    private long _cacheVersion = -1, _cacheDeepVersion = -1;
    private int _cutAnchorKey = int.MinValue;

    public PlaceTrial(PathGrid grid) { _grid = grid; _f = new TrialFlood(grid); }

    /// <summary>Diagnostics: exact floods and cut sets computed. Never read by gameplay code.</summary>
    public long ExactChecks { get; private set; }
    public long CutComputes { get; private set; }

    private VoxelWorld World => _grid.World;

    /// <summary>True if, after <paramref name="placed"/> becomes solid, <paramref name="stand"/> can reach none of
    /// <paramref name="anchors"/>. The caller decides whether they are connected now.</summary>
    public bool Cuts(Int3 placed, Int3 stand, IReadOnlyList<Int3> anchors)
    {
        if (anchors.Count == 0 || !MaySplit(placed)) return false;
        ExactChecks++;
        var end = _f.Flood(new PlaceTrialCells(_grid, placed), stand, anchors, out _, out _, out _, out _);
        return end != FloodEnd.Met;
    }

    /// <summary>Local test: false when the placement provably cuts nothing. Cached per cell.</summary>
    public bool MaySplit(Int3 placed)
    {
        _grid.SyncWorldChanges();
        if (_cacheVersion != _grid.WalkabilityVersion || _cacheDeepVersion != _grid.DeepVersion)
        {
            _splitCache.Clear();
            _cutCache.Clear();
            _cacheVersion = _grid.WalkabilityVersion;
            _cacheDeepVersion = _grid.DeepVersion;
        }
        var world = World;
        if (!world.InBounds(placed) || world.IsSolid(placed)) return false;
        int key = world.Index(placed);
        if (_splitCache.TryGetValue(key, out var cached)) return cached;
        bool result = ComputeMaySplit(placed);
        _splitCache[key] = result;
        return result;
    }

    /// <summary>True if, after <paramref name="placed"/> becomes solid, <paramref name="cell"/> may no longer reach the
    /// anchors. Exact for a cell that reaches them now; the caller checks that. The placed cell and the cell below it
    /// never count as cut: a dwarf there blocks the placement instead (CON-13).</summary>
    public bool MayCut(Int3 placed, IReadOnlyList<Int3> anchors, int anchorKey, Int3 cell)
    {
        if (!MaySplit(placed)) return false;   // also syncs the grid and ages the caches
        var world = World;
        if (!world.InBounds(cell) || anchors.Count == 0) return false;
        if (anchorKey != _cutAnchorKey) { _cutCache.Clear(); _cutAnchorKey = anchorKey; }
        int key = world.Index(placed);
        if (!_cutCache.TryGetValue(key, out var cut))
        {
            cut = ComputeCut(placed, anchors);
            _cutCache[key] = cut;
        }
        return cut.Cuts(world.Index(cell));
    }

    private bool ComputeMaySplit(Int3 placed)
    {
        Span<int> targets = stackalloc int[MaxTargets];
        int n = Targets(placed, targets, out bool overflow);
        if (overflow) return true;
        return _f.MayMiss(new PlaceTrialCells(_grid, placed), targets[..n], -1, LocalBudget);
    }

    /// <summary>The cells that must stay linked on the what-if view: the ends of every move lost (both ends stay
    /// walkable), and the live move neighbors of every cell that stops being walkable. Deduplicated, ascending scan
    /// order, so the answer is deterministic.</summary>
    private int Targets(Int3 p, Span<int> targets, out bool overflow)
    {
        overflow = false;
        var world = World;
        int sizeX = world.SizeX, layer = world.SizeX * world.SizeZ;
        var live = new LiveCells(_grid);
        var trial = new PlaceTrialCells(_grid, p);
        Span<PathStep> before = stackalloc PathStep[PathMoves.MaxMoves];
        Span<PathStep> after = stackalloc PathStep[PathMoves.MaxMoves];
        int n = 0;
        for (int y = p.Y - 3; y <= p.Y + 1; y++)
        for (int z = p.Z - 1; z <= p.Z + 1; z++)
        for (int x = p.X - 1; x <= p.X + 1; x++)
        {
            if ((live.FlagsAt(x, y, z) & PathGrid.WalkableFlag) == 0) continue;
            int nb = PathMoves.Steps(live, x, y, z, before);
            bool stays = (trial.FlagsAt(x, y, z) & PathGrid.WalkableFlag) != 0;
            int na = stays ? PathMoves.Steps(trial, x, y, z, after) : 0;
            bool lostAny = false;
            for (int k = 0; k < nb; k++)
            {
                var b = before[k];
                bool kept = false;
                for (int j = 0; j < na; j++)
                    if (after[j].X == b.X && after[j].Y == b.Y && after[j].Z == b.Z) { kept = true; break; }
                if (kept || (trial.FlagsAt(b.X, b.Y, b.Z) & PathGrid.WalkableFlag) == 0) continue;
                lostAny = true;
                if (!Add(targets, ref n, b.X + b.Z * sizeX + b.Y * layer)) { overflow = true; return n; }
            }
            if (stays && lostAny && !Add(targets, ref n, x + z * sizeX + y * layer)) { overflow = true; return n; }
        }
        return n;
    }

    private static bool Add(Span<int> targets, ref int n, int index)
    {
        for (int i = 0; i < n; i++) if (targets[i] == index) return true;
        if (n == targets.Length) return false;
        targets[n++] = index;
        return true;
    }

    /// <summary>Floods from each target against the anchors on the what-if view, like <see cref="DigTrial"/>'s cut:
    /// a target side that runs out is a cut-off pocket; if the anchor side runs out, its component is the answer.</summary>
    private TrialCutOff ComputeCut(Int3 p, IReadOnlyList<Int3> anchors)
    {
        CutComputes++;
        var world = World;
        int sizeX = world.SizeX, layer = world.SizeX * world.SizeZ;
        int skipA = world.Index(p), skipB = p.Y > 0 ? world.Index(p + Int3.Down) : -1;
        Span<int> targets = stackalloc int[MaxTargets];
        int n = Targets(p, targets, out _);
        Span<bool> known = stackalloc bool[MaxTargets];
        var trial = new PlaceTrialCells(_grid, p);
        HashSet<int>? pockets = null;
        for (int k = 0; k < n; k++)
        {
            if (known[k]) continue;
            int ti = targets[k];
            int ty = ti / layer, rem = ti - ty * layer, tz = rem / sizeX, tx = rem - tz * sizeX;
            var end = _f.Flood(trial, new Int3(tx, ty, tz), anchors, out int genA, out int genB, out int tailA, out int tailB);
            if (end == FloodEnd.StandNotWalkable) continue;
            if (end == FloodEnd.AnchorSideOut)
            {
                var hall = new HashSet<int>();
                for (int i = 0; i < tailB; i++) hall.Add(_f.Qb[i]);
                return new TrialCutOff(hall, hallSide: true, skipA, skipB);
            }
            if (end == FloodEnd.StandSideOut)
            {
                pockets ??= new HashSet<int>();
                for (int i = 0; i < tailA; i++) pockets.Add(_f.Qa[i]);
            }
            for (int j = k + 1; j < n; j++)
            {
                int m = _f.Mark[targets[j]];
                if (m == genA || m == genB) known[j] = true;
            }
        }
        return pockets is null ? TrialCutOff.Nothing : new TrialCutOff(pockets, hallSide: false, skipA, skipB);
    }
}
