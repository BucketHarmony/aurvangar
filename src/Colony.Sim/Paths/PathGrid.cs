using Colony.Sim.Core;
using Colony.Sim.Plants;
using Colony.Sim.Water;
using Colony.Sim.World;

namespace Colony.Sim.Paths;

/// <summary>Walkability cache. Spec: docs/specs/pathfinding.md PTH-01..03. SCAFFOLD: direct (uncached) evaluation.
/// M4-T1: add the lazy per-cell flag cache fed by VoxelWorld.ChangedCells and water changes.</summary>
public sealed class PathGrid
{
    private readonly VoxelWorld _world;
    private readonly WaterGrid _water;
    private readonly PlantSystem _plants;

    public PathGrid(VoxelWorld world, WaterGrid water, PlantSystem plants)
    {
        _world = world; _water = water; _plants = plants;
    }

    public VoxelWorld World => _world;

    /// <summary>PTH-01.</summary>
    public bool IsStandable(Int3 c) =>
        _world.InBounds(c)
        && !_world.IsSolid(c)
        && !_plants.IsOccupied(c)
        && !_world.IsSolid(c + Int3.Up)
        && _world.IsSolid(c + Int3.Down);

    /// <summary>PTH-02 (construction-site blocking is added in M5-T2).</summary>
    public bool IsWalkable(Int3 c) => IsStandable(c) && !_water.IsDeep(c);

    /// <summary>Called by Simulation after world/water changes. M4-T1.</summary>
    public void Invalidate(IReadOnlyList<int> changedCells) { }
}
