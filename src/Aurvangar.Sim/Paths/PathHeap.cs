namespace Aurvangar.Sim.Paths;

/// <summary>Binary min-heap of open A* nodes keyed by (f, h, index) for deterministic tie-breaks (PTH-09).
/// Stale entries are allowed (lazy decrease-key); the search skips them when popped.
/// The key is packed into one ulong (f: 20 bits, h: 16 bits, index: 28 bits), so ordering by the packed value is
/// exactly the lexicographic (f, h, index) order and each sift step is a single compare.</summary>
internal sealed class PathHeap
{
    public const int IndexBits = 28, HBits = 16, FBits = 20;
    public const int MaxIndex = (1 << IndexBits) - 1, MaxH = (1 << HBits) - 1, MaxF = (1 << FBits) - 1;
    private const ulong IndexMask = MaxIndex;

    private ulong[] _items = new ulong[256];

    public int Count { get; private set; }

    public void Clear() => Count = 0;

    public void Push(int f, int h, int index)
    {
        System.Diagnostics.Debug.Assert((uint)f <= MaxF && (uint)h <= MaxH && (uint)index <= MaxIndex);
        if (Count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
        ulong e = ((ulong)(uint)f << (HBits + IndexBits)) | ((ulong)(uint)h << IndexBits) | (uint)index;
        var items = _items;
        int i = Count++;
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            ulong p = items[parent];
            if (e >= p) break;
            items[i] = p;
            i = parent;
        }
        items[i] = e;
    }

    public int PopIndex()
    {
        var items = _items;
        int top = (int)(items[0] & IndexMask);
        int count = --Count;
        ulong last = items[count];
        int i = 0;
        int half = count >> 1;
        while (i < half)
        {
            int child = 2 * i + 1;
            ulong c = items[child];
            if (child + 1 < count && items[child + 1] < c) c = items[++child];
            if (c >= last) break;
            items[i] = c;
            i = child;
        }
        if (count > 0) items[i] = last;
        return top;
    }
}
