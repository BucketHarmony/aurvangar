using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Blocks;

/// <summary>CON-04: whether dwarves build an entry. Never renumber: saved and hashed.</summary>
public enum PlanState : byte { Planned = 1, Released = 2 }

/// <summary>CON-04: one cell the player wants filled with a construction block.</summary>
public readonly record struct PlanEntry(BlockId Block, PlanState State);

/// <summary>CON-08: why a cell may not get a plan entry (the first failing check), or Ok.</summary>
public enum PlanResult : byte { Ok, OutOfWorld, Solid, Building, Plant, Farm, Unsupported }

/// <summary>CON-05: why an entry waits (derived, never stored). The first check that applies, in this order.</summary>
public enum BuildStatus : byte { Planned, InJob, GivenUp, BelowFirst, NoSupport, Occupied, NoAccess, WouldStrand, NoMaterial, Ready }

/// <summary>CON-04 (M8-T2, ADR-062): the plan entries by cell index. Sim state: saved (section <c>BlockPlans</c>) and
/// hashed (only when not empty). Entries exist only for cells that are not built yet: the Place step removes its entry,
/// and <see cref="BlockBuildSystem"/> drops any entry whose cell became solid. Validity (CON-08) and statuses (CON-05)
/// live in BlockPlans.Rules.cs.</summary>
public sealed partial class BlockPlans
{
    private readonly VoxelWorld _world;
    private readonly SortedDictionary<int, PlanEntry> _entries = new();

    public BlockPlans(VoxelWorld world) { _world = world; }

    public int Count => _entries.Count;

    /// <summary>All entries, ascending cell index (which is ascending (y, index) order).</summary>
    public IEnumerable<(Int3 Cell, PlanEntry Entry)> All
    {
        get
        {
            foreach (var (i, e) in _entries) yield return (_world.CellOf(i), e);
        }
    }

    public PlanEntry? Get(Int3 c) =>
        _world.InBounds(c) && _entries.TryGetValue(_world.Index(c), out var e) ? e : null;

    public bool Has(Int3 c) => _world.InBounds(c) && _entries.ContainsKey(_world.Index(c));

    internal bool HasAt(int index) => _entries.ContainsKey(index);

    public void Set(Int3 c, PlanEntry e)
    {
        if (!_world.InBounds(c)) throw new ArgumentOutOfRangeException(nameof(c));
        _entries[_world.Index(c)] = e;
    }

    public bool Remove(Int3 c) => _world.InBounds(c) && _entries.Remove(_world.Index(c));

    public void Clear() => _entries.Clear();

    /// <summary>SaveGame load.</summary>
    internal void Restore(int index, PlanEntry e) => _entries[index] = e;

    /// <summary>Hashed only when there is an entry, so a game without plans hashes as before M8-T2.</summary>
    public void AddToHash(ref StateHasher h)
    {
        if (_entries.Count == 0) return;
        h.Add(_entries.Count);
        foreach (var (i, e) in _entries)
        {
            h.Add(i); h.Add((byte)e.Block); h.Add((byte)e.State);
        }
    }
}
