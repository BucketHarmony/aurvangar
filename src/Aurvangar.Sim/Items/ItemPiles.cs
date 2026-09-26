using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Items;

/// <summary>Loose item piles on the ground (ECO-08): at most one item type per cell, unlimited count, keyed and
/// iterated by cell index. Mutated only through <see cref="Actions.WorldActions"/> (and loaders / test setup).
/// Every change emits <see cref="ItemPileChanged"/>.</summary>
public sealed class ItemPiles
{
    private readonly VoxelWorld _world;
    private readonly EventBus _events;
    private readonly SortedDictionary<int, ItemStack> _piles = new();

    public ItemPiles(VoxelWorld world, EventBus events) { _world = world; _events = events; }

    public int Count => _piles.Count;

    /// <summary>The pile at the cell, or <see cref="ItemStack.Empty"/>.</summary>
    public ItemStack At(Int3 c) =>
        _world.InBounds(c) && _piles.TryGetValue(_world.Index(c), out var s) ? s : ItemStack.Empty;

    /// <summary>All piles in ascending cell index order.</summary>
    public IEnumerable<(Int3 Cell, ItemStack Stack)> All
    {
        get { foreach (var (i, s) in _piles) yield return (_world.CellOf(i), s); }
    }

    /// <summary>True when a pile of <paramref name="item"/> may go here: in bounds, and no pile of another item.</summary>
    public bool Accepts(Int3 c, ItemId item)
    {
        if (!_world.InBounds(c)) return false;
        return !_piles.TryGetValue(_world.Index(c), out var s) || s.Item == item;
    }

    /// <summary>Adds items to the pile at the cell. Throws if the cell holds another item (callers check first).</summary>
    public void Add(Int3 c, ItemId item, int count)
    {
        if (!_world.InBounds(c)) throw new ArgumentOutOfRangeException(nameof(c), $"pile cell {c} out of bounds");
        if (!item.IsValid || count <= 0) throw new ArgumentException($"invalid pile stack {item.Value}x{count}");
        int i = _world.Index(c);
        if (_piles.TryGetValue(i, out var s))
        {
            if (s.Item != item) throw new InvalidOperationException($"ECO-08: cell {c} already holds item {s.Item.Value}");
            _piles[i] = s with { Count = s.Count + count };
        }
        else _piles.Add(i, new ItemStack(item, count));
        _events.Emit(new ItemPileChanged(c));
    }

    /// <summary>Removes up to the pile's count; an emptied pile is removed. Returns the number taken.</summary>
    public int Take(Int3 c, int count)
    {
        if (!_world.InBounds(c) || count <= 0) return 0;
        int i = _world.Index(c);
        if (!_piles.TryGetValue(i, out var s)) return 0;
        int n = Math.Min(count, s.Count);
        if (n == s.Count) _piles.Remove(i);
        else _piles[i] = s with { Count = s.Count - n };
        _events.Emit(new ItemPileChanged(c));
        return n;
    }

    /// <summary>SaveGame load: sets a pile by cell index without an event.</summary>
    internal void Restore(int index, ItemStack stack) => _piles.Add(index, stack);

    public void AddToHash(ref StateHasher h)
    {
        h.Add(_piles.Count);
        foreach (var (i, s) in _piles) { h.Add(i); h.Add(s.Item.Value); h.Add(s.Count); }
    }
}
