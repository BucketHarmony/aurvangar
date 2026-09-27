using System.Numerics;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.ViewCore.Meshing;

/// <summary>Greedy mesher for one 32^3 chunk (VIEW-03) with z-slicing (VIEW-04). M3-T1, M3-T2.
/// Rules: faces between two solid cells are culled (including across chunk borders); coplanar adjacent faces of the
/// same block type and same cut flag merge into one quad; cells with y &gt; sliceY count as Air; top faces of solid
/// cells at y == sliceY whose above cell is solid in the real world are "cut" faces (darkened color, counted in
/// CutQuadCount). Positions are in world coordinates (1 unit = 1 cell, cell (x,y,z) spans [x, x+1]). Built cells with a
/// fine shape (CON-19) are drawn from 0.25 m sub-cells (VIEW-27, M11-T11).</summary>
public static class ChunkMesher
{
    private const int S = VoxelWorld.ChunkSize;
    private const int P = S + 2;                 // padded edge: one neighbor cell on each side
    private static readonly int[] PadStride = { 1, P * P, P };   // x, y, z strides in the padded grid
    private const int CutBit = 1 << 8;           // mask flag above the block id byte: slice cut face (VIEW-04)

    public static MeshData Build(VoxelWorld world, int cx, int cy, int cz, int sliceY, BlockColors colors)
    {
        int ox = cx * S, oy = cy * S, oz = cz * S;
        var pad = FillPadded(world, ox, oy, oz, sliceY);
        var shaped = ClearShaped(world, pad, ox, oy, oz, sliceY);
        var mesh = new MeshData();
        var mask = new int[S * S];
        Span<int> c = stackalloc int[3];
        Span<int> origin = stackalloc int[] { ox, oy, oz };

        for (int d = 0; d < 3; d++)
        {
            int u = (d + 1) % 3, v = (d + 2) % 3;    // (u, v, d) is right-handed: u x v points along +d
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int nOff = sign * PadStride[d];
                for (int k = 0; k < S; k++)
                {
                    // VIEW-03: a face is visible where a solid cell borders a non-solid cell (neighbor chunks included).
                    bool any = false;
                    bool cutLayer = d == 1 && sign > 0 && oy + k == sliceY;   // VIEW-04: top faces on the slice
                    c[d] = k;
                    for (int j = 0; j < S; j++)
                    {
                        c[v] = j;
                        for (int i = 0; i < S; i++)
                        {
                            c[u] = i;
                            int pi = (c[0] + 1) + (c[2] + 1) * P + (c[1] + 1) * P * P;
                            byte b = pad[pi];
                            int m = b != 0 && pad[pi + nOff] == 0 ? b : 0;
                            // The cut flag is part of the mask key, so cut and uncut faces never merge (ADR-016).
                            if (m != 0 && cutLayer && world.IsSolid(ox + c[0], sliceY + 1, oz + c[2])) m |= CutBit;
                            mask[i + j * S] = m;
                            any |= m != 0;
                        }
                    }
                    if (any) EmitGreedy(mesh, mask, d, u, v, sign, k, origin, colors);
                }
            }
        }
        if (shaped != null) EmitShaped(mesh, world, shaped, sliceY, colors);
        return mesh;
    }

    /// <summary>VIEW-27 (M11-T11): cells that are not Full (CON-19) count as not solid in the padded grid, so the full
    /// cells around them draw the faces the shape no longer covers. Returns this chunk's shaped cells at or below the
    /// slice (null when there are none); they are meshed by <see cref="EmitShaped"/>.</summary>
    private static List<(Int3 Cell, BlockForm Form)>? ClearShaped(VoxelWorld world, byte[] pad, int ox, int oy, int oz, int sliceY)
    {
        if (world.FormCount == 0) return null;
        List<(Int3, BlockForm)>? inside = null;
        foreach (var (index, form) in world.Forms)
        {
            var c = world.CellOf(index);
            int lx = c.X - ox, ly = c.Y - oy, lz = c.Z - oz;
            if (c.Y > sliceY || lx < -1 || lx > S || ly < -1 || ly > S || lz < -1 || lz > S) continue;
            pad[(lx + 1) + (lz + 1) * P + (ly + 1) * P * P] = 0;
            if (lx >= 0 && lx < S && ly >= 0 && ly < S && lz >= 0 && lz < S) (inside ??= new()).Add((c, form));
        }
        return inside;
    }

    private static readonly Int3[] SlotSteps = { Int3.East, Int3.West, Int3.Up, Int3.Down, Int3.South, Int3.North };

    /// <summary>VIEW-27: each shaped cell from its 0.25 m sub-cells (<see cref="ShapeMesher"/>); a neighbour above the
    /// slice is air, a full solid one covers the whole side. The top of a shaped cell on the slice with a solid cell
    /// above is cut (VIEW-04).</summary>
    private static void EmitShaped(MeshData mesh, VoxelWorld world, List<(Int3 Cell, BlockForm Form)> cells, int sliceY, BlockColors colors)
    {
        Span<ulong> around = stackalloc ulong[6];
        foreach (var (c, form) in cells)
        {
            for (int s = 0; s < 6; s++)
            {
                var n = c + SlotSteps[s];
                around[s] = n.Y > sliceY || !world.IsSolid(n.X, n.Y, n.Z) ? ShapePattern.Empty : ShapePattern.Of(world.FormAt(n));
            }
            var id = world.GetBlock(c);
            bool cut = c.Y == sliceY && world.IsSolid(c.X, c.Y + 1, c.Z);
            ShapeMesher.Emit(mesh, new Vector3(c.X, c.Y, c.Z), 1f, ShapePattern.Of(form), around,
                colors.Get(id), colors.GetCut(id), cut);
        }
    }

    /// <summary>Copy the chunk plus a one-cell border into a padded grid of visible block ids (0 = not solid or above
    /// the slice). Out-of-bounds cells follow WLD-04 (Bedrock below the world, Air elsewhere).</summary>
    private static byte[] FillPadded(VoxelWorld world, int ox, int oy, int oz, int sliceY)
    {
        var pad = new byte[P * P * P];
        var blocks = world.Blocks;
        int p = 0;
        for (int ly = -1; ly <= S; ly++)
        {
            int wy = oy + ly;
            for (int lz = -1; lz <= S; lz++)
            {
                int wz = oz + lz;
                for (int lx = -1; lx <= S; lx++, p++)
                {
                    int wx = ox + lx;
                    if (wy > sliceY) continue;                // VIEW-04: above the slice counts as Air
                    if (world.InBounds(wx, wy, wz))
                    {
                        int wi = world.Index(wx, wy, wz);
                        if (world.IsSolidAt(wi)) pad[p] = blocks[wi];
                    }
                    else if (world.IsSolid(wx, wy, wz))
                    {
                        pad[p] = (byte)world.GetBlock(wx, wy, wz);
                    }
                }
            }
        }
        return pad;
    }

    private static void EmitGreedy(MeshData mesh, int[] mask, int d, int u, int v, int sign, int k,
        ReadOnlySpan<int> origin, BlockColors colors)
    {
        var normal = Axis(d) * sign;
        var du = Axis(u);
        var dv = Axis(v);
        Span<float> p = stackalloc float[3];
        for (int j = 0; j < S; j++)
        {
            for (int i = 0; i < S;)
            {
                int m = mask[i + j * S];
                if (m == 0) { i++; continue; }

                int w = 1;
                while (i + w < S && mask[i + w + j * S] == m) w++;
                int h = 1;
                while (j + h < S && RowMatches(mask, i, j + h, w, m)) h++;
                for (int jj = j; jj < j + h; jj++) Array.Clear(mask, i + jj * S, w);

                p[d] = origin[d] + k + (sign > 0 ? 1 : 0);
                p[u] = origin[u] + i;
                p[v] = origin[v] + j;
                var a = new Vector3(p[0], p[1], p[2]);
                var eu = du * w;
                var ev = dv * h;
                bool cut = (m & CutBit) != 0;
                var id = (BlockId)(m & 0xFF);
                var color = cut ? colors.GetCut(id) : colors.Get(id);
                // Counter-clockwise seen from the normal side (MeshData contract).
                if (sign > 0) mesh.AddQuad(a, a + eu, a + eu + ev, a + ev, normal, color, cut);
                else mesh.AddQuad(a, a + ev, a + eu + ev, a + eu, normal, color, cut);
                i += w;
            }
        }
    }

    private static bool RowMatches(int[] mask, int i, int j, int w, int m)
    {
        int row = j * S;
        for (int x = i; x < i + w; x++)
            if (mask[x + row] != m) return false;
        return true;
    }

    private static Vector3 Axis(int a) => a switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, _ => Vector3.UnitZ };
}
