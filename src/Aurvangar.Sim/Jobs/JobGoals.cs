using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Jobs;

/// <summary>Goal cells of a GoTo step: walkable cells from which the step's target is in reach (ARCH-07). Ascending
/// cell index, so A* ties and the region filter are deterministic.</summary>
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
                                if (world.InBounds(c) && grid.IsWalkable(c)) seen.Add(world.Index(c));
                            }
                foreach (var i in seen) goals.Add(world.CellOf(i));
                return goals;
            default:
                throw new InvalidOperationException($"unknown goal mode {step.Goal}");
        }
    }
}
