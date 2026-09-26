using Colony.Sim.Core;

namespace Colony.Sim.World;

/// <summary>Output of terrain generation beyond the block array.</summary>
public sealed class TerrainResult
{
    public Int3 HubOrigin { get; set; }
    public List<Int3> TreeBases { get; } = new();
    public List<Int3> BushBases { get; } = new();
    public List<Int3> WaterSources { get; } = new();
    public List<Int3> WaterDrains { get; } = new();
    /// <summary>Cells to fill with water before pre-settling (GEN-08).</summary>
    public List<Int3> InitialWater { get; } = new();
    /// <summary>Column surface heights (x + z*SizeX), for tests and spawn logic.</summary>
    public int[] Heights { get; set; } = Array.Empty<int>();
}

/// <summary>Deterministic terrain (docs/specs/world.md GEN-01..08). Pure function of (seed, world size). M1-T3.</summary>
public static class TerrainGenerator
{
    public static TerrainResult Generate(VoxelWorld world, ulong seed) =>
        throw new NotImplementedException("M1-T3: terrain generation (GEN-01..07)");
}
