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

/// <summary>Cellular-automaton water. Spec: docs/specs/water.md. The step itself lives in WaterGrid.Step.cs.</summary>
public sealed partial class WaterGrid
{
    /// <summary>WAT-01: units per full cell.</summary>
    public const int Full = 1024;

    private readonly VoxelWorld _world;
    private readonly ushort[] _level;
    private readonly List<int> _sources = new();
    private readonly List<int> _drains = new();
    private readonly WaterActiveSet _active;

    public WaterStats Stats { get; } = new();

    /// <summary>WAT-09: 0..100, set by WeatherSystem.</summary>
    public int SourceStrength { get; set; } = 100;

    public WaterGrid(VoxelWorld world)
    {
        _world = world;
        _level = new ushort[world.CellCount];
        _active = new WaterActiveSet(world.CellCount);
        _delta = new int[world.CellCount];
        _touchedFlag = new bool[world.CellCount];
        _emitted = new ushort[world.CellCount];
        _drift = new bool[world.CellCount];
        _chunkDrift = new List<int>?[world.ChunkCount];
        _chunkEmit = new bool[world.ChunkCount];
    }

    public int GetLevel(Int3 c) => _world.InBounds(c) ? _level[_world.Index(c)] : 0;

    public int GetLevelAt(int index) => _level[index];

    public ReadOnlySpan<ushort> Levels => _level;

    internal Span<ushort> LevelsMutable => _level;

    /// <summary>Set a level directly (setup, tests, loader). Clamps to [0, Full]. Solid cells are forced to 0.
    /// A change activates the cell and its 6 neighbors (WAT-02).</summary>
    public void SetLevel(Int3 c, int level)
    {
        if (!_world.InBounds(c)) return;
        int i = _world.Index(c);
        ushort v = _world.IsSolidAt(i) ? (ushort)0 : (ushort)Math.Clamp(level, 0, Full);
        int old = _level[i];
        if (old == v) return;
        _level[i] = v;
        NoteWalkClass(i, old);
        NoteLevelChange(i);
        ActivateAround(i);
    }

    public void AddSource(Int3 c) { if (_world.InBounds(c)) _sources.Add(_world.Index(c)); }

    public void AddDrain(Int3 c) { if (_world.InBounds(c)) _drains.Add(_world.Index(c)); }

    public IReadOnlyList<int> Sources => _sources;

    public IReadOnlyList<int> Drains => _drains;

    /// <summary>Zero the volume counters so conservation (WAT-11) is measured from the current total. Used after
    /// the world-creation pre-settle (ADR-014).</summary>
    public void ResetStats()
    {
        Stats.SourceAdded = 0; Stats.Drained = 0; Stats.Evaporated = 0; Stats.Pumped = 0;
    }

    /// <summary>Number of cells in the active set (WAT-02).</summary>
    public int ActiveCount => _active.Count;

    /// <summary>Sum of all levels. Used by conservation tests.</summary>
    public long TotalVolume()
    {
        long sum = 0;
        foreach (var v in _level) sum += v;
        return sum;
    }

    /// <summary>ARCH-06: levels, active set (sorted), source strength, sources, drains (list order), and volume
    /// accounting. The active set is state: which cells step next changes the outcome (ADR-010).</summary>
    public void AddToHash(ref StateHasher h)
    {
        h.Add(Levels);
        var active = _active.Sorted();
        h.Add(active.Count); foreach (var i in active) h.Add(i);
        h.Add(SourceStrength);
        h.Add(_sources.Count); foreach (var i in _sources) h.Add(i);
        h.Add(_drains.Count); foreach (var i in _drains) h.Add(i);
        h.Add(Stats.SourceAdded); h.Add(Stats.Drained); h.Add(Stats.Evaporated); h.Add(Stats.Pumped);
    }

    /// <summary>A cell is deep (not walkable) at or above half a block (WAT-14).</summary>
    public bool IsDeep(Int3 c) => GetLevel(c) >= Full / 2;

    /// <summary>PTH-03 hook: called with a cell index whenever that cell's level crosses dry/wet (0) or shallow/deep
    /// (<c>Full / 2</c>); the flag is true when the shallow/deep line was crossed (walkability changed, not only the
    /// wading cost). PathGrid sets it. Not state: never hashed or saved.</summary>
    public Action<int, bool>? WalkClassChanged { get; set; }

    /// <summary>0 dry, 1 wet but wadeable, 2 deep (WAT-14).</summary>
    private static int WalkClass(int level) => level == 0 ? 0 : level < Full / 2 ? 1 : 2;

    /// <summary>Notify <see cref="WalkClassChanged"/> if cell <paramref name="i"/> changed class since
    /// <paramref name="oldLevel"/>. Call after writing the new level.</summary>
    private void NoteWalkClass(int i, int oldLevel)
    {
        if (WalkClassChanged == null) return;
        int was = WalkClass(oldLevel), now = WalkClass(_level[i]);
        if (was != now) WalkClassChanged(i, (was == 2) != (now == 2));
    }
}
