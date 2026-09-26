using Colony.Sim.Water;
using Colony.Sim.World;

namespace Colony.ViewCore.Meshing;

/// <summary>Water surface mesh for one chunk (water.md rendering contract, VIEW-07). M3-T3.
/// Rules: a surface cell is a wet cell (level &gt; 0) whose above cell is dry or solid. Emit one top quad per surface
/// cell at height y + level/Full (no greedy merge). For each of the 4 horizontal neighbors that is not solid and whose
/// own surface height in that column position is lower (dry counts as y + 0), emit a side quad spanning the height
/// difference. Cells above sliceY are ignored.</summary>
public static class WaterMesher
{
    public static MeshData Build(VoxelWorld world, WaterGrid water, int cx, int cy, int cz, int sliceY) =>
        throw new NotImplementedException("M3-T3: water surface mesher");
}
