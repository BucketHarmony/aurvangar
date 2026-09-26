using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Water;

/// <summary>Volume accounting for conservation tests (WAT-11).</summary>
public sealed class WaterStats
{
    public long SourceAdded { get; set; }
    public long Drained { get; set; }
    public long Evaporated { get; set; }
    public long Pumped { get; set; }
}

/// <summary>Cellular-automaton water. Spec: docs/specs/water.md.
/// SCAFFOLD: storage, sources/drains lists and accessors exist; Tick is a no-op until M2.</summary>
public sealed class WaterGrid
{
    /// <summary>WAT-01: units per full cell.</summary>
    public const int Full = 1024;

    private readonly VoxelWorld _world;
    private readonly ushort[] _level;
    private readonly List<int> _sources = new();
    private readonly List<int> _drains = new();

    public WaterStats Stats { get; } = new();

    /// <summary>WAT-09: 0..100, set by WeatherSystem.</summary>
    public int SourceStrength { get; set; } = 100;

    public WaterGrid(VoxelWorld world)
    {
        _world = world;
        _level = new ushort[world.CellCount];
    }

    public int GetLevel(Int3 c) => _world.InBounds(c) ? _level[_world.Index(c)] : 0;

    public int GetLevelAt(int index) => _level[index];

    public ReadOnlySpan<ushort> Levels => _level;

    internal Span<ushort> LevelsMutable => _level;

    /// <summary>Set a level directly (setup, tests, loader). Clamps to [0, Full]. Solid cells are forced to 0.
    /// M2-T1: must also activate the cell and its neighbors.</summary>
    public void SetLevel(Int3 c, int level)
    {
        if (!_world.InBounds(c)) return;
        int i = _world.Index(c);
        _level[i] = _world.IsSolidAt(i) ? (ushort)0 : (ushort)Math.Clamp(level, 0, Full);
    }

    public void AddSource(Int3 c) { if (_world.InBounds(c)) _sources.Add(_world.Index(c)); }

    public void AddDrain(Int3 c) { if (_world.InBounds(c)) _drains.Add(_world.Index(c)); }

    public IReadOnlyList<int> Sources => _sources;

    public IReadOnlyList<int> Drains => _drains;

    /// <summary>Number of cells in the active set (WAT-02).</summary>
    public int ActiveCount => 0; // M2-T1

    /// <summary>Sum of all levels. Used by conservation tests.</summary>
    public long TotalVolume()
    {
        long sum = 0;
        foreach (var v in _level) sum += v;
        return sum;
    }

    /// <summary>One CA step (WAT-03..16). M2-T1..T4.</summary>
    public void Tick(EventBus events)
    {
        // Intentionally empty until M2. Do not add logic here without un-skipping the WaterGridTests first.
    }

    /// <summary>A cell is deep (not walkable) at or above half a block (WAT-14).</summary>
    public bool IsDeep(Int3 c) => GetLevel(c) >= Full / 2;
}
