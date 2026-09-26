using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.World;

/// <summary>Column height shaping for terrain generation: value noise (GEN-01), hill (GEN-02), river channel (GEN-03).
/// Integer-only; all randomness comes from <see cref="Rng"/> derived from the world seed.</summary>
internal static class TerrainShape
{
    public const int BaseMin = 18, BaseMax = 26;
    public const int HillX = 90, HillZ = 40, HillRadius = 26, HillPeak = 16, MaxHeight = 48;
    public const int RiverCenterZ = 80, RiverAmplitude = 8, RiverHalfWidth = 3, RiverDepth = 4;

    /// <summary>Lowest water cell of the channel (ADR-009): <c>BaseMin - RiverDepth</c>. The solid floor top is one below.</summary>
    public const int RiverBed = BaseMin - RiverDepth;

    /// <summary>Top of the initial river water (GEN-08: fill to bed+3).</summary>
    public const int RiverFillTop = RiverBed + 3;

    private const ulong NoiseSalt = 0x6E6F697365UL; // "noise"

    /// <summary>GEN-01: 2-octave integer value noise mapped to [BaseMin, BaseMax]. Index x + z*sizeX.</summary>
    public static int[] BaseHeights(int sizeX, int sizeZ, ulong seed)
    {
        ulong noiseSeed = Rng.Derive(seed, NoiseSalt).NextU64();
        var h = new int[sizeX * sizeZ];
        for (int z = 0; z < sizeZ; z++)
            for (int x = 0; x < sizeX; x++)
            {
                int n = (2 * Octave(noiseSeed, 1, x, z, 32) + Octave(noiseSeed, 2, x, z, 16)) / 3; // [0, 1023]
                h[x + z * sizeX] = BaseMin + n * (BaseMax - BaseMin + 1) / 1024;
            }
        return h;
    }

    /// <summary>GEN-02: radial bump, smooth falloff (1 - d²/r²)², clamped to MaxHeight.</summary>
    public static void AddHill(int[] heights, int sizeX, int sizeZ)
    {
        const int r2 = HillRadius * HillRadius;
        for (int z = 0; z < sizeZ; z++)
            for (int x = 0; x < sizeX; x++)
            {
                int dx = x - HillX, dz = z - HillZ;
                int d2 = dx * dx + dz * dz;
                if (d2 >= r2) continue;
                int f = r2 - d2;
                int bump = (int)((long)HillPeak * f * f / ((long)r2 * r2));
                int i = x + z * sizeX;
                heights[i] = Math.Min(heights[i] + bump, MaxHeight);
            }
    }

    /// <summary>GEN-03: channel center z per x, <c>80 + 8*sin(x/20)</c> from the Fixed sine table.</summary>
    public static int[] RiverCenters(int sizeX)
    {
        var t = new int[sizeX];
        for (int x = 0; x < sizeX; x++)
        {
            // x/20 radians in 1/1024 turns = x * 1024 / (40*pi); 40*pi ~= 125.6637, scaled by 10^4 and rounded.
            int angle = (int)(((long)x * Fixed.Turn * 10000 + 628318) / 1256637);
            t[x] = RiverCenterZ + Fixed.FloorDiv(RiverAmplitude * Fixed.Sin(angle) + Fixed.One / 2, Fixed.One);
        }
        return t;
    }

    /// <summary>Distance in cells from the channel edge along Z (0 = inside the channel).</summary>
    public static int ChannelDistance(int[] riverCenters, int x, int z) =>
        Math.Max(0, Math.Abs(z - riverCenters[x]) - RiverHalfWidth);

    /// <summary>GEN-03 + ADR-009: carve the channel (floor top at RiverBed-1) and slope the banks 1 block per cell
    /// until they meet the natural terrain.</summary>
    public static void CarveRiver(int[] heights, int[] riverCenters, int sizeX, int sizeZ)
    {
        for (int z = 0; z < sizeZ; z++)
            for (int x = 0; x < sizeX; x++)
            {
                int d = ChannelDistance(riverCenters, x, z);
                int i = x + z * sizeX;
                heights[i] = Math.Min(heights[i], RiverBed - 1 + d);
            }
    }

    private static int Octave(ulong seed, ulong octave, int x, int z, int period)
    {
        int ix = x / period, iz = z / period;
        int tx = Smooth((x - ix * period) * 1024 / period);
        int tz = Smooth((z - iz * period) * 1024 / period);
        int v00 = Lattice(seed, octave, ix, iz), v10 = Lattice(seed, octave, ix + 1, iz);
        int v01 = Lattice(seed, octave, ix, iz + 1), v11 = Lattice(seed, octave, ix + 1, iz + 1);
        int a = v00 + (v10 - v00) * tx / 1024;
        int b = v01 + (v11 - v01) * tx / 1024;
        return a + (b - a) * tz / 1024;
    }

    /// <summary>Smoothstep on [0, 1024]: t²(3 - 2t).</summary>
    private static int Smooth(int t) => t * t / 1024 * (3 * 1024 - 2 * t) / 1024;

    private static int Lattice(ulong seed, ulong octave, int ix, int iz) =>
        (int)(Rng.Hash64(seed
            + octave * 0xD6E8FEB86659FD93UL
            + (ulong)(uint)ix * 0x9E3779B97F4A7C15UL
            + (ulong)(uint)iz * 0xC2B2AE3D27D4EB4FUL) % 1024);
}
