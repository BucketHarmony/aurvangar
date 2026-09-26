using Aurvangar.Sim.Events;

namespace Aurvangar.ViewCore.Frame;

/// <summary>Routes drained sim events to the terrain and water remesh queues (VIEW-02). Chunk indices use the
/// VoxelWorld layout (cx + cz * ChunksX + cy * ChunksX * ChunksZ).
/// <list type="bullet">
/// <item><c>ChunkDirty</c> → terrain and water of that chunk (a block change can add or remove water side faces;
/// the world already dirties neighbor chunks for border cells, WLD-03).</item>
/// <item><c>WaterDirty</c> → water of that chunk and of its 6 face neighbors, because a water mesh reads the
/// one-cell border (ADR-017) and the event does not say which cell changed (ADR-018).</item>
/// </list></summary>
public sealed class RemeshRouter
{
    /// <summary>VIEW-02: chunk remeshes per frame.</summary>
    public const int TerrainBudget = 4;
    /// <summary>VIEW-02: water remeshes per frame.</summary>
    public const int WaterBudget = 4;

    private readonly int _cx, _cy, _cz;

    public RemeshRouter(int chunksX, int chunksY, int chunksZ)
    {
        _cx = chunksX; _cy = chunksY; _cz = chunksZ;
        int count = chunksX * chunksY * chunksZ;
        Terrain = new RemeshQueue(count);
        Water = new RemeshQueue(count);
    }

    public RemeshQueue Terrain { get; }
    public RemeshQueue Water { get; }

    public int ChunkCount => _cx * _cy * _cz;

    /// <summary>Queues every chunk for both meshes (initial build, slice reset).</summary>
    public void EnqueueAll()
    {
        for (int i = 0; i < ChunkCount; i++) { Terrain.Enqueue(i); Water.Enqueue(i); }
    }

    public void Route(IReadOnlyList<SimEvent> events)
    {
        foreach (var e in events)
        {
            switch (e)
            {
                case ChunkDirty cd:
                    Terrain.Enqueue(cd.ChunkIndex);
                    Water.Enqueue(cd.ChunkIndex);
                    break;
                case WaterDirty wd:
                    EnqueueWaterWithNeighbors(wd.ChunkIndex);
                    break;
            }
        }
    }

    private void EnqueueWaterWithNeighbors(int ci)
    {
        int layer = _cx * _cz;
        int y = ci / layer, rem = ci - y * layer, z = rem / _cx, x = rem - z * _cx;
        Water.Enqueue(ci);
        if (x > 0) Water.Enqueue(ci - 1);
        if (x < _cx - 1) Water.Enqueue(ci + 1);
        if (z > 0) Water.Enqueue(ci - _cx);
        if (z < _cz - 1) Water.Enqueue(ci + _cx);
        if (y > 0) Water.Enqueue(ci - layer);
        if (y < _cy - 1) Water.Enqueue(ci + layer);
    }
}
