namespace Aurvangar.Sim.Core;

/// <summary>FNV-1a 64-bit accumulator for Simulation.StateHash (ARCH-06).</summary>
public struct StateHasher
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public ulong Value { get; private set; }

    public static StateHasher Create() => new() { Value = Offset };

    public void Add(byte b) { Value = (Value ^ b) * Prime; }

    public void Add(int v) { unchecked { Add((byte)v); Add((byte)(v >> 8)); Add((byte)(v >> 16)); Add((byte)(v >> 24)); } }

    public void Add(long v) { Add((int)v); Add((int)(v >> 32)); }

    public void Add(ulong v) => Add((long)v);

    public void Add(bool v) => Add((byte)(v ? 1 : 0));

    public void Add(Int3 c) { Add(c.X); Add(c.Y); Add(c.Z); }

    public void Add(ReadOnlySpan<byte> bytes) { foreach (var b in bytes) Add(b); }

    public void Add(ReadOnlySpan<ushort> values) { foreach (var v in values) { Add((byte)v); Add((byte)(v >> 8)); } }
}
