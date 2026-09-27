using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Paths;

/// <summary>How a two-sided flood ended.</summary>
internal enum FloodEnd : byte { Met, StandSideOut, AnchorSideOut, StandNotWalkable }

/// <summary>The cells cut off by one dig or placement: either the pockets that lose their link (<see cref="HallSide"/>
/// false), or, when the anchors' own side ran out first, the anchors' whole component afterwards (true). Up to two
/// cells are never "cut": a dwarf there is waited for instead (DSG-08 for the cell on top of a dug block; the placed
/// cell and the cell below it for a placement, CON-13).</summary>
internal sealed class TrialCutOff
{
    public static readonly TrialCutOff Nothing = new(new HashSet<int>(), hallSide: false, -1, -1);
    public readonly HashSet<int> Cells;   // lookups only, never enumerated
    public readonly bool HallSide;
    private readonly int _skipA, _skipB;
    public TrialCutOff(HashSet<int> cells, bool hallSide, int skipA, int skipB)
    { Cells = cells; HallSide = hallSide; _skipA = skipA; _skipB = skipB; }

    public bool Cuts(int index) =>
        index != _skipA && index != _skipB && (HallSide ? !Cells.Contains(index) : Cells.Contains(index));
}

/// <summary>The flood engine shared by the what-if helpers <see cref="DigTrial"/> (M4-T14) and <see cref="PlaceTrial"/>
/// (M8-T2): a generation-marked visit array and two queues, and the alternating two-sided flood over any
/// <see cref="IMoveCells"/> view. Derived scratch space: not state, never hashed or saved.</summary>
internal sealed class TrialFlood
{
    private readonly PathGrid _grid;
    public int[] Mark = Array.Empty<int>();
    public int[] Qa = new int[256], Qb = new int[256];
    private int _gen;

    public TrialFlood(PathGrid grid) { _grid = grid; }

    private VoxelWorld World => _grid.World;

    public int NextGen()
    {
        if (Mark.Length != World.CellCount) Mark = new int[World.CellCount];
        if (_gen == int.MaxValue) { Array.Clear(Mark); _gen = 0; }
        return ++_gen;
    }

    public static void Push(ref int[] queue, ref int tail, int index)
    {
        if (tail == queue.Length) Array.Resize(ref queue, queue.Length * 2);
        queue[tail++] = index;
    }

    /// <summary>The alternating two-sided flood: from <paramref name="stand"/> (side A) and from the walkable anchors
    /// (side B) on the view. Met when they touch. When one side runs out, every cell it reached is in its queue
    /// (<see cref="Qa"/>[0..tailA) for the stand side, <see cref="Qb"/>[0..tailB) for the anchor side) and marked with
    /// its generation: that side's whole component on the view.</summary>
    public FloodEnd Flood<TCells>(TCells trial, Int3 stand, IReadOnlyList<Int3> anchors,
        out int genA, out int genB, out int tailA, out int tailB) where TCells : struct, IMoveCells
    {
        var world = World;
        int sizeX = world.SizeX, layer = world.SizeX * world.SizeZ;
        int standIndex = world.Index(stand);
        genA = 0; genB = 0; tailA = 0; tailB = 0;
        if ((trial.FlagsAt(stand.X, stand.Y, stand.Z) & PathGrid.WalkableFlag) == 0) return FloodEnd.StandNotWalkable;
        genA = NextGen(); genB = NextGen();
        int headA = 0, headB = 0;
        Mark[standIndex] = genA;
        Push(ref Qa, ref tailA, standIndex);
        foreach (var c in anchors)
        {
            if (!world.InBounds(c) || (trial.FlagsAt(c.X, c.Y, c.Z) & PathGrid.WalkableFlag) == 0) continue;
            int i = world.Index(c);
            if (Mark[i] == genA) return FloodEnd.Met;
            if (Mark[i] == genB) continue;
            Mark[i] = genB;
            Push(ref Qb, ref tailB, i);
        }
        Span<PathStep> steps = stackalloc PathStep[PathMoves.MaxMoves];
        while (headA < tailA && headB < tailB)
        {
            if (Expand(trial, ref Qa, ref headA, ref tailA, genA, genB, steps, sizeX, layer)) return FloodEnd.Met;
            if (Expand(trial, ref Qb, ref headB, ref tailB, genB, genA, steps, sizeX, layer)) return FloodEnd.Met;
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
            int mk = Mark[bi];
            if (mk == other) return true;
            if (mk == own) continue;
            Mark[bi] = own;
            Push(ref queue, ref tail, bi);
        }
        return false;
    }

    /// <summary>Local test shared by both what-if helpers: floods from <paramref name="targets"/>[0] on the view,
    /// never entering <paramref name="skip"/> (-1 for none), until every other target is met. False when all are met
    /// within <paramref name="budget"/> queued cells; true when one is not (the exact test must decide).</summary>
    public bool MayMiss<TCells>(TCells trial, ReadOnlySpan<int> targets, int skip, int budget) where TCells : struct, IMoveCells
    {
        int n = targets.Length;
        if (n <= 1) return false;
        var world = World;
        int sizeX = world.SizeX, layer = world.SizeX * world.SizeZ;
        int gen = NextGen();
        Span<PathStep> steps = stackalloc PathStep[PathMoves.MaxMoves];
        int found = 1, head = 0, tail = 0;
        Mark[targets[0]] = gen;
        Push(ref Qa, ref tail, targets[0]);
        while (head < tail && tail <= budget)
        {
            int ai = Qa[head++];
            int ay = ai / layer, rem = ai - ay * layer, az = rem / sizeX, ax = rem - az * sizeX;
            int m = PathMoves.Steps(trial, ax, ay, az, steps);
            for (int k = 0; k < m; k++)
            {
                int bi = steps[k].X + steps[k].Z * sizeX + steps[k].Y * layer;
                if (bi == skip || Mark[bi] == gen) continue;
                Mark[bi] = gen;
                for (int t = 1; t < n; t++)
                    if (targets[t] == bi && ++found == n) return false;
                Push(ref Qa, ref tail, bi);
            }
        }
        return true;
    }
}
