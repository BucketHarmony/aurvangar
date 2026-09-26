using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

public class VoxelWorldTests
{
    private static VoxelWorld NewWorld(int x = 64, int y = 32, int z = 64) => new(x, y, z, TestContent.Db.SolidTable);

    [Fact]
    public void Index_RoundTrips()
    {
        var w = NewWorld();
        foreach (var c in new[] { new Int3(0, 0, 0), new Int3(63, 31, 63), new Int3(5, 7, 40) })
            Assert.Equal(c, w.CellOf(w.Index(c)));
    }

    [Fact]
    public void Index_YMajorLayout() // WLD-02
    {
        var w = NewWorld();
        Assert.Equal(1, w.Index(1, 0, 0));
        Assert.Equal(64, w.Index(0, 0, 1));
        Assert.Equal(64 * 64, w.Index(0, 1, 0));
    }

    [Fact]
    public void OutOfBounds_ReadRules() // WLD-04
    {
        var w = NewWorld();
        Assert.Equal(BlockId.Bedrock, w.GetBlock(new Int3(3, -1, 3)));
        Assert.Equal(BlockId.Air, w.GetBlock(new Int3(-1, 3, 3)));
        Assert.Equal(BlockId.Air, w.GetBlock(new Int3(3, 99, 3)));
        Assert.True(w.IsSolid(new Int3(3, -1, 3)));
    }

    [Fact]
    public void OutOfBounds_WriteRejected()
    {
        var w = NewWorld();
        Assert.False(w.SetBlock(new Int3(-1, 0, 0), BlockId.Stone));
        Assert.Empty(w.ChangedCells);
    }

    [Fact]
    public void SetBlock_RecordsChangeAndDirtiesChunk()
    {
        var w = NewWorld();
        Assert.True(w.SetBlock(new Int3(10, 10, 10), BlockId.Stone));
        Assert.False(w.SetBlock(new Int3(10, 10, 10), BlockId.Stone)); // unchanged
        Assert.Single(w.ChangedCells);
        Assert.Equal(new[] { w.ChunkIndexOf(new Int3(10, 10, 10)) }, w.TakeDirtyChunks());
        Assert.Empty(w.TakeDirtyChunks());
    }

    [Fact]
    public void SetBlock_OnChunkBorder_DirtiesNeighborChunk() // WLD-03
    {
        var w = NewWorld();
        w.SetBlock(new Int3(31, 5, 5), BlockId.Stone);
        var dirty = w.TakeDirtyChunks();
        Assert.Equal(2, dirty.Count);
        Assert.Contains(w.ChunkIndexOf(new Int3(31, 5, 5)), dirty);
        Assert.Contains(w.ChunkIndexOf(new Int3(32, 5, 5)), dirty);
    }

    [Fact]
    public void Constructor_RejectsNonChunkMultiple()
    {
        Assert.Throws<ArgumentException>(() => new VoxelWorld(40, 32, 32, TestContent.Db.SolidTable));
    }

    [Fact]
    public void ChunkCoords_RoundTrip()
    {
        var w = NewWorld();
        for (int ci = 0; ci < w.ChunkCount; ci++)
        {
            var (cx, cy, cz) = w.ChunkCoords(ci);
            Assert.Equal(ci, w.ChunkIndex(cx, cy, cz));
        }
    }
}
