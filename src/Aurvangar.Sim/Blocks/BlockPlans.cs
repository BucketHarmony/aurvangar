using Aurvangar.Sim.Content;
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
public enum BuildStatus : byte { Planned, InJob, GivenUp, BelowFirst, WaitSupport, NoSupport, CourseBelow, Occupied, NoAccess, WouldStrand, NoMaterial, Ready }

/// <summary>CON-04 (M8-T2, ADR-062): the plan entries by cell index. Sim state: saved (section <c>BlockPlans</c>) and
/// hashed (only when not empty). Entries exist only for cells that are not built yet: the Place step removes its entry,
/// and <see cref="BlockBuildSystem"/> drops any entry whose cell became solid. Validity (CON-08) and statuses (CON-05)
/// live in BlockPlans.Rules.cs.</summary>
public sealed partial class BlockPlans
{
    private readonly VoxelWorld _world;
    private readonly ContentDb _content;
    private readonly SortedDictionary<int, PlanEntry> _entries = new();

    public BlockPlans(VoxelWorld world, ContentDb content) { _world = world; _content = content; }

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

    /// <summary>CON-06: for each item, the cost summed over the entries in <paramref name="state"/> (all entries when
    /// null), ascending item id; items with no cost are left out. O(entries), no cache.</summary>
    public IReadOnlyList<(ItemId Item, int Count)> Needed(PlanState? state)
    {
        var sums = new SortedDictionary<int, int>();
        foreach (var e in _entries.Values)
        {
            if (state is { } s && e.State != s) continue;
            var (item, cost) = _content.CostOf(e.Block);
            if (cost <= 0) continue;
            sums[item.Value] = sums.GetValueOrDefault(item.Value) + cost;
        }
        var result = new List<(ItemId, int)>(sums.Count);
        foreach (var (item, n) in sums) result.Add((new ItemId(item), n));
        return result;
    }

    /// <summary>CON-07 <c>ReleasePlan</c>: every Planned entry in the box spanned by <paramref name="a"/> and
    /// <paramref name="b"/> (corners in any order, inclusive) becomes Released. O(entries). Returns how many.</summary>
    public int Release(Int3 a, Int3 b)
    {
        var min = new Int3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
        var max = new Int3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
        var hits = new List<int>();
        foreach (var (i, e) in _entries)
        {
            if (e.State != PlanState.Planned) continue;
            var c = _world.CellOf(i);
            if (c.X >= min.X && c.X <= max.X && c.Y >= min.Y && c.Y <= max.Y && c.Z >= min.Z && c.Z <= max.Z) hits.Add(i);
        }
        foreach (var i in hits) _entries[i] = _entries[i] with { State = PlanState.Released };
        return hits.Count;
    }

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
