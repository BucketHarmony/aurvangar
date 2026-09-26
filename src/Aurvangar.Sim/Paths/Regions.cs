using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Paths;

/// <summary>Reachability regions by flood fill (PTH-13, ADR-025).
/// Every walkable cell gets a region id; two walkable cells share an id exactly when A* can connect them (the move
/// rules in <see cref="PathMoves.From"/> are symmetric between walkable cells). Rebuilt fully at the end of a tick
/// when <see cref="PathGrid.WalkabilityVersion"/> moved or <see cref="MarkDirty"/> was called. Ids are assigned
/// 1, 2, ... in ascending flat index of each region's lowest cell, so they are deterministic. Derived data: not
/// hashed, not saved (a load marks it dirty through <see cref="PathGrid.InvalidateAll"/>).</summary>
public sealed class Regions
{
    public const int None = 0;

    private readonly PathGrid _grid;
    private readonly VoxelWorld _world;
    private readonly int[] _region;
    private int[] _queue = new int[1024];
    private bool _forced = true;
    private long _builtVersion = -1;

    public Regions(PathGrid grid)
    {
        _grid = grid;
        _world = grid.World;
        _region = new int[_world.CellCount];
    }

    /// <summary>True when a rebuild is pending for the end of the tick.</summary>
    public bool IsDirty => _forced || _builtVersion != _grid.WalkabilityVersion;

    /// <summary>Number of regions after the last rebuild.</summary>
    public int Count { get; private set; }

    public void MarkDirty() => _forced = true;

    /// <summary>Region id of a walkable cell as of the last rebuild, or <see cref="None"/>.</summary>
    public int RegionOf(Int3 c) => _world.InBounds(c) ? _region[_world.Index(c)] : None;

    /// <summary>Rebuild if dirty. Returns true if it rebuilt. Called by Simulation at the end of the tick (ARCH-01 step 11).</summary>
    public bool RebuildIfDirty()
    {
        _grid.SyncWorldChanges();
        if (!IsDirty) return false;
        Rebuild();
        return true;
    }

    private void Rebuild()
    {
        _forced = false;
        Array.Clear(_region);
        int sizeX = _world.SizeX, layer = _world.SizeX * _world.SizeZ;
        int next = 0;
        Span<PathStep> steps = stackalloc PathStep[PathMoves.MaxMoves];
        // y = 0 has no floor below, so nothing there is standable.
        for (int i = layer; i < _region.Length; i++)
        {
            // Cheap PTH-01 prefilter (not solid, solid below) before touching the flag cache.
            if (_region[i] != None || _world.IsSolidAt(i) || !_world.IsSolidAt(i - layer)) continue;
            int y = i / layer, rem = i - y * layer, z = rem / sizeX, x = rem - z * sizeX;
            if ((_grid.FlagsAt(x, y, z) & PathGrid.WalkableFlag) == 0) continue;
            Fill(i, ++next, steps, sizeX, layer);
        }
        Count = next;
        _builtVersion = _grid.WalkabilityVersion;
    }

    /// <summary>Breadth-first flood fill from a walkable seed with the A* move rules (<see cref="PathMoves.Steps"/>;
    /// <see cref="RebuildIfDirty"/> synced the change log first).</summary>
    private void Fill(int seedIndex, int id, Span<PathStep> steps, int sizeX, int layer)
    {
        int head = 0, tail = 0;
        _region[seedIndex] = id;
        Push(ref tail, seedIndex);
        while (head < tail)
        {
            int ai = _queue[head++];
            int ay = ai / layer, rem = ai - ay * layer, az = rem / sizeX, ax = rem - az * sizeX;
            int n = PathMoves.Steps(_grid, ax, ay, az, steps);
            for (int k = 0; k < n; k++)
            {
                int bi = steps[k].X + steps[k].Z * sizeX + steps[k].Y * layer;
                if (_region[bi] != None) continue;
                _region[bi] = id;
                Push(ref tail, bi);
            }
        }
    }

    private void Push(ref int tail, int index)
    {
        if (tail == _queue.Length) Array.Resize(ref _queue, _queue.Length * 2);
        _queue[tail++] = index;
    }
}
