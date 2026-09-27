namespace Aurvangar.Sim.World;

/// <summary>CON-19 (M11-T10, ADR-080): the fine shape of a built block. Must match the <c>shapes</c> list of
/// data/blocks.json (validated by ContentDb). Never renumber: saved, hashed and logged.</summary>
public enum BlockShape : byte
{
    /// <summary>The whole 1 m cell (every block before M11-T10, and every natural block).</summary>
    Full = 0,
    /// <summary>The lower half of the cell.</summary>
    Slab = 1,
    /// <summary>Two steps; the rotation says which way it climbs (CON-19).</summary>
    Stair = 2,
    /// <summary>A post in the middle of the cell, full height.</summary>
    Pillar = 3,
}

/// <summary>CON-19: a shape and its rotation (0..3, quarter turns; 0 for a shape with one rotation). The default is
/// <see cref="Full"/>. <see cref="Packed"/> is <c>shape * 4 + rotation</c>, 0 for Full: the byte that is saved,
/// hashed and logged.</summary>
public readonly record struct BlockForm(BlockShape Shape, byte Rotation)
{
    public static readonly BlockForm Full = default;

    public bool IsFull => Shape == BlockShape.Full && Rotation == 0;

    public byte Packed => (byte)(((int)Shape << 2) | (Rotation & 3));

    public static BlockForm FromPacked(byte packed) => new((BlockShape)(packed >> 2), (byte)(packed & 3));

    public override string ToString() => Rotation == 0 ? Shape.ToString() : $"{Shape} r{Rotation}";
}
