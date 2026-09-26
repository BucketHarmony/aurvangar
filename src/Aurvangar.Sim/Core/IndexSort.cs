using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Aurvangar.Sim.Core;

/// <summary>Ascending sort for lists of non-negative cell indices. LSD radix sort (11-bit digits), O(n) per pass;
/// used on hot paths where <c>List.Sort</c> dominated the water step (WAT-P1). Small lists fall back to
/// <c>List.Sort</c>. The result is identical to any other ascending sort, so determinism is unaffected.</summary>
public sealed class IndexSort
{
    private const int Bits = 11;
    private const int Radix = 1 << Bits;
    private const int SmallList = 256;

    private readonly int[] _count = new int[Radix];
    private int[] _scratch = Array.Empty<int>();

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Sort(List<int> list)
    {
        int n = list.Count;
        if (n < SmallList) { list.Sort(); return; }
        if (_scratch.Length < n) _scratch = new int[Math.Max(n, _scratch.Length * 2)];

        var src = CollectionsMarshal.AsSpan(list);
        var dst = _scratch.AsSpan(0, n);
        int max = 0;
        foreach (var v in src) if (v > max) max = v;

        bool inScratch = false;
        for (int shift = 0; (max >> shift) > 0; shift += Bits)
        {
            Array.Clear(_count);
            foreach (var v in src) _count[(v >> shift) & (Radix - 1)]++;
            int sum = 0;
            for (int b = 0; b < Radix; b++) { int c = _count[b]; _count[b] = sum; sum += c; }
            foreach (var v in src) dst[_count[(v >> shift) & (Radix - 1)]++] = v;
            var t = src; src = dst; dst = t;
            inScratch = !inScratch;
        }
        if (inScratch) src.CopyTo(CollectionsMarshal.AsSpan(list));
    }
}
