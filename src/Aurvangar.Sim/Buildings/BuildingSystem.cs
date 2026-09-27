using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Paths;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Buildings;

/// <summary>Placement, construction, storage, production. Spec: docs/specs/buildings.md.
/// Placement validation and blueprints: BuildingSystem.Placement.cs (M5-T1). Construction jobs, completion,
/// cancel and deconstruction: <see cref="Construction"/> and WorldActions.Construction.cs (M5-T2). Pump jobs:
/// <see cref="Pumps"/> (M5-T4).</summary>
public sealed partial class BuildingSystem
{
    private readonly VoxelWorld _world;
    private readonly PlantSystem _plants;
    private readonly PathGrid _paths;
    private readonly Blocks.BlockPlans _plans;
    private readonly SortedDictionary<int, Building> _buildings = new();

    /// <summary>Footprint cell index -> id of the lowest-id building covering it. Derived (kept by Add/Remove, also on
    /// load through <see cref="Restore"/>); lookups only, never enumerated.</summary>
    private readonly Dictionary<int, int> _cellOwner = new();

    public IdAllocator Ids { get; } = new();

    public BuildingSystem(VoxelWorld world, PlantSystem plants, PathGrid paths, Blocks.BlockPlans plans)
    {
        _world = world; _plants = plants; _paths = paths; _plans = plans;
    }

    public IEnumerable<Building> All => _buildings.Values;

    public Building? Get(BuildingId id) => _buildings.TryGetValue(id.Value, out var b) ? b : null;

    /// <summary>Place a complete building without construction (the hub at world creation). Writes BuildingSolid.</summary>
    public Building PlacePrebuilt(BuildingDef def, Int3 origin, int rotation)
    {
        var b = new Building { Id = new BuildingId(Ids.Allocate()), Def = def, Origin = origin, Rotation = rotation, State = BuildingState.Complete };
        Add(b);
        if (def.SetsBlocks)
            foreach (var c in b.FootprintCells()) _world.SetBlock(c, BlockId.BuildingSolid);
        return b;
    }

    /// <summary>SaveGame load: re-adds a building with its saved id. Blocks are loaded separately, so none are written.</summary>
    internal void Restore(Building b) => Add(b);

    /// <summary>SaveGame load: re-derives construction-site blocking (PTH-02) from the loaded building states.</summary>
    internal void AfterLoad()
    {
        foreach (var b in _buildings.Values)
            if (b.State == BuildingState.UnderConstruction)
                foreach (var c in b.FootprintCells()) _paths.SetSite(c, true);
    }

    private void Add(Building b)
    {
        _buildings.Add(b.Id.Value, b);
        foreach (var c in b.FootprintCells())
        {
            if (!_world.InBounds(c)) continue;
            int i = _world.Index(c);
            if (!_cellOwner.TryGetValue(i, out var owner) || owner > b.Id.Value) _cellOwner[i] = b.Id.Value;
        }
    }

    /// <summary>Removes a building (cancelled or deconstructed, BLD-09). Blocks, jobs and site cells are the caller's.</summary>
    internal void Remove(Building b)
    {
        _buildings.Remove(b.Id.Value);
        foreach (var c in b.FootprintCells())
        {
            if (!_world.InBounds(c)) continue;
            int i = _world.Index(c);
            if (!_cellOwner.TryGetValue(i, out var owner) || owner != b.Id.Value) continue;
            _cellOwner.Remove(i);
            foreach (var other in _buildings.Values)   // overlapping prebuilt buildings only (tests); lowest id wins
                if (other.Covers(c)) { _cellOwner[i] = other.Id.Value; break; }
        }
    }

    /// <summary>BLD-12: stored items summed over all storage buildings by ItemId.Value, recomputed each tick for the
    /// HUD (complete storage buildings only). Computed at the buildings step (ARCH-01 step 7), so it lags hauling and
    /// eating by one tick and is empty after a load until the next tick. Derived from <see cref="Building.Stored"/>, so
    /// not hashed or saved.</summary>
    public IReadOnlyDictionary<int, int> Totals => _totals;

    private readonly SortedDictionary<int, int> _totals = new();

    public void Tick(Simulation sim)
    {
        Construction.Tick(sim);   // M5-T2: BLD-06 delivers, BLD-08 construct, BLD-09 deconstruct jobs
        Pumps.Tick(sim);          // M5-T4: BLD-13 NoWater and OperatePump jobs, BLD-14 buffer hauls
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
