using Aurvangar.Sim.Core;
using Aurvangar.Sim.Plants;

namespace Aurvangar.Sim.World;

/// <summary>Tree (GEN-06) and berry bush (GEN-07) placement by seeded dart throwing.</summary>
internal static class TerrainPlants
{
    public const int TreeTarget = 150, TreeSpacing = 4, TreeRiverClearance = 3;
    public const int BushTarget = 24, BushRiverMin = 4, BushRiverMax = 12, BushHubRange = 30, BushSpacing = 2;
    private const int MaxAttempts = 50000;
    private const ulong TreeSalt = 0x7472656573UL, BushSalt = 0x6275736865UL; // "trees", "bushe"

    /// <summary>GEN-06: Grass surface, outside the spawn flat, more than 3 cells from the channel, spacing ≥ 4
    /// (Chebyshev on X/Z). Stops at 150 trees or when attempts run out.</summary>
    public static void PlaceTrees(TerrainResult r, VoxelWorld world, int[] heights, int[] river, bool[] inFlat, ulong seed)
    {
        int sx = world.SizeX, sz = world.SizeZ;
        var rng = Rng.Derive(seed, TreeSalt);
        var blocked = new bool[sx * sz];
        for (int a = 0; a < MaxAttempts && r.TreeBases.Count < TreeTarget; a++)
        {
            int x = rng.NextInt(sx), z = rng.NextInt(sz);
            int i = x + z * sx;
            if (blocked[i] || inFlat[i]) continue;
            if (TerrainShape.ChannelDistance(river, x, z) <= TreeRiverClearance) continue;
            int h = heights[i];
            if (h + PlantSystem.TreeHeight >= world.SizeY || world.GetBlock(x, h, z) != BlockId.Grass) continue;
            r.TreeBases.Add(new Int3(x, h + 1, z));
            Mark(blocked, sx, sz, x, z, TreeSpacing - 1);
        }
    }

    /// <summary>GEN-07: Grass surface 4..12 cells from the channel and within 30 cells of the spawn center, outside
    /// the flat, not next to a tree or another bush.</summary>
    public static void PlaceBushes(TerrainResult r, VoxelWorld world, int[] heights, int[] river, bool[] inFlat, ulong seed)
    {
        int sx = world.SizeX, sz = world.SizeZ;
        var rng = Rng.Derive(seed, BushSalt);
        var blocked = new bool[sx * sz];
        foreach (var t in r.TreeBases) Mark(blocked, sx, sz, t.X, t.Z, BushSpacing - 1);
        int x0 = Math.Max(0, r.SpawnX - BushHubRange), x1 = Math.Min(sx - 1, r.SpawnX + BushHubRange);
        int z0 = Math.Max(0, r.SpawnZ - BushHubRange), z1 = Math.Min(sz - 1, r.SpawnZ + BushHubRange);
        for (int a = 0; a < MaxAttempts && r.BushBases.Count < BushTarget; a++)
        {
            int x = rng.NextInt(x0, x1 + 1), z = rng.NextInt(z0, z1 + 1);
            int i = x + z * sx;
            if (blocked[i] || inFlat[i]) continue;
            int d = TerrainShape.ChannelDistance(river, x, z);
            if (d < BushRiverMin || d > BushRiverMax) continue;
            int h = heights[i];
            if (h + 1 >= world.SizeY || world.GetBlock(x, h, z) != BlockId.Grass) continue;
            r.BushBases.Add(new Int3(x, h + 1, z));
            Mark(blocked, sx, sz, x, z, BushSpacing - 1);
        }
    }

    private static void Mark(bool[] blocked, int sx, int sz, int cx, int cz, int radius)
    {
        for (int z = Math.Max(0, cz - radius); z <= Math.Min(sz - 1, cz + radius); z++)
            for (int x = Math.Max(0, cx - radius); x <= Math.Min(sx - 1, cx + radius); x++)
                blocked[x + z * sx] = true;
    }
}
