using System.Numerics;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Frame;
using Aurvangar.ViewCore.Meshing;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M3-T4: engine-neutral parts of the Godot game loop (VIEW-01, VIEW-02).</summary>
public class TickAccumulatorTests
{
    [Fact]
    public void OneX_RunsOneTickPerTenthSecond_AndKeepsRemainder()
    {
        var acc = new TickAccumulator();
        Assert.Equal(0, acc.Advance(0.05, 1));
        Assert.Equal(1, acc.Advance(0.06, 1));   // 0.11 total
        Assert.Equal(2, acc.Advance(0.2, 1));    // 0.01 + 0.2 = 0.21
        Assert.Equal(0.01, acc.Accumulator, 6);
    }

    [Fact]
    public void Speeds_ScaleDelta_AndPauseRunsNothing()
    {
        Assert.Equal(new[] { 0, 1, 3, 6 }, TickAccumulator.Speeds);
        var acc = new TickAccumulator();
        Assert.Equal(0, acc.Advance(10.0, 0));
        Assert.Equal(0.0, acc.Accumulator, 6);
        Assert.Equal(3, acc.Advance(0.1, 3));
    }

    [Fact]
    public void AtMostFourTicksPerFrame_AndBacklogIsDropped()
    {
        var acc = new TickAccumulator();
        Assert.Equal(TickAccumulator.MaxTicksPerFrame, acc.Advance(5.0, 6));
        // No spiral of death: the leftover is clamped to at most one tick.
        Assert.True(acc.Accumulator <= TickAccumulator.TickSeconds);
        Assert.Equal(1, acc.Advance(0.0, 1));
        Assert.Equal(0, acc.Advance(0.0, 1));
    }
}

public class RemeshQueueTests
{
    [Fact]
    public void Dedupes_AndTakesInFifoOrderWithinBudget()
    {
        var q = new RemeshQueue(16);
        q.Enqueue(5); q.Enqueue(2); q.Enqueue(5); q.Enqueue(9); q.Enqueue(1); q.Enqueue(7);
        Assert.Equal(5, q.Count);
        var batch = new List<int>();
        q.TakeBatch(4, batch);
        Assert.Equal(new[] { 5, 2, 9, 1 }, batch);
        Assert.Equal(1, q.Count);
        q.Enqueue(5); // taken chunks can be queued again
        q.TakeBatch(4, batch);
        Assert.Equal(new[] { 7, 5 }, batch);
        Assert.Equal(0, q.Count);
    }
}

public class RemeshRouterTests
{
    // 3 x 2 x 3 chunks: index = cx + cz * 3 + cy * 9.
    private static RemeshRouter NewRouter() => new(3, 2, 3);

    [Fact]
    public void EnqueueAll_QueuesEveryChunkForTerrainAndWater()
    {
        var r = NewRouter();
        r.EnqueueAll();
        Assert.Equal(18, r.Terrain.Count);
        Assert.Equal(18, r.Water.Count);
    }

    [Fact]
    public void ChunkDirty_RemeshesTerrainAndWaterOfThatChunk()
    {
        var r = NewRouter();
        r.Route(new SimEvent[] { new ChunkDirty(4), new ChunkDirty(4), new AgentSpawned(new Sim.Core.AgentId(1)) });
        Assert.Equal(new[] { 4 }, Drain(r.Terrain));
        Assert.Equal(new[] { 4 }, Drain(r.Water));
    }

    [Fact]
    public void WaterDirty_AlsoRemeshesFaceNeighborWater_ClippedToWorld()
    {
        var r = NewRouter();
        // Chunk (1,0,1) = 4: neighbors (0,0,1)=3, (2,0,1)=5, (1,0,0)=1, (1,0,2)=7, (1,1,1)=13; no chunk below.
        r.Route(new SimEvent[] { new WaterDirty(4) });
        Assert.Equal(0, r.Terrain.Count);
        Assert.Equal(new[] { 1, 3, 4, 5, 7, 13 }, Drain(r.Water).OrderBy(i => i).ToArray());

        // Corner chunk (0,1,0) = 9: neighbors (1,1,0)=10, (0,1,1)=12, (0,0,0)=0.
        r.Route(new SimEvent[] { new WaterDirty(9) });
        Assert.Equal(new[] { 0, 9, 10, 12 }, Drain(r.Water).OrderBy(i => i).ToArray());
    }

