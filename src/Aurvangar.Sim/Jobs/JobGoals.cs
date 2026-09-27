using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;

namespace Aurvangar.Sim.Jobs;

/// <summary>Goal cells of a GoTo step: walkable cells from which the step's target is in reach (ARCH-07). Ascending
/// cell index, so A* ties and the region filter are deterministic. A building's goals are never inside its own
/// footprint, nor (while it is deconstructed) on top of it (ADR-041).</summary>
public static class JobGoals
{
    public static List<Int3> For(Simulation sim, JobStep step)
    {
        var grid = sim.PathGrid;
        var world = sim.World;
        var goals = new List<Int3>();
        switch (step.Goal)
        {
            case GoalMode.Exact:
                if (grid.IsWalkable(step.Cell)) goals.Add(step.Cell);
                return goals;
            case GoalMode.Reach:
                // dy, dz, dx ascending is ascending flat index for a fixed center.
                for (int dy = -1; dy <= 1; dy++)
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            var c = step.Cell + new Int3(dx, dy, dz);
                            if (grid.IsWalkable(c)) goals.Add(c);
                        }
                return goals;
            case GoalMode.Dig:
                return DigGoals(sim, step.Cell);
            case GoalMode.Build:
                return BuildStandCells(sim, step.Cell, strandFree: true, prefer: true);
            case GoalMode.Building:
                var b = sim.Buildings.Get(new BuildingId(step.Target));
                if (b is null) return goals;
                var seen = new SortedSet<int>();
                foreach (var f in b.FootprintCells())
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dz = -1; dz <= 1; dz++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                var c = f + new Int3(dx, dy, dz);
                                if (!world.InBounds(c) || !grid.IsWalkable(c) || b.Covers(c)) continue;
                                if (b.State == Buildings.BuildingState.Deconstructing && b.Covers(c + Int3.Down)) continue;
                                seen.Add(world.Index(c));
                            }
                foreach (var i in seen) goals.Add(world.CellOf(i));
                return goals;
            default:
                throw new InvalidOperationException($"unknown goal mode {step.Goal}");
        }
    }

    /// <summary>JOB-09: reach cells of a dig target except the cell on top of it (its floor would vanish). When
    /// possible only cells whose own floor is not designated for digging, so they stay standable after the next digs.
    /// M4-T14: never a cell the dig would cut off from the Great Hall (<see cref="DigStrand"/>).</summary>
    private static List<Int3> DigGoals(Simulation sim, Int3 target) => DigStandCells(sim, target, strandFree: true);

    /// <summary>M4-T14: true when the dig has stand cells but every one of them would be cut off from the hall.</summary>
    public static bool DigBlockedByStrand(Simulation sim, Int3 target) =>
        DigStandCells(sim, target, strandFree: false).Count > 0 && DigStandCells(sim, target, strandFree: true).Count == 0;

    private static List<Int3> DigStandCells(Simulation sim, Int3 target, bool strandFree)
    {
        var grid = sim.PathGrid;
        var marks = sim.Designations;
        bool check = strandFree && sim.DigTrial.MaySplit(target);
        var all = new List<Int3>();
        var safe = new List<Int3>();
        for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dy == 1 && dz == 0 && dx == 0) continue;
                    var c = target + new Int3(dx, dy, dz);
                    if (!grid.IsWalkable(c)) continue;
                    if (check && DigStrand.Strands(sim, target, c)) continue;
                    all.Add(c);
                    if (marks.Get(c + Int3.Down) != DesignationMark.Dig) safe.Add(c);
                }
        return safe.Count > 0 ? safe : all;
    }

    /// <summary>CON-11 (M8-T2): build stand cells of a block at <paramref name="target"/>: walkable cells in reach
    /// except the cell itself and the one below it (the block would take the stand cell or its headroom), ascending
    /// index. <paramref name="strandFree"/> drops cells the placement would cut off from the Great Hall
    /// (<see cref="PlaceStrand"/>, CON-14). <paramref name="prefer"/> keeps only cells with no plan entry and no Dig mark
    /// on their floor when there are any (like <see cref="DigStandCells"/>).</summary>
    public static List<Int3> BuildStandCells(Simulation sim, Int3 target, bool strandFree, bool prefer)
    {
        var grid = sim.PathGrid;
        bool check = strandFree && sim.PlaceTrial.MaySplit(target);
        var all = new List<Int3>();
        var safe = new List<Int3>();
        for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dz == 0 && dx == 0 && dy <= 0) continue;   // the cell itself and the one below it
                    var c = target + new Int3(dx, dy, dz);
                    if (!grid.IsWalkable(c)) continue;
                    if (check && PlaceStrand.Strands(sim, target, c)) continue;
                    all.Add(c);
                    if (prefer && !sim.Plans.Has(c) && sim.Designations.Get(c + Int3.Down) != DesignationMark.Dig) safe.Add(c);
                }
        return safe.Count > 0 ? safe : all;
    }
}
