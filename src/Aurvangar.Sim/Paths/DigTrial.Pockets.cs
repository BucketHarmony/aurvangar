using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Paths;

/// <summary>M7-T6 (G3 answer 8, ADR-059): which cells a dig would cut off from the anchors, for any dwarf, not only the
/// digger. One answer per dug block, cached like <see cref="MaySplit"/> until the walkability (or deep) version moves,
/// so the per-dwarf test is a set lookup.</summary>
public sealed partial class DigTrial
{
    private readonly Dictionary<int, TrialCutOff> _cutCache = new();   // lookups only, never enumerated
    private int _cutAnchorKey = int.MinValue;

    /// <summary>Diagnostics: cut sets computed. Never read by gameplay code.</summary>
    public long CutComputes { get; private set; }

    /// <summary>True if, after <paramref name="dug"/> becomes air, <paramref name="cell"/> may no longer reach the
    /// anchors. Exact for a cell that reaches them now; the caller checks that. <paramref name="anchorKey"/> names the
    /// anchor set (the hall's id): the cache is dropped when it changes.</summary>
    public bool MayCut(Int3 dug, IReadOnlyList<Int3> anchors, int anchorKey, Int3 cell)
    {
        if (!MaySplit(dug)) return false;   // also syncs the grid and ages the caches
        var world = World;
        if (!world.InBounds(cell) || anchors.Count == 0) return false;
        if (anchorKey != _cutAnchorKey) { _cutCache.Clear(); _cutAnchorKey = anchorKey; }
        int key = world.Index(dug);
        if (!_cutCache.TryGetValue(key, out var cut))
        {
            cut = ComputeCut(dug, anchors);
            _cutCache[key] = cut;
        }
        return cut.Cuts(world.Index(cell));
    }

    /// <summary>Floods from each move neighbor of the cell on top of the dug block (the only cell that loses its floor)
    /// against the anchors, on the what-if view. A neighbor side that runs out is a cut-off pocket; neighbors met by an
    /// earlier flood share its answer. If the anchor side runs out, its component is the whole answer.</summary>
    private TrialCutOff ComputeCut(Int3 dug, IReadOnlyList<Int3> anchors)
    {
        CutComputes++;
        var world = World;
        var top = dug + Int3.Up;
        Span<PathStep> steps = stackalloc PathStep[PathMoves.MaxMoves];
        int n = PathMoves.Steps(_grid, top.X, top.Y, top.Z, steps);
        int sizeX = world.SizeX, layer = world.SizeX * world.SizeZ;
        Span<int> targets = stackalloc int[PathMoves.MaxMoves];
        Span<bool> known = stackalloc bool[PathMoves.MaxMoves];
        for (int k = 0; k < n; k++) targets[k] = steps[k].X + steps[k].Z * sizeX + steps[k].Y * layer;

        var trial = new DigTrialCells(_grid, dug);
        HashSet<int>? pockets = null;
        for (int k = 0; k < n; k++)
        {
            if (known[k]) continue;
            var start = new Int3(steps[k].X, steps[k].Y, steps[k].Z);
            var end = _f.Flood(trial, start, anchors, out int genA, out int genB, out int tailA, out int tailB);
            if (end == FloodEnd.StandNotWalkable) continue;   // not expected: only the top cell loses its floor
            // The anchor side ran out first: its component is the answer. With no anchor walkable on the what-if view
            // (the dig takes the floor of the hall's only reach cell) it is empty, and every dwarf counts as cut.
            if (end == FloodEnd.AnchorSideOut)
            {
                var hall = new HashSet<int>();
                for (int i = 0; i < tailB; i++) hall.Add(_f.Qb[i]);
                return new TrialCutOff(hall, hallSide: true, world.Index(top), -1);
            }
            if (end == FloodEnd.StandSideOut)
            {
                pockets ??= new HashSet<int>();
                for (int i = 0; i < tailA; i++) pockets.Add(_f.Qa[i]);
            }
            for (int j = k + 1; j < n; j++)
            {
                int m = _f.Mark[targets[j]];
                if (m == genA || m == genB) known[j] = true;   // same pocket, or linked to the anchors
            }
        }
        return pockets is null ? TrialCutOff.Nothing : new TrialCutOff(pockets, hallSide: false, world.Index(top), -1);
    }
}
