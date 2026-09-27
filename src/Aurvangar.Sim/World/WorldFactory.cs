using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Paths;

namespace Aurvangar.Sim.World;

/// <summary>Builds the standard game world: 128x64x128, terrain, hub, plants, colonists, pre-settled river.</summary>
public static partial class WorldFactory
{
    public const int SizeX = 128, SizeY = 64, SizeZ = 128;

    public static Simulation Create(ulong seed, ContentDb content)
    {
        var sim = new Simulation(content, SizeX, SizeY, SizeZ, seed);
        var terrain = TerrainGenerator.Generate(sim.World, seed);   // M1-T3
        var hub = content.Building("hub");
        var hall = sim.Buildings.PlacePrebuilt(hub, terrain.HubOrigin(hub.Footprint), 0);
        foreach (var t in terrain.TreeBases) sim.Plants.AddTree(t);
        foreach (var b in terrain.BushBases) sim.Plants.AddBush(b);
        PreSettleRiver(sim, terrain);
        PlaceStartBuildings(sim, hall);   // M11-T2: the wagon (BLD-15)
        SpawnColonists(sim, hall.EntranceCell);
        StartingStock(sim);
        sim.World.ClearChangeLog();
        sim.PathGrid.ClearWalkChanges();   // worldgen is not a JOB-12 walkability change
        sim.World.MarkAllDirty();
        sim.Events.Drain();   // the initial world (hub, colonists) is read by the view directly, not announced (ADR-026)
        return sim;
    }

    /// <summary>BLD-15 (M11-T2, ADR-077): every start building holds its <c>startStock</c> from buildings.json (the Great
    /// Hall its food and water, the wagon the building supplies).</summary>
    private static void StartingStock(Simulation sim)
    {
        foreach (var b in sim.Buildings.All)
            if (b.Def.StartStock is { } stock)
                foreach (var (item, n) in stock) b.Stored[sim.Content.Item(item).Value] = n;
    }

    /// <summary>GEN-08 pre-settle length in ticks.</summary>
    public const int PreSettleTicks = 600;

    /// <summary>GEN-08: register sources and drains, fill the channel, and run the full tick loop so the river is
    /// settled at tick 0. The clock and water stats are reset afterwards (ADR-014); Create drains the events.</summary>
    private static void PreSettleRiver(Simulation sim, TerrainResult terrain)
    {
        foreach (var c in terrain.WaterSources) sim.Water.AddSource(c);
        foreach (var c in terrain.WaterDrains) sim.Water.AddDrain(c);
        foreach (var c in terrain.InitialWater) sim.Water.SetLevel(c, Water.WaterGrid.Full);
        sim.RunTicks(PreSettleTicks);
        sim.Clock.Tick = 0;
        sim.Water.ResetStats();
    }

    /// <summary>The five starting dwarves, named from the Dvergatal (docs/00-overview.md). ASCII spellings.</summary>
    public static readonly string[] ColonistNames = { "Dvalinn", "Althjofr", "Nyradr", "Reginn", "Hanarr" };

    /// <summary>M4-T4 (ADR-026): one colonist per name on the first dry walkable cells met by a breadth-first walk from
    /// the hub entrance using the PTH-04..08 move rules (fixed neighbor order, so the result is deterministic).</summary>
    private static void SpawnColonists(Simulation sim, Int3 entrance)
    {
        var grid = sim.PathGrid;
        var world = sim.World;
        var seen = new bool[world.CellCount];
        var queue = new Queue<Int3>();
        var cells = new List<Int3>();
        Span<PathMove> moves = stackalloc PathMove[PathMoves.MaxMoves];
        if (grid.IsWalkable(entrance)) { queue.Enqueue(entrance); seen[world.Index(entrance)] = true; }
        while (queue.Count > 0 && cells.Count < ColonistNames.Length)
        {
            var c = queue.Dequeue();
            if (!grid.IsWet(c)) cells.Add(c);
            int n = PathMoves.From(grid, c, moves);
            for (int i = 0; i < n; i++)
            {
                int idx = world.Index(moves[i].To);
                if (seen[idx]) continue;
                seen[idx] = true;
                queue.Enqueue(moves[i].To);
            }
        }
        if (cells.Count < ColonistNames.Length)
            throw new InvalidOperationException($"WorldFactory: only {cells.Count} spawn cells near the hub entrance {entrance}");
        for (int i = 0; i < ColonistNames.Length; i++) sim.Agents.Spawn(cells[i], ColonistNames[i]);
    }
}
