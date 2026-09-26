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

    /// <summary>Bushes: berries available (0 = growing).</summary>
    public int Berries { get; set; }

    /// <summary>Bushes: ticks remaining until ripe.</summary>
    public int RegrowTicks { get; set; }
}

/// <summary>Owns trees and bushes. SCAFFOLD: storage and occupancy work; growth logic is M6-T3.</summary>
public sealed class PlantSystem
{
    public const int TreeHeight = 4;

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

    public Plant AddBush(Int3 baseCell) => Add(PlantKind.Bush, baseCell, 1, berries: 2);

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

    private void SetOccupied(int index, bool value)
    {
        if (_occupied[index] == value) return;
        _occupied[index] = value;
        OccupancyChanged?.Invoke(index);
    }

    public void Tick(SimClock clock)
    {
        // M6-T3: bush regrowth. M6-T2 crops live in FarmSystem, not here.
    }

    public void AddToHash(ref StateHasher h)
    {
        h.Add(Ids.Next);
        h.Add(_plants.Count);
        foreach (var p in _plants.Values)
        {
            h.Add(p.Id.Value); h.Add((byte)p.Kind); h.Add(p.Base); h.Add(p.Height);
            h.Add(p.MarkedForChop); h.Add(p.Berries); h.Add(p.RegrowTicks);
        }
    }
}
