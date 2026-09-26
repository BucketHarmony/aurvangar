namespace Aurvangar.Sim.Paths;

/// <summary>Binary min-heap of open A* nodes keyed by (f, h, index) for deterministic tie-breaks (PTH-09).
/// Stale entries are allowed (lazy decrease-key); the search skips them when popped.</summary>
internal sealed class PathHeap
{
    private struct Entry
    {
        public int F, H, Index;
    }

    private Entry[] _items = new Entry[256];

    public int Count { get; private set; }

    public void Clear() => Count = 0;

    public void Push(int f, int h, int index)
    {
        if (Count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
        var e = new Entry { F = f, H = h, Index = index };
        int i = Count++;
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (!Less(e, _items[parent])) break;
            _items[i] = _items[parent];
            i = parent;
        }
        _items[i] = e;
    }

    public int PopIndex()
    {
        int top = _items[0].Index;
        var last = _items[--Count];
        int i = 0;
        int half = Count >> 1;
        while (i < half)
        {
            int child = 2 * i + 1;
            if (child + 1 < Count && Less(_items[child + 1], _items[child])) child++;
            if (!Less(_items[child], last)) break;
            _items[i] = _items[child];
            i = child;
        }
        if (Count > 0) _items[i] = last;
        return top;
    }

    private static bool Less(in Entry a, in Entry b)
    {
        if (a.F != b.F) return a.F < b.F;
        if (a.H != b.H) return a.H < b.H;
        return a.Index < b.Index;
    }
}
