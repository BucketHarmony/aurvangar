using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Paths;

public enum PathStatus { Found, NoPath, TooFar, InvalidStart }

/// <summary>Result of a path query. Path includes start and end cells (PTH-12).</summary>
public sealed record PathResult(PathStatus Status, Int3[] Path, int Cost)
{
    public static readonly PathResult None = new(PathStatus.NoPath, Array.Empty<Int3>(), 0);
    public bool Found => Status == PathStatus.Found;
}

/// <summary>A* over PathGrid. Spec: PTH-04..12. M4-T2.</summary>
public sealed class Pathfinder
{
    public const int MaxExpanded = 20_000;          // PTH-10
    public const int CostStraight = 10, CostDiagonal = 14, CostStepUp = 6, CostStepDown = 2, CostWade = 8; // PTH-07

    private readonly PathGrid _grid;

    public Pathfinder(PathGrid grid) { _grid = grid; }

    /// <summary>Total searches run. Used to prove region filtering avoids A* (JOB scenario 5).</summary>
    public long Searches { get; private set; }

    public PathResult FindPath(Int3 start, Int3 goal) => FindPath(start, new[] { goal });

    /// <summary>PTH-11: path to the cheapest of several goals.</summary>
    public PathResult FindPath(Int3 start, IReadOnlyList<Int3> goals)
    {
        Searches++;
        throw new NotImplementedException("M4-T2: A* pathfinder (docs/specs/pathfinding.md PTH-04..12)");
    }
}
