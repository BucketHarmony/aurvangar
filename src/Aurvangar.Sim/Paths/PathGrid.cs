using Aurvangar.Sim.Core;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Paths;

/// <summary>Walkability cache. Spec: docs/specs/pathfinding.md PTH-01..03 (ADR-023).
/// One flag byte per cell, computed lazily on first query and cleared (with its 3x3x3 neighborhood) when a block,
/// a water level class or a plant's occupancy changes. Block changes are read from
/// <see cref="VoxelWorld.ChangedCells"/> through a cursor at every query, so changes are visible at once, also
/// mid-tick; water and plants push changes through their hooks. The cache is derived data: not hashed, not saved.
/// </summary>
public sealed class PathGrid
{
    private const byte Valid = 1 << 7;
    private const byte Standable = 1 << 0;
    private const byte Walkable = 1 << 1;
    private const byte Wet = 1 << 2;

    private readonly VoxelWorld _world;
    private readonly WaterGrid _water;
    private readonly PlantSystem _plants;
    private readonly byte[] _flags;

    /// <summary>Total world changes (<c>ChangeLogBase + ChangedCells.Count</c>) already applied to the cache.</summary>
    private long _worldSeen;

    public PathGrid(VoxelWorld world, WaterGrid water, PlantSystem plants)
    {
        _world = world; _water = water; _plants = plants;
        _flags = new byte[world.CellCount];
        _worldSeen = world.ChangeLogBase + world.ChangedCells.Count;
        water.WalkClassChanged = OnWaterClassChanged;
        plants.OccupancyChanged = OnOccupancyChanged;
    }

    public VoxelWorld World => _world;

    /// <summary>Diagnostics: number of cell flag computations so far. Never read by gameplay code.</summary>
    public long FlagComputations { get; private set; }

    /// <summary>Bumped whenever walkability or a move rule input may have changed: any block change, plant
    /// occupancy change, shallow/deep water crossing in a standable cell, or <see cref="InvalidateAll"/>. Dry/wet crossings (wading cost
    /// only) do not bump it. <see cref="Regions"/> rebuilds when it differs from the version it was built at (PTH-13).
    /// Derived, not state.</summary>
    public long WalkabilityVersion { get; private set; }

    /// <summary>PTH-01.</summary>
    public bool IsStandable(Int3 c) => (Flags(c) & Standable) != 0;

    /// <summary>PTH-02 (construction-site blocking is added in M5-T2).</summary>
    public bool IsWalkable(Int3 c) => (Flags(c) & Walkable) != 0;

    /// <summary>The cell holds any water (PTH-07 wading cost).</summary>
    public bool IsWet(Int3 c) => (Flags(c) & Wet) != 0;

    /// <summary>Apply pending world changes. Called by Simulation at tick end, before the change log is cleared.
    /// Queries also do this, so calling it is never required for correctness.</summary>
    public void SyncWorldChanges()
    {
        var changes = _world.ChangedCells;
        long logBase = _world.ChangeLogBase;
        long total = logBase + changes.Count;
        if (_worldSeen == total) return;
        if (_worldSeen < logBase)
        {
            InvalidateAll();                      // entries were cleared before this grid saw them
        }
        else
        {
            for (int k = (int)(_worldSeen - logBase); k < changes.Count; k++) InvalidateAround(changes[k]);
            WalkabilityVersion++;
        }
        _worldSeen = total;
    }

    /// <summary>Drop every cached flag. For writers that bypass the change log (SetBlockRaw, save load).</summary>
    public void InvalidateAll()
    {
        Array.Clear(_flags);
        WalkabilityVersion++;
        _worldSeen = _world.ChangeLogBase + _world.ChangedCells.Count;
    }

    private byte Flags(Int3 c)
    {
        if (!_world.InBounds(c)) return 0;
        SyncWorldChanges();
        int i = _world.Index(c);
        byte f = _flags[i];
        if ((f & Valid) != 0) return f;
        f = Compute(c, i);
        _flags[i] = f;
        return f;
    }

    private byte Compute(Int3 c, int i)
    {
        FlagComputations++;
        byte f = Valid;
        int level = _water.GetLevelAt(i);
        if (level > 0) f |= Wet;
        if (StandableAt(c, i))
        {
            f |= Standable;
            if (level < WaterGrid.Full / 2) f |= Walkable;   // WAT-14
        }
        return f;
    }

    /// <summary>PTH-01 evaluated directly (water never affects standability).</summary>
    private bool StandableAt(Int3 c, int i) =>
        !_world.IsSolidAt(i) && !_plants.IsOccupiedAt(i) && !_world.IsSolid(c + Int3.Up) && _world.IsSolid(c + Int3.Down);

    private void OnWaterClassChanged(int index, bool deepChanged)
    {
        InvalidateAround(index);
        // Only a standable cell's walkability depends on its depth; deep crossings elsewhere (mid-column, no floor)
        // change nothing a move reads (ADR-025).
        if (deepChanged && StandableAt(_world.CellOf(index), index)) WalkabilityVersion++;
    }

    private void OnOccupancyChanged(int index)
    {
        InvalidateAround(index);
        WalkabilityVersion++;
    }

    /// <summary>PTH-03: clear the cell and its 3x3x3 neighborhood.</summary>
    private void InvalidateAround(int index)
    {
        var c = _world.CellOf(index);
        int x0 = Math.Max(c.X - 1, 0), x1 = Math.Min(c.X + 1, _world.SizeX - 1);
        int y0 = Math.Max(c.Y - 1, 0), y1 = Math.Min(c.Y + 1, _world.SizeY - 1);
        int z0 = Math.Max(c.Z - 1, 0), z1 = Math.Min(c.Z + 1, _world.SizeZ - 1);
        for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    _flags[_world.Index(x, y, z)] = 0;
    }
}
