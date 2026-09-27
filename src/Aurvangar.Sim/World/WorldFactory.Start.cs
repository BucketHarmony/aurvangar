using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Paths;

namespace Aurvangar.Sim.World;

/// <summary>M11-T2 (BLD-15, ADR-077): the start buildings other than the Great Hall (the wagon).</summary>
public static partial class WorldFactory
{
    /// <summary>How far (Chebyshev, x/z) the wagon's footprint may be from the hall's: at least one walkable cell between
    /// them, at most <see cref="MaxStartGap"/>.</summary>
    public const int MinStartGap = 2, MaxStartGap = 4;

    /// <summary>How far above or below the hall's floor a start building may stand.</summary>
    public const int MaxStartRise = 2;

    /// <summary>BLD-15: every other building with a <c>startStock</c>, in data order, is placed complete beside the hall
    /// (<see cref="FindStartSite"/>). Throws when there is no site (the world is unusable).</summary>
    private static void PlaceStartBuildings(Simulation sim, Building hall)
    {
        foreach (var def in sim.Content.Buildings)
        {
            if (def.StartStock is null || def.Id == hall.Def.Id) continue;
            if (!TryPlaceStart(sim, hall, def))
                throw new InvalidOperationException($"WorldFactory: no site for the {def.Name} beside the {hall.Def.Name}");
        }
    }

    /// <summary>Tries the candidate sites nearest first (<see cref="StartCandidates"/>). A site must pass the placement
    /// rules (BLD-02, bar prebuilt-only), be dry, and have its entrance reachable from the hall's; once placed, the hall's
    /// entrance must still reach it, else it is taken back and the next site is tried.</summary>
    private static bool TryPlaceStart(Simulation sim, Building hall, BuildingDef def)
    {
        var reach = Reach(sim, hall.EntranceCell);
        foreach (var (origin, rot) in StartCandidates(hall, def))
        {
            if (sim.Buildings.CanPlaceAtStart(def, origin, rot) != PlacementResult.Ok) continue;
            var entrance = BuildingShape.Entrance(def, origin, rot);
            if (!reach[sim.World.Index(entrance)] || sim.PathGrid.IsWet(entrance)) continue;
            if (BuildingShape.Footprint(def, origin, rot).Any(c => sim.Water.GetLevel(c) > 0)) continue;

            var b = sim.Buildings.PlacePrebuilt(def, origin, rot);
            if (Reach(sim, hall.EntranceCell)[sim.World.Index(entrance)]) return true;
            foreach (var c in b.FootprintCells()) sim.World.SetBlock(c, BlockId.Air);
            sim.Buildings.Remove(b);
        }
        return false;
    }

    /// <summary>Origins and rotations around the hall, nearest first: by footprint gap (<see cref="MinStartGap"/>..
    /// <see cref="MaxStartGap"/>), then height difference from the hall's floor, then z, x and rotation.</summary>
    private static IEnumerable<(Int3 Origin, int Rotation)> StartCandidates(Building hall, BuildingDef def)
    {
        var (hMin, hMax) = FootprintBox(hall.Def, hall.Origin, hall.Rotation);
        int reachXZ = MaxStartGap + Math.Max(def.Footprint[0], def.Footprint[2]);
        var list = new List<(int Gap, int Rise, int Z, int X, int Rot, Int3 Origin)>();
        for (int dy = -MaxStartRise; dy <= MaxStartRise; dy++)
            for (int z = hMin.Z - reachXZ; z <= hMax.Z + reachXZ; z++)
                for (int x = hMin.X - reachXZ; x <= hMax.X + reachXZ; x++)
                    for (int rot = 0; rot < 360; rot += 90)
                    {
                        var origin = new Int3(x, hall.Origin.Y + dy, z);
                        var (min, max) = FootprintBox(def, origin, rot);
                        int gx = Math.Max(Math.Max(hMin.X - max.X, min.X - hMax.X), 0);
                        int gz = Math.Max(Math.Max(hMin.Z - max.Z, min.Z - hMax.Z), 0);
                        int gap = Math.Max(gx, gz);
                        if (gap < MinStartGap || gap > MaxStartGap) continue;
                        list.Add((gap, Math.Abs(dy), z, x, rot, origin));
                    }
        return list.OrderBy(c => c.Gap).ThenBy(c => c.Rise).ThenBy(c => c.Z).ThenBy(c => c.X).ThenBy(c => c.Rot)
            .Select(c => (c.Origin, c.Rot));
    }

    private static (Int3 Min, Int3 Max) FootprintBox(BuildingDef def, Int3 origin, int rotation)
    {
        var min = new Int3(int.MaxValue, int.MaxValue, int.MaxValue);
        var max = new Int3(int.MinValue, int.MinValue, int.MinValue);
        foreach (var c in BuildingShape.Footprint(def, origin, rotation))
        {
            min = new Int3(Math.Min(min.X, c.X), Math.Min(min.Y, c.Y), Math.Min(min.Z, c.Z));
            max = new Int3(Math.Max(max.X, c.X), Math.Max(max.Y, c.Y), Math.Max(max.Z, c.Z));
        }
        return (min, max);
    }

    /// <summary>The walkable cells reachable from <paramref name="from"/> by the PTH-04..08 moves (breadth first).</summary>
    private static bool[] Reach(Simulation sim, Int3 from)
    {
        var grid = sim.PathGrid;
        var seen = new bool[sim.World.CellCount];
        if (!grid.IsWalkable(from)) return seen;
        var queue = new Queue<Int3>();
        Span<PathMove> moves = stackalloc PathMove[PathMoves.MaxMoves];
        queue.Enqueue(from);
        seen[sim.World.Index(from)] = true;
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            int n = PathMoves.From(grid, c, moves);
            for (int i = 0; i < n; i++)
            {
                int idx = sim.World.Index(moves[i].To);
                if (seen[idx]) continue;
                seen[idx] = true;
                queue.Enqueue(moves[i].To);
            }
        }
        return seen;
    }
}
