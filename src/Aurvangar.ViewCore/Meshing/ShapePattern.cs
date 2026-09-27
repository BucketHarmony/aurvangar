using Aurvangar.Sim.World;

namespace Aurvangar.ViewCore.Meshing;

/// <summary>CON-19 geometry at 0.25 m detail (M11-T11, VIEW-27): each form is a 4x4x4 pattern of sub-cells, one bit per
/// sub-cell in a <see cref="ulong"/>, bit <c>sx + sz*4 + sy*16</c> (sx, sy, sz in 0..3 from the cell's low corner).
/// <list type="bullet">
/// <item>Full: all 64.</item>
/// <item>Slab: the lower half (sy 0..1).</item>
/// <item>Stair: the lower half plus the upper half on the high side: rotation 0 climbs toward +Z (sz 2..3), 1 toward +X,
/// 2 toward -Z, 3 toward -X.</item>
/// <item>Pillar: the 0.5 x 0.5 post in the middle (sx, sz 1..2), full height.</item>
/// </list>
/// A packed form the data does not define draws as Full.</summary>
public static class ShapePattern
{
    public const int N = 4;
    public const ulong Full = ulong.MaxValue;
    public const ulong Empty = 0;

    private static readonly ulong[] Table = BuildTable();

    public static int Bit(int sx, int sy, int sz) => sx + sz * N + sy * N * N;

    public static bool Has(ulong pattern, int sx, int sy, int sz) => (pattern >> Bit(sx, sy, sz) & 1) != 0;

    /// <summary>The pattern of <paramref name="form"/>.</summary>
    public static ulong Of(BlockForm form) => Table[form.Packed & 15];

    /// <summary>Sub-cells set in <paramref name="pattern"/>.</summary>
    public static int Count(ulong pattern) => System.Numerics.BitOperations.PopCount(pattern);

    private static ulong[] BuildTable()
    {
        var t = new ulong[16];
        for (int i = 0; i < 16; i++) t[i] = Full;
        t[new BlockForm(BlockShape.Slab, 0).Packed] = Make((x, y, z) => y < 2);
        for (byte r = 0; r < 4; r++)
        {
            byte rot = r;
            t[new BlockForm(BlockShape.Stair, rot).Packed] = Make((x, y, z) => y < 2 || HighSide(rot, x, z));
        }
        t[new BlockForm(BlockShape.Pillar, 0).Packed] = Make((x, y, z) => x is 1 or 2 && z is 1 or 2);
        return t;
    }

    /// <summary>CON-19: the stair's high half for each rotation.</summary>
    private static bool HighSide(int rot, int x, int z) => rot switch
    {
        0 => z >= 2,
        1 => x >= 2,
        2 => z < 2,
        _ => x < 2,
    };

    private static ulong Make(Func<int, int, int, bool> inside)
    {
        ulong p = 0;
        for (int y = 0; y < N; y++)
            for (int z = 0; z < N; z++)
                for (int x = 0; x < N; x++)
                    if (inside(x, y, z)) p |= 1UL << Bit(x, y, z);
        return p;
    }
}
