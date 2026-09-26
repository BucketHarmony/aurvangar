using Colony.Sim.Core;

namespace Colony.Sim.Paths;

/// <summary>Reachability regions by flood fill (PTH-13). M4-T3.</summary>
public sealed class Regions
{
    public const int None = 0;

    private readonly PathGrid _grid;

    public Regions(PathGrid grid) { _grid = grid; }

    public bool IsDirty { get; private set; } = true;

    public void MarkDirty() => IsDirty = true;

    /// <summary>Region id of a walkable cell, or None.</summary>
    public int RegionOf(Int3 c) => throw new NotImplementedException("M4-T3: regions (PTH-13)");

    public void RebuildIfDirty()
    {
        // M4-T3: flood fill all walkable cells with the pathfinder's neighbor rules. No-op until then.
    }
}
