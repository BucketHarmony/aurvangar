using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Save;

/// <summary>SAV-01 section markers, written before each section so a corrupt or mismatched file fails at the first
/// section that does not line up. Never renumber; append new sections and bump <see cref="SaveGame.FormatVersion"/>.</summary>
internal enum SaveSection
{
    Header = 1, Blocks, Water, WaterStats, Plants, Buildings, Storage, Piles, Designations, Agents, Jobs, Ids,
    Commands, End,
}

/// <summary>Binary helpers shared by the save sections.</summary>
internal static class SaveIo
{
    private const int SectionBase = 0x53454300;   // "SEC" + section number

    public static void Section(this BinaryWriter w, SaveSection s) => w.Write(SectionBase + (int)s);

    public static void ExpectSection(this BinaryReader r, SaveSection s)
    {
        int v = r.ReadInt32();
        if (v != SectionBase + (int)s)
            throw new InvalidDataException($"Save file is corrupt: expected section {s} (0x{SectionBase + (int)s:x8}), found 0x{v:x8}.");
    }

    public static void Write(this BinaryWriter w, Int3 c) { w.Write(c.X); w.Write(c.Y); w.Write(c.Z); }

    public static Int3 ReadInt3(this BinaryReader r) => new(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());

    public static void WriteCount(this BinaryWriter w, int n) => w.Write7BitEncodedInt(n);

    /// <summary>A non-negative count no larger than <paramref name="max"/>.</summary>
    public static int ReadCount(this BinaryReader r, int max, string what)
    {
        int n = r.Read7BitEncodedInt();
        if (n < 0 || n > max) throw new InvalidDataException($"Save file is corrupt: {what} count {n} is out of range 0..{max}.");
        return n;
    }

    /// <summary>A value in [0, max).</summary>
    public static int ReadIndex(this BinaryReader r, int max, string what)
    {
        int i = r.ReadInt32();
        if (i < 0 || i >= max) throw new InvalidDataException($"Save file is corrupt: {what} {i} is out of range 0..{max - 1}.");
        return i;
    }

    /// <summary>A byte-sized enum value that must be defined.</summary>
    public static T ReadEnum<T>(this BinaryReader r, string what) where T : struct, Enum
    {
        byte b = r.ReadByte();
        var v = (T)Enum.ToObject(typeof(T), b);
        if (!Enum.IsDefined(v)) throw new InvalidDataException($"Save file is corrupt: {what} value {b} is not defined.");
        return v;
    }

    /// <summary>Run-length encoding: (run length, value) pairs covering the whole span.</summary>
    public static void WriteRle(this BinaryWriter w, ReadOnlySpan<byte> data)
    {
        int i = 0;
        while (i < data.Length)
        {
            byte v = data[i];
            int j = i + 1;
            while (j < data.Length && data[j] == v) j++;
            w.Write7BitEncodedInt(j - i);
            w.Write(v);
            i = j;
        }
    }

    public static void ReadRle(this BinaryReader r, Span<byte> into, string what)
    {
        int i = 0;
        while (i < into.Length)
        {
            int n = r.Read7BitEncodedInt();
            byte v = r.ReadByte();
            if (n <= 0 || n > into.Length - i) throw new InvalidDataException($"Save file is corrupt: {what} run of {n} at {i}.");
            into.Slice(i, n).Fill(v);
            i += n;
        }
    }

    public static void WriteRle(this BinaryWriter w, ReadOnlySpan<ushort> data)
    {
        int i = 0;
        while (i < data.Length)
        {
            ushort v = data[i];
            int j = i + 1;
            while (j < data.Length && data[j] == v) j++;
            w.Write7BitEncodedInt(j - i);
            w.Write(v);
            i = j;
        }
    }

    public static void ReadRle(this BinaryReader r, Span<ushort> into, int maxValue, string what)
    {
        int i = 0;
        while (i < into.Length)
        {
            int n = r.Read7BitEncodedInt();
            ushort v = r.ReadUInt16();
            if (n <= 0 || n > into.Length - i) throw new InvalidDataException($"Save file is corrupt: {what} run of {n} at {i}.");
            if (v > maxValue) throw new InvalidDataException($"Save file is corrupt: {what} value {v} above {maxValue}.");
            into.Slice(i, n).Fill(v);
            i += n;
        }
    }
}
