using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Paths;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Buildings;

/// <summary>Placement, construction, storage, production. Spec: docs/specs/buildings.md.
/// Placement validation and blueprints: BuildingSystem.Placement.cs (M5-T1). Construction is M5-T2.</summary>
public sealed partial class BuildingSystem
{
    private readonly VoxelWorld _world;
    private readonly PlantSystem _plants;
    private readonly PathGrid _paths;
    private readonly SortedDictionary<int, Building> _buildings = new();

    public IdAllocator Ids { get; } = new();

    public BuildingSystem(VoxelWorld world, PlantSystem plants, PathGrid paths)
    {
        _world = world; _plants = plants; _paths = paths;
    }

    public IEnumerable<Building> All => _buildings.Values;

    public Building? Get(BuildingId id) => _buildings.TryGetValue(id.Value, out var b) ? b : null;

    /// <summary>Place a complete building without construction (the hub at world creation). Writes BuildingSolid.</summary>
    public Building PlacePrebuilt(BuildingDef def, Int3 origin, int rotation)
    {
        var b = new Building { Id = new BuildingId(Ids.Allocate()), Def = def, Origin = origin, Rotation = rotation, State = BuildingState.Complete };
        _buildings.Add(b.Id.Value, b);
        if (def.SetsBlocks)
            foreach (var c in b.FootprintCells()) _world.SetBlock(c, BlockId.BuildingSolid);
        return b;
    }

    /// <summary>SaveGame load: re-adds a building with its saved id. Blocks are loaded separately, so none are written.</summary>
    internal void Restore(Building b) => _buildings.Add(b.Id.Value, b);

    /// <summary>BLD-12: stored items summed over all storage buildings by ItemId.Value, recomputed each tick for the
    /// HUD (complete storage buildings only). Computed at the buildings step (ARCH-01 step 7), so it lags hauling and
    /// eating by one tick and is empty after a load until the next tick. Derived from <see cref="Building.Stored"/>, so
    /// not hashed or saved.</summary>
    public IReadOnlyDictionary<int, int> Totals => _totals;

    private readonly SortedDictionary<int, int> _totals = new();

    public void Tick(Simulation sim)
    {
        // M5-T2: construction jobs and completion. M5-T4: pump production.
        _totals.Clear();
        foreach (var b in _buildings.Values)
        {
            if (b.State != BuildingState.Complete || b.Def.Storage is null) continue;
            foreach (var (item, n) in b.Stored) _totals[item] = (_totals.TryGetValue(item, out var t) ? t : 0) + n;
        }
    }

    public void AddToHash(ref StateHasher h)
    {
        h.Add(Ids.Next);
        h.Add(_buildings.Count);
        foreach (var b in _buildings.Values)
        {
            h.Add(b.Id.Value); h.Add(b.Def.Id.Length); foreach (var ch in b.Def.Id) h.Add((byte)ch);
            h.Add(b.Origin); h.Add(b.Rotation); h.Add((byte)b.State); h.Add(b.Progress); h.Add(b.NoWater);
            h.Add(b.Delivered.Count); foreach (var (k, v) in b.Delivered) { h.Add(k); h.Add(v); }
            h.Add(b.Stored.Count); foreach (var (k, v) in b.Stored) { h.Add(k); h.Add(v); }
        }
    }
}
