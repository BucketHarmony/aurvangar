using System.Numerics;

namespace Aurvangar.ViewCore.Meshing;

/// <summary>Meshes one cell's <see cref="ShapePattern"/> from its 0.25 m sub-cells (M11-T11, VIEW-27). A sub-cell face
/// is drawn where the sub-cell beside it is empty: inside the cell from the pattern itself, across the cell's side from
/// the neighbour cell's pattern (<see cref="ShapePattern.Full"/> for a full solid neighbour, 0 for air). Coplanar
/// faces of one layer merge greedily in the 4x4 grid, so a slab or a pillar is 6 quads.</summary>
public static class ShapeMesher
{
    private const int N = ShapePattern.N;

    /// <summary>Neighbour slot of direction (<paramref name="d"/>: 0 x, 1 y, 2 z; <paramref name="sign"/> ±1):
    /// +X, -X, +Y, -Y, +Z, -Z.</summary>
    public static int Slot(int d, int sign) => d * 2 + (sign > 0 ? 0 : 1);

    /// <summary>Emits the visible faces of <paramref name="pattern"/> for a cell whose low corner is
    /// <paramref name="lo"/> and whose edge is <paramref name="size"/> (1 for the terrain; a little more for an inflated
    /// ghost). <paramref name="neighbours"/> holds the 6 neighbour patterns in <see cref="Slot"/> order. With
    /// <paramref name="cutTop"/> (VIEW-04), the faces on the cell's top plane take <paramref name="cutColor"/> and count
    /// as cut.</summary>
    public static void Emit(MeshData mesh, Vector3 lo, float size, ulong pattern, ReadOnlySpan<ulong> neighbours,
        Vector4 color, Vector4 cutColor = default, bool cutTop = false)
    {
        if (pattern == 0) return;
        float step = size / N;
        Span<int> c = stackalloc int[3];
        Span<int> nc = stackalloc int[3];
        Span<bool> mask = stackalloc bool[N * N];
        Span<float> p = stackalloc float[3];
        for (int d = 0; d < 3; d++)
        {
            int u = (d + 1) % 3, v = (d + 2) % 3;    // (u, v, d) right-handed, as in ChunkMesher
            var normalBase = Axis(d);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                ulong across = neighbours[Slot(d, sign)];
                for (int k = 0; k < N; k++)
                {
                    bool any = false;
                    c[d] = k;
                    for (int j = 0; j < N; j++)
                    {
                        c[v] = j;
                        for (int i = 0; i < N; i++)
                        {
                            c[u] = i;
                            bool face = false;
                            if (ShapePattern.Has(pattern, c[0], c[1], c[2]))
                            {
                                nc[0] = c[0]; nc[1] = c[1]; nc[2] = c[2];
                                int nk = k + sign;
                                if (nk >= 0 && nk < N) { nc[d] = nk; face = !ShapePattern.Has(pattern, nc[0], nc[1], nc[2]); }
                                else { nc[d] = nk < 0 ? N - 1 : 0; face = !ShapePattern.Has(across, nc[0], nc[1], nc[2]); }
                            }
                            mask[i + j * N] = face;
                            any |= face;
                        }
                    }
                    if (!any) continue;
                    bool cut = cutTop && d == 1 && sign > 0 && k == N - 1;
                    var col = cut ? cutColor : color;
                    var normal = normalBase * sign;
                    for (int j = 0; j < N; j++)
                        for (int i = 0; i < N;)
                        {
                            if (!mask[i + j * N]) { i++; continue; }
                            int w = 1;
                            while (i + w < N && mask[i + w + j * N]) w++;
                            int h = 1;
                            while (j + h < N && RowSet(mask, i, j + h, w)) h++;
                            for (int jj = j; jj < j + h; jj++)
                                for (int ii = i; ii < i + w; ii++) mask[ii + jj * N] = false;
                            p[d] = Get(lo, d) + (k + (sign > 0 ? 1 : 0)) * step;
                            p[u] = Get(lo, u) + i * step;
                            p[v] = Get(lo, v) + j * step;
                            var a = new Vector3(p[0], p[1], p[2]);
                            var eu = Axis(u) * (w * step);
                            var ev = Axis(v) * (h * step);
                            // Counter-clockwise seen from the normal side (MeshData contract).
                            if (sign > 0) mesh.AddQuad(a, a + eu, a + eu + ev, a + ev, normal, col, cut);
                            else mesh.AddQuad(a, a + ev, a + eu + ev, a + eu, normal, col, cut);
                            i += w;
                        }
                }
            }
        }
    }

    /// <summary>A free-standing shape (every neighbour empty): the tool and plan ghosts.</summary>
    public static void EmitAlone(MeshData mesh, Vector3 lo, float size, ulong pattern, Vector4 color)
    {
        Span<ulong> none = stackalloc ulong[6];
        Emit(mesh, lo, size, pattern, none, color);
    }

    private static bool RowSet(Span<bool> mask, int i, int j, int w)
    {
        for (int x = i; x < i + w; x++)
            if (!mask[x + j * N]) return false;
        return true;
    }

    private static float Get(Vector3 v, int a) => a switch { 0 => v.X, 1 => v.Y, _ => v.Z };

    private static Vector3 Axis(int a) => a switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, _ => Vector3.UnitZ };
}
