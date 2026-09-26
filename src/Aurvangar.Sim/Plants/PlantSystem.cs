using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Plants;

public enum PlantKind : byte { Tree = 1, Bush = 2 }

/// <summary>A tree or berry bush anchored at a base cell (WLD-07, ECO-09, ECO-10).</summary>
public sealed class Plant
{
    public PlantId Id { get; init; }
    public PlantKind Kind { get; init; }
    public Int3 Base { get; init; }

    /// <summary>Trees: trunk height in cells (occupies Base .. Base+Height-1). Bushes: 1.</summary>
    public int Height { get; init; }

    public bool MarkedForChop { get; set; }

    /// <summary>JOB-08 for chop (ADR-029): its chop job gave up after five failures. No new job is posted until the
    /// tree is designated again. The view shows it like DigUnreachable.</summary>
    public bool ChopUnreachable { get; set; }

    /// <summary>Bushes: berries available (0 = growing).</summary>
    public int Berries { get; set; }

    /// <summary>Bushes: ticks remaining until ripe.</summary>
    public int RegrowTicks { get; set; }
}

/// <summary>Owns trees and bushes. Ticked at ARCH-01 step 5: bushes regrow (ECO-10), then <see cref="BushHarvest"/>
/// keeps the bush harvest jobs on the board.</summary>
public sealed class PlantSystem
{
    public const int TreeHeight = 4;

    /// <summary>ECO-10: berries on a ripe bush, and per harvest.</summary>
    public const int BushBerries = 2;

    /// <summary>ECO-10: ticks a harvested bush grows before it is ripe again.</summary>
    public const int BushRegrowTicks = 1200;

    private readonly VoxelWorld _world;
    private readonly SortedDictionary<int, Plant> _plants = new();
    private readonly bool[] _occupied; // cells holding a trunk or bush (non-standable, WLD-07)

    public IdAllocator Ids { get; } = new();

    /// <summary>PTH-03 hook: called with a cell index whenever a trunk or bush starts or stops occupying it.
    /// PathGrid sets it. Not state: never hashed or saved.</summary>
    public Action<int>? OccupancyChanged { get; set; }

    public PlantSystem(VoxelWorld world)
    {
        _world = world;
        _occupied = new bool[world.CellCount];
    }

    /// <summary>All plants in ascending id order.</summary>
    public IEnumerable<Plant> All => _plants.Values;

    public int Count => _plants.Count;

    public Plant? Get(PlantId id) => _plants.TryGetValue(id.Value, out var p) ? p : null;

    public bool IsOccupied(Int3 c) => _world.InBounds(c) && _occupied[_world.Index(c)];

    public bool IsOccupiedAt(int index) => _occupied[index];

    public Plant AddTree(Int3 baseCell) => Add(PlantKind.Tree, baseCell, TreeHeight, berries: 0);

    public Plant AddBush(Int3 baseCell) => Add(PlantKind.Bush, baseCell, 1, berries: BushBerries);

    public void Remove(PlantId id)
    {
        if (!_plants.Remove(id.Value, out var p)) return;
        for (int h = 0; h < p.Height; h++)
        {
            var c = p.Base + Int3.Up * h;
            if (_world.InBounds(c)) SetOccupied(_world.Index(c), false);
        }
    }

    private Plant Add(PlantKind kind, Int3 baseCell, int height, int berries)
    {
        var p = new Plant { Id = new PlantId(Ids.Allocate()), Kind = kind, Base = baseCell, Height = height, Berries = berries };
        _plants.Add(p.Id.Value, p);
        for (int h = 0; h < height; h++)
        {
            var c = baseCell + Int3.Up * h;
            if (_world.InBounds(c)) SetOccupied(_world.Index(c), true);
        }
        return p;
    }

    /// <summary>SaveGame load: re-adds a plant with its saved id and fields and marks its cells occupied.</summary>
    internal void Restore(Plant p)
    {
        _plants.Add(p.Id.Value, p);
        for (int h = 0; h < p.Height; h++)
        {
            var c = p.Base + Int3.Up * h;
            if (_world.InBounds(c)) SetOccupied(_world.Index(c), true);
        }
    }

    private void SetOccupied(int index, bool value)
    {
        if (_occupied[index] == value) return;
        _occupied[index] = value;
        OccupancyChanged?.Invoke(index);
    }

    /// <summary>ARCH-01 step 5. ECO-10: a growing bush counts down and is ripe (<see cref="BushBerries"/>) on the tick
    /// its countdown reaches 0, so it is ripe again exactly <see cref="BushRegrowTicks"/> ticks after the harvest.
    /// Then the harvest jobs are synced. Crops (M6-T2) live in FarmSystem, not here.</summary>
    public void Tick(Simulation sim)
    {
        foreach (var p in _plants.Values)
        {
            if (p.Kind != PlantKind.Bush || p.Berries > 0 || p.RegrowTicks <= 0) continue;
            if (--p.RegrowTicks == 0) p.Berries = BushBerries;
        }
        BushHarvest.Sync(sim);
    }

    /// <summary>WorldActions support (ECO-10): the bush gives up its berries and starts growing.</summary>
    internal static void Harvest(Plant bush)
    {
        bush.Berries = 0;
        bush.RegrowTicks = BushRegrowTicks;
    }

    public void AddToHash(ref StateHasher h)
    {
        h.Add(Ids.Next);
        h.Add(_plants.Count);
        foreach (var p in _plants.Values)
        {
            h.Add(p.Id.Value); h.Add((byte)p.Kind); h.Add(p.Base); h.Add(p.Height);
            h.Add(p.MarkedForChop); h.Add(p.ChopUnreachable); h.Add(p.Berries); h.Add(p.RegrowTicks);
        }
    }
}
