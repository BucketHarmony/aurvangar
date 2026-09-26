using System.Numerics;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;

namespace Aurvangar.ViewCore.Meshing;

/// <summary>Water surface mesh for one chunk (water.md rendering contract, VIEW-07). M3-T3.
/// Rules: a surface cell is a wet cell (level &gt; 0) whose above cell is dry, solid, out of bounds or above the
/// slice. Emit one top quad per surface cell at height y + level/Full (no greedy merge). The water in a wet cell tops
/// out at y + level/Full if it is a surface cell, else at y + 1. For each of the 4 horizontal neighbors that is not
/// solid and whose own water top in that cell is lower (dry counts as y + 0), emit a side quad spanning the height
/// difference; this applies to every wet cell, not only surface cells, so falls and cliffs have no gaps (ADR-017).
/// Cells above sliceY are ignored (VIEW-04). Neighbors in other chunks are read from the world. Positions are in
/// world coordinates; quads are counter-clockwise seen from the normal side (MeshData contract). Color is
/// depth-tinted by the water column depth below the cell (WaterColors.ForDepth).</summary>
public static class WaterMesher
{
    private const int S = VoxelWorld.ChunkSize;
    private const float InvFull = 1f / WaterGrid.Full;

    private static readonly (int Dx, int Dz)[] Sides = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    public static MeshData Build(VoxelWorld world, WaterGrid water, int cx, int cy, int cz, int sliceY,
        WaterColors? colors = null)
    {
        colors ??= WaterColors.Default;
        var mesh = new MeshData();
        var levels = water.Levels;
        int ox = cx * S, oy = cy * S, oz = cz * S;
        int yEnd = Math.Min(Math.Min(oy + S, world.SizeY), sliceY + 1);
        int xEnd = Math.Min(ox + S, world.SizeX), zEnd = Math.Min(oz + S, world.SizeZ);

        for (int y = oy; y < yEnd; y++)
        {
            for (int z = oz; z < zEnd; z++)
            {
                for (int x = ox; x < xEnd; x++)
                {
                    int wi = world.Index(x, y, z);
                    int level = levels[wi];
                    if (level == 0 || world.IsSolidAt(wi)) continue;

                    bool surface = !WetAbove(world, levels, x, y, z, sliceY);
                    float top = surface ? y + level * InvFull : y + 1;
                    var color = colors.ForDepth(ColumnDepth(world, levels, x, y, z, level));
                    if (surface)
                    {
                        // Surface cell: top quad (u = +Z, v = +X, u x v = +Y).
                        AddFace(mesh, new Vector3(x, top, z), Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, color);
                    }

                    foreach (var (dx, dz) in Sides)
                    {
                        int nx = x + dx, nz = z + dz;
                        if (world.IsSolid(nx, y, nz)) continue;
                        float nTop = y;
                        if (world.InBounds(nx, y, nz))
                        {
                            int nl = levels[world.Index(nx, y, nz)];
                            if (nl > 0) nTop = WaterTop(world, levels, nx, y, nz, nl, sliceY);
                        }
                        if (nTop >= top) continue;
                        AddSide(mesh, x, z, dx, dz, nTop, top - nTop, color);
                    }
                }
            }
        }
        return mesh;
    }

    /// <summary>Height of the water top in a wet cell: y + level/Full for a surface cell, y + 1 when the cell above is
    /// wet and visible.</summary>
    private static float WaterTop(VoxelWorld world, ReadOnlySpan<ushort> levels, int x, int y, int z, int level,
        int sliceY) =>
        WetAbove(world, levels, x, y, z, sliceY) ? y + 1 : y + level * InvFull;

    private static bool WetAbove(VoxelWorld world, ReadOnlySpan<ushort> levels, int x, int y, int z, int sliceY) =>
        y + 1 <= sliceY && y + 1 < world.SizeY && levels[world.Index(x, y + 1, z)] > 0;

    /// <summary>Water depth in level units: this cell's level plus Full per contiguous wet cell below, capped.</summary>
    private static int ColumnDepth(VoxelWorld world, ReadOnlySpan<ushort> levels, int x, int y, int z, int level)
    {
        int depth = level;
        for (int k = 1; k <= WaterColors.DeepCells && y - k >= 0; k++)
        {
            if (levels[world.Index(x, y - k, z)] == 0) break;
            depth += WaterGrid.Full;
        }
        return depth;
    }

    /// <summary>Vertical side quad on the face of cell (x, z) toward (dx, dz), from y0 up by height h.</summary>
    private static void AddSide(MeshData mesh, int x, int z, int dx, int dz, float y0, float h, Vector4 color)
    {
        var up = Vector3.UnitY * h;
        if (dx > 0) AddFace(mesh, new Vector3(x + 1, y0, z), up, Vector3.UnitZ, Vector3.UnitX, color);
        else if (dx < 0) AddFace(mesh, new Vector3(x, y0, z), Vector3.UnitZ, up, -Vector3.UnitX, color);
        else if (dz > 0) AddFace(mesh, new Vector3(x, y0, z + 1), Vector3.UnitX, up, Vector3.UnitZ, color);
        else AddFace(mesh, new Vector3(x, y0, z), up, Vector3.UnitX, -Vector3.UnitZ, color);
    }

    /// <summary>Quad a, a+u, a+u+v, a+v; with u x v along the normal this is counter-clockwise from the normal side.</summary>
    private static void AddFace(MeshData mesh, Vector3 a, Vector3 u, Vector3 v, Vector3 normal, Vector4 color) =>
        mesh.AddQuad(a, a + u, a + u + v, a + v, normal, color);
}
