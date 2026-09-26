using Colony.Sim.World;

namespace Colony.ViewCore.Meshing;

/// <summary>Greedy mesher for one 32^3 chunk (VIEW-03) with z-slicing (VIEW-04). M3-T1, M3-T2.
/// Rules: faces between two solid cells are culled (including across chunk borders); coplanar adjacent faces of the
/// same block type and same cut flag merge into one quad; cells with y &gt; sliceY count as Air; top faces of solid
/// cells at y == sliceY whose above cell is solid in the real world are "cut" faces (darkened color, counted in
/// CutQuadCount).</summary>
public static class ChunkMesher
{
    public static MeshData Build(VoxelWorld world, int cx, int cy, int cz, int sliceY, BlockColors colors) =>
        throw new NotImplementedException("M3-T1: greedy chunk mesher (docs/specs/view-ui.md VIEW-03)");
}
