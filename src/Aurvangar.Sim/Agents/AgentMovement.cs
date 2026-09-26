using Aurvangar.Sim.Core;
using Aurvangar.Sim.Paths;

namespace Aurvangar.Sim.Agents;

/// <summary>Where an agent is in following its current path (PTH-15/16). Job steps (M4-T6) read it.</summary>
public enum MoveStatus : byte { None, Moving, Arrived, Failed }

/// <summary>Path following. Spec: PTH-15..17 (ADR-026).
/// A segment is one path step: at its start the agent re-checks that the step is still legal (PTH-16), sets
/// <see cref="Agent.NextCell"/> and <see cref="Agent.MoveTotal"/>, then counts <see cref="Agent.MoveProgress"/> up
/// one per tick. When progress reaches the total it re-checks once more and enters the cell. A blocked step
/// repaths once per <see cref="Start"/> to the same goal (the last path cell); a second block or a failed repath
/// fails the move. Agents never block each other (PTH-17).</summary>
public static class AgentMovement
{
    public const int TicksStraight = 4, TicksDiagonal = 6, TicksStepUp = 2, TicksWade = 3;   // PTH-15

    /// <summary>PTH-15: ticks for one step. Diagonal if both x and z change; step up if <paramref name="to"/> is
    /// higher; wading if <paramref name="to"/> holds water (the same rule as the PTH-07 wading cost).</summary>
    public static int MoveTicks(PathGrid grid, Int3 from, Int3 to)
    {
        int t = from.X != to.X && from.Z != to.Z ? TicksDiagonal : TicksStraight;
        if (to.Y > from.Y) t += TicksStepUp;
        if (grid.IsWet(to)) t += TicksWade;
        return t;
    }

    /// <summary>Paths from the agent's cell to the cheapest goal and starts following it. Any step in progress is
    /// abandoned (the agent is still on <see cref="Agent.Cell"/>). Status is Arrived at once when the agent already
    /// stands on a goal, Failed when no path exists.</summary>
    public static PathStatus Start(Pathfinder pathfinder, Agent a, IReadOnlyList<Int3> goals)
    {
        if (!a.IsAlive) return PathStatus.InvalidStart;
        var r = pathfinder.FindPath(a.Cell, goals);
        a.Repathed = false;
        if (!r.Found) { Stop(a, MoveStatus.Failed); return r.Status; }
        Follow(a, r.Path);
        return r.Status;
    }

    /// <summary>Starts following a path found elsewhere (the WAT-14 flee search). Same as <see cref="Start"/> after
    /// its search: a one-cell path arrives at once.</summary>
    public static void Begin(Agent a, Int3[] path)
    {
        a.Repathed = false;
        Follow(a, path);
    }

    /// <summary>One tick of movement for an agent whose status is <see cref="MoveStatus.Moving"/>. With
    /// <paramref name="swim"/> (a Flee job, ADR-031) steps may enter deep cells, and a blocked step fails the move
    /// instead of running an A* repath (the flee search is run again instead).</summary>
    public static void Advance(PathGrid grid, Pathfinder pathfinder, Agent a, bool swim = false)
    {
        if (a.Move != MoveStatus.Moving) return;
        if (a.MoveTotal == 0)
        {
            var next = a.Path[a.PathPos + 1];
            if (!CanStep(grid, a.Cell, next, swim)) { Repath(pathfinder, a, swim); return; }   // PTH-16: next tick starts the new path
            a.NextCell = next;
            a.MoveTotal = MoveTicks(grid, a.Cell, next);
        }
        a.MoveProgress++;
        if (a.MoveProgress < a.MoveTotal) return;

        if (!CanStep(grid, a.Cell, a.NextCell, swim)) { Repath(pathfinder, a, swim); return; }  // PTH-16: never enter a blocked cell
        a.Cell = a.NextCell;
        a.PathPos++;
        a.MoveProgress = 0;
        a.MoveTotal = 0;
        if (a.PathPos == a.Path.Length - 1) Stop(a, MoveStatus.Arrived);
    }

    /// <summary>Stops any move at once (a released job): the agent stays on <see cref="Agent.Cell"/>, status None.</summary>
    public static void Halt(Agent a) => Stop(a, MoveStatus.None);

    /// <summary>True if <paramref name="to"/> is one legal PTH-04..08 move from <paramref name="from"/> right now.</summary>
    public static bool CanStep(PathGrid grid, Int3 from, Int3 to, bool swim = false)
    {
        Span<PathMove> moves = stackalloc PathMove[PathMoves.MaxMoves];
        int n = PathMoves.From(grid, from, moves, swim);
        for (int i = 0; i < n; i++)
            if (moves[i].To == to) return true;
        return false;
    }

    private static void Repath(Pathfinder pathfinder, Agent a, bool swim)
    {
        var goal = a.Path[^1];
        if (a.Repathed || swim) { Stop(a, MoveStatus.Failed); return; }
        a.Repathed = true;
        var r = pathfinder.FindPath(a.Cell, goal);
        if (!r.Found) { Stop(a, MoveStatus.Failed); return; }
        Follow(a, r.Path);
    }

    private static void Follow(Agent a, Int3[] path)
    {
        a.NextCell = a.Cell;
        a.MoveProgress = 0;
        a.MoveTotal = 0;
        if (path.Length <= 1) { Stop(a, MoveStatus.Arrived); return; }
        a.Path = path;
        a.PathPos = 0;
        a.Move = MoveStatus.Moving;
    }

    private static void Stop(Agent a, MoveStatus status)
    {
        a.Move = status;
        a.Path = Array.Empty<Int3>();
        a.PathPos = 0;
        a.NextCell = a.Cell;
        a.MoveProgress = 0;
        a.MoveTotal = 0;
    }
}
