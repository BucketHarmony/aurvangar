using System.Runtime.InteropServices;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Water;

/// <summary>ECO-15: one moist flag per (x,z) column, for the column's top surface cell (its highest solid cell).
/// A column is moist when a water cell with level >= <see cref="MinLevel"/> lies within Chebyshev radius
/// <see cref="Radius"/> at <c>y in [surfaceY - 2, surfaceY + 1]</c>. Recomputed in ticks that are a multiple of
/// <see cref="Interval"/> (ARCH-01 step 4). The flags reflect the water at the last recompute, so they are saved and
/// hashed (ADR-046).
/// Algorithm: one pass over all cells builds, per column, a bitmask of the heights holding enough water and the
/// surface height; the masks are OR-dilated along x then z (separable Chebyshev box); a column is moist when its
/// dilated mask has a bit inside its own height window.</summary>
public sealed class MoistureMap
{
    public const int Interval = 50;
    public const int Radius = 5;
    public const int MinLevel = 128;
    public const int Below = 2;
    public const int Above = 1;

    private readonly VoxelWorld _world;
    private readonly WaterGrid _water;
    private readonly int _columns;
    private readonly int _words;          // ulongs per column mask (SizeY / 64 rounded up)
    private readonly byte[] _moist;       // 0 or 1 per column, index x + z * SizeX
    private readonly int[] _surface;      // highest solid y per column, -1 when none (scratch, rebuilt each recompute)
    private readonly ulong[] _wet;        // per-column height masks, column-major blocks of _words
    private readonly ulong[] _tmp;

    public MoistureMap(VoxelWorld world, WaterGrid water)
    {
        _world = world;
        _water = water;
        _columns = world.SizeX * world.SizeZ;
        _words = (world.SizeY + 63) >> 6;
        _moist = new byte[_columns];
        _surface = new int[_columns];
        _wet = new ulong[_columns * _words];
        _tmp = new ulong[_columns * _words];
    }

    /// <summary>Per-column flags (1 = moist), index <c>x + z * SizeX</c>.</summary>
    public ReadOnlySpan<byte> Flags => _moist;

    internal Span<byte> FlagsMutable => _moist;

    public bool IsMoist(int x, int z) =>
        (uint)x < (uint)_world.SizeX && (uint)z < (uint)_world.SizeZ && _moist[x + z * _world.SizeX] != 0;

    /// <summary>The column's highest solid cell y, or -1 when it has none. Reads the world now (not the last recompute).</summary>
    public int SurfaceY(int x, int z)
    {
        for (int y = _world.SizeY - 1; y >= 0; y--)
            if (_world.IsSolid(x, y, z)) return y;
        return -1;
    }

    /// <summary>ARCH-01 step 4.</summary>
    public void Tick(long tick)
    {
        if (tick % Interval == 0) Recompute();
    }

    public void Recompute()
    {
        BuildColumnMasks();
        DilateX();
        DilateZ();
        Classify();
    }

    /// <summary>Top-down pass over the layers. Surface search stops once every column has one; the water scan skips
    /// groups of four empty cells (most of the world is dry).</summary>
    private void BuildColumnMasks()
    {
        Array.Clear(_wet);
        Array.Fill(_surface, -1);
        var levels = _water.Levels;
        var blocks = _world.Blocks;
        int unresolved = _columns;
        for (int y = _world.SizeY - 1; y >= 0; y--)
        {
            int layer = y * _columns;
            if (unresolved > 0)
            {
                var bl = blocks.Slice(layer, _columns);
                for (int i = 0; i < _columns; i++)
                {
                    if (_surface[i] < 0 && _world.IsSolidBlock(bl[i])) { _surface[i] = y; unresolved--; }
                }
            }

            int word = y >> 6;
            ulong bit = 1UL << (y & 63);
            var lv = levels.Slice(layer, _columns);
            var groups = MemoryMarshal.Cast<ushort, ulong>(lv);
            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g] == 0) continue;
                for (int i = g * 4, end = i + 4; i < end; i++)
                    if (lv[i] >= MinLevel) _wet[i * _words + word] |= bit;
            }
            for (int i = groups.Length * 4; i < _columns; i++)
                if (lv[i] >= MinLevel) _wet[i * _words + word] |= bit;
        }
    }

    /// <summary>_tmp[x,z] = OR of _wet over x-Radius..x+Radius (clipped).</summary>
    private void DilateX()
    {
        int sx = _world.SizeX, sz = _world.SizeZ, w = _words;
        for (int z = 0; z < sz; z++)
        {
            int row = z * sx;
            for (int x = 0; x < sx; x++)
            {
                int lo = Math.Max(0, x - Radius), hi = Math.Min(sx - 1, x + Radius);
                int dst = (row + x) * w;
                for (int k = 0; k < w; k++)
                {
                    ulong m = 0;
                    for (int xx = lo; xx <= hi; xx++) m |= _wet[(row + xx) * w + k];
                    _tmp[dst + k] = m;
                }
            }
        }
    }

    /// <summary>_wet[x,z] = OR of _tmp over z-Radius..z+Radius (clipped).</summary>
    private void DilateZ()
    {
        int sx = _world.SizeX, sz = _world.SizeZ, w = _words;
        for (int z = 0; z < sz; z++)
        {
            int lo = Math.Max(0, z - Radius), hi = Math.Min(sz - 1, z + Radius);
            for (int x = 0; x < sx; x++)
            {
                int dst = (z * sx + x) * w;
                for (int k = 0; k < w; k++)
                {
                    ulong m = 0;
                    for (int zz = lo; zz <= hi; zz++) m |= _tmp[(zz * sx + x) * w + k];
                    _wet[dst + k] = m;
                }
            }
        }
    }

    private void Classify()
    {
        int maxY = _world.SizeY - 1;
        for (int i = 0; i < _columns; i++)
        {
            int s = _surface[i];
            byte moist = 0;
            if (s >= 0)
            {
                int lo = Math.Max(0, s - Below), hi = Math.Min(maxY, s + Above);
                for (int y = lo; y <= hi; y++)
                {
                    if ((_wet[i * _words + (y >> 6)] & (1UL << (y & 63))) != 0) { moist = 1; break; }
                }
            }
            _moist[i] = moist;
        }
    }

    public void AddToHash(ref StateHasher h) => h.Add(_moist);
}
