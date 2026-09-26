using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.World;

/// <summary>Output of terrain generation beyond the block array.</summary>
public sealed class TerrainResult
{
    /// <summary>Footprint origin that centers a building of the given footprint on the spawn flat, standing on it
    /// (GEN-05). The footprint comes from data (hub in buildings.json), never hard-coded.</summary>
    public Int3 HubOrigin(int[] footprint) =>
        new(SpawnX - footprint[0] / 2, SpawnSurfaceY + 1, SpawnZ - footprint[2] / 2);
    public List<Int3> TreeBases { get; } = new();
    public List<Int3> BushBases { get; } = new();
    public List<Int3> WaterSources { get; } = new();
    public List<Int3> WaterDrains { get; } = new();
    /// <summary>Cells to fill with water before pre-settling (GEN-08).</summary>
    public List<Int3> InitialWater { get; } = new();
    /// <summary>Column surface heights (x + z*SizeX), for tests and spawn logic.</summary>
    public int[] Heights { get; set; } = Array.Empty<int>();
    /// <summary>Spawn flat center column (GEN-05).</summary>
    public int SpawnX { get; set; }
    public int SpawnZ { get; set; }
    /// <summary>Surface (top solid) y of the spawn flat.</summary>
    public int SpawnSurfaceY { get; set; }
}

/// <summary>Deterministic terrain (docs/specs/world.md GEN-01..08). Pure function of (seed, world size).</summary>
public static class TerrainGenerator
{
    public const int SpawnCenterX = 40, SpawnCenterZ = 60, SpawnHalf = 4; // 9x9 (GEN-05)
    public const int SpawnRiverClearance = 6, SpawnShiftStep = 4;

    public static TerrainResult Generate(VoxelWorld world, ulong seed)
    {
        int sx = world.SizeX, sz = world.SizeZ;
        var heights = TerrainShape.BaseHeights(sx, sz, seed);   // GEN-01
        TerrainShape.AddHill(heights, sx, sz);                   // GEN-02
        var river = TerrainShape.RiverCenters(sx);               // GEN-03
        TerrainShape.CarveRiver(heights, river, sx, sz);

        var r = new TerrainResult { Heights = heights };
        var inFlat = FlattenSpawn(r, heights, river, sx, sz);    // GEN-05
        WriteColumns(world, heights, river);                     // GEN-04

        r.SpawnSurfaceY = heights[r.SpawnX + r.SpawnZ * sx];

        TerrainPlants.PlaceTrees(r, world, heights, river, inFlat, seed);   // GEN-06
        TerrainPlants.PlaceBushes(r, world, heights, river, inFlat, seed);  // GEN-07
        AddWater(r, world, heights, river);                                 // GEN-08 (registered in M2-T5)
        return r;
    }

    /// <summary>GEN-05: 9x9 area flattened to its median height; shifted north in 4-cell steps while the river channel
    /// is within 6 cells of it.</summary>
    private static bool[] FlattenSpawn(TerrainResult r, int[] heights, int[] river, int sx, int sz)
    {
        int cx = Math.Clamp(SpawnCenterX, SpawnHalf, sx - 1 - SpawnHalf);
        int cz = Math.Clamp(SpawnCenterZ, SpawnHalf, sz - 1 - SpawnHalf);
        while (cz - SpawnShiftStep >= SpawnHalf && RiverNear(river, cx, cz, sx)) cz -= SpawnShiftStep;
        r.SpawnX = cx; r.SpawnZ = cz;

        var samples = new List<int>();
        for (int z = cz - SpawnHalf; z <= cz + SpawnHalf; z++)
            for (int x = cx - SpawnHalf; x <= cx + SpawnHalf; x++)
                samples.Add(heights[x + z * sx]);
        samples.Sort();
        int median = samples[samples.Count / 2];

        var inFlat = new bool[sx * sz];
        for (int z = cz - SpawnHalf; z <= cz + SpawnHalf; z++)
            for (int x = cx - SpawnHalf; x <= cx + SpawnHalf; x++)
            {
                heights[x + z * sx] = median;
                inFlat[x + z * sx] = true;
            }
        return inFlat;
    }

    private static bool RiverNear(int[] river, int cx, int cz, int sx)
    {
        int reach = SpawnHalf + SpawnRiverClearance;
        for (int x = Math.Max(0, cx - reach); x <= Math.Min(sx - 1, cx + reach); x++)
        {
            int lo = river[x] - TerrainShape.RiverHalfWidth, hi = river[x] + TerrainShape.RiverHalfWidth;
            if (lo <= cz + reach && hi >= cz - reach) return true;
        }
        return false;
    }

    /// <summary>GEN-04: Bedrock at y=0, Stone below h-4, Dirt h-4..h-1, Grass (or Sand near the river) at h.</summary>
    private static void WriteColumns(VoxelWorld world, int[] heights, int[] river)
    {
        int sx = world.SizeX, sz = world.SizeZ;
        for (int z = 0; z < sz; z++)
            for (int x = 0; x < sx; x++)
            {
                int h = heights[x + z * sx];
                var top = TerrainShape.ChannelDistance(river, x, z) <= 2 ? BlockId.Sand : BlockId.Grass;
                world.SetBlockRaw(world.Index(x, 0, z), BlockId.Bedrock);
                for (int y = 1; y <= h && y < world.SizeY; y++)
                {
                    var b = y < h - 4 ? BlockId.Stone : y < h ? BlockId.Dirt : top;
                    world.SetBlockRaw(world.Index(x, y, z), b);
                }
            }
    }

    /// <summary>GEN-08 + ADR-021: sources at x=0 and every <see cref="TerrainShape.RiverSpringSpacing"/> cells along the
    /// channel (never in the drain column), drains at x=max over the channel; initial water fills every air cell up to
    /// bed+3 (only the river valley lies that low).</summary>
    private static void AddWater(TerrainResult r, VoxelWorld world, int[] heights, int[] river)
    {
        int sx = world.SizeX, sz = world.SizeZ;
        int top = Math.Min(TerrainShape.RiverFillTop, world.SizeY - 1);
        for (int x = 0; x < sx - 1; x += TerrainShape.RiverSpringSpacing)
            for (int z = 0; z < sz; z++)
                if (TerrainShape.ChannelDistance(river, x, z) == 0)
                    for (int y = TerrainShape.RiverBed; y <= top; y++) r.WaterSources.Add(new Int3(x, y, z));
        for (int z = 0; z < sz; z++)
        {
            if (TerrainShape.ChannelDistance(river, sx - 1, z) == 0)
                for (int y = TerrainShape.RiverBed; y < world.SizeY; y++) r.WaterDrains.Add(new Int3(sx - 1, y, z));
        }
        for (int y = 1; y <= top; y++)
            for (int z = 0; z < sz; z++)
                for (int x = 0; x < sx; x++)
                    if (y > heights[x + z * sx]) r.InitialWater.Add(new Int3(x, y, z));
    }
}