    [Fact]
    public void Budgets_AreFourPerFrame()
    {
        Assert.Equal(4, RemeshRouter.TerrainBudget);
        Assert.Equal(4, RemeshRouter.WaterBudget);
    }

    private static int[] Drain(RemeshQueue q)
    {
        var all = new List<int>();
        var batch = new List<int>();
        while (q.Count > 0) { q.TakeBatch(100, batch); all.AddRange(batch); }
        return all.ToArray();
    }
}

public class MeshWindingTests
{
    [Fact]
    public void ClockwiseIndices_ReverseEachTriangle()
    {
        var m = new MeshData();
        m.AddQuad(Vector3.Zero, Vector3.UnitX, new Vector3(1, 1, 0), Vector3.UnitY, Vector3.UnitZ, Vector4.One);
        var cw = MeshWinding.ClockwiseIndices(m);
        Assert.Equal(new[] { 0, 2, 1, 0, 3, 2 }, cw);
    }

    [Fact]
    public void ClockwiseTriangles_ExpandToOneVertexPerCorner()
    {
        var m = new MeshData();
        var a = Vector3.Zero; var b = Vector3.UnitX; var c = new Vector3(1, 1, 0); var d = Vector3.UnitY;
        m.AddQuad(a, b, c, d, Vector3.UnitZ, Vector4.One);
        var tris = MeshWinding.ClockwiseTriangles(m);
        Assert.Equal(new[] { a, c, b, a, d, c }, tris);
    }
}

/// <summary>M3-T4 "seed 1 renders with terrain and river", checked headless: the frame loop's queues and budgets
/// drive the ViewCore meshers over the whole world (the Godot layer only copies these meshes).</summary>
public class SeedOneFrameLoopTests
{
    [Fact]
    public void InitialLoad_MeshesTerrainAndRiver_WithinBudgetedFrames()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var w = sim.World;
        int sliceY = w.SizeY - 1;
        var router = new RemeshRouter(w.ChunksX, w.ChunksY, w.ChunksZ);
        router.EnqueueAll();
        var colors = new BlockColors(TestContent.Db);
        var batch = new List<int>();
        int frames = 0, terrainQuads = 0, waterQuads = 0;
        while (router.Terrain.Count > 0 || router.Water.Count > 0)
        {
            frames++;
            router.Terrain.TakeBatch(RemeshRouter.TerrainBudget, batch);
            Assert.True(batch.Count <= RemeshRouter.TerrainBudget);
            foreach (int ci in batch)
            {
                var (cx, cy, cz) = w.ChunkCoords(ci);
                terrainQuads += ChunkMesher.Build(w, cx, cy, cz, sliceY, colors).QuadCount;
            }
            router.Water.TakeBatch(RemeshRouter.WaterBudget, batch);
            foreach (int ci in batch)
            {
                var (cx, cy, cz) = w.ChunkCoords(ci);
                waterQuads += WaterMesher.Build(w, sim.Water, cx, cy, cz, sliceY).QuadCount;
            }
        }
        Assert.Equal((w.ChunkCount + 3) / 4, frames);
        Assert.True(terrainQuads > 0);
        Assert.True(waterQuads > 0, "the river should produce water surface quads");

        // A block change after load reaches the terrain queue through ChunkDirty.
        var cell = new Int3(w.SizeX / 2, 1, w.SizeZ / 2);
        w.SetBlock(cell, BlockId.Air);
        sim.Tick();
        router.Route(sim.Events.Drain());
        Assert.True(router.Terrain.Count >= 1);
    }
}
