using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Meshing;
using Xunit;
using Xunit.Abstractions;

namespace Aurvangar.Sim.Tests.Perf;

[Trait("Category", "Perf")]
[Collection(PerfCollection.Name)]
public class MesherPerfTests
{
    private readonly ITestOutputHelper _out;

    public MesherPerfTests(ITestOutputHelper output) => _out = output;

    /// <summary>MESH-P1: ChunkMesher.Build on the busiest seed-1 surface chunk (most quads with no slice, i.e. the
    /// most terrain surface) median &lt;= 6 ms * PERF_SCALE. Also measured with the slice cutting through that chunk,
    /// which adds cut faces and exposes the interior.</summary>
    [Fact]
    public void Mesher_SurfaceChunk() // MESH-P1
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var w = sim.World;
        var colors = new BlockColors(TestContent.Db);
        int fullSlice = w.SizeY - 1;

        int busiest = -1, busiestQuads = -1;
        for (int ci = 0; ci < w.ChunkCount; ci++)
        {
            var (x, y, z) = w.ChunkCoords(ci);
            int q = ChunkMesher.Build(w, x, y, z, fullSlice, colors).QuadCount;
            if (q > busiestQuads) { busiestQuads = q; busiest = ci; }
        }
        Assert.True(busiestQuads > 0, "seed 1 should have surface chunks");
        var (cx, cy, cz) = w.ChunkCoords(busiest);

        var times = PerfHelpers.Measure(() => ChunkMesher.Build(w, cx, cy, cz, fullSlice, colors), iterations: 50);
        double median = PerfHelpers.Median(times);

        int midSlice = cy * VoxelWorld.ChunkSize + VoxelWorld.ChunkSize / 2;
        var sliced = ChunkMesher.Build(w, cx, cy, cz, midSlice, colors);
        var slicedTimes = PerfHelpers.Measure(() => ChunkMesher.Build(w, cx, cy, cz, midSlice, colors), iterations: 50);
        double slicedMedian = PerfHelpers.Median(slicedTimes);

        _out.WriteLine($"MESH-P1: chunk ({cx},{cy},{cz}) {busiestQuads} quads: median {median:F3} ms, p95 {PerfHelpers.P95(times):F3} ms; " +
                       $"slice y={midSlice} ({sliced.QuadCount} quads, {sliced.CutQuadCount} cut): median {slicedMedian:F3} ms");
        Assert.True(median <= 6.0 * PerfHelpers.Scale, $"mesher median {median:F2} ms");
        Assert.True(slicedMedian <= 6.0 * PerfHelpers.Scale, $"sliced mesher median {slicedMedian:F2} ms");
    }

    /// <summary>MESH-P1 with fine shapes (M11-T11, VIEW-27): the same busiest seed-1 chunk with every column's top cell
    /// given a shape (slab, stair in each rotation, pillar in turn; up to 1,024 shaped cells, far more than any build),
    /// median &lt;= 6 ms * PERF_SCALE.</summary>
    [Fact]
    public void Mesher_ShapedSurfaceChunk() // MESH-P1
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var w = sim.World;
        var colors = new BlockColors(TestContent.Db);
        int fullSlice = w.SizeY - 1;
        int busiest = -1, busiestQuads = -1;
        for (int ci = 0; ci < w.ChunkCount; ci++)
        {
            var (x, y, z) = w.ChunkCoords(ci);
            int q = ChunkMesher.Build(w, x, y, z, fullSlice, colors).QuadCount;
            if (q > busiestQuads) { busiestQuads = q; busiest = ci; }
        }
        var (cx, cy, cz) = w.ChunkCoords(busiest);
        var forms = new[]
        {
            new BlockForm(BlockShape.Slab, 0), new BlockForm(BlockShape.Stair, 0), new BlockForm(BlockShape.Stair, 1),
            new BlockForm(BlockShape.Stair, 2), new BlockForm(BlockShape.Stair, 3), new BlockForm(BlockShape.Pillar, 0),
        };
        int s = VoxelWorld.ChunkSize, shaped = 0;
        for (int z = cz * s; z < cz * s + s; z++)
            for (int x = cx * s; x < cx * s + s; x++)
                for (int y = cy * s + s - 1; y >= cy * s; y--)
                {
                    var c = new Aurvangar.Sim.Core.Int3(x, y, z);
                    if (!w.IsSolid(c)) continue;
                    if (w.SetForm(c, forms[shaped % forms.Length])) shaped++;
                    break;
                }
        Assert.True(shaped > 500, $"only {shaped} shaped cells");
        var mesh = ChunkMesher.Build(w, cx, cy, cz, fullSlice, colors);
        var times = PerfHelpers.Measure(() => ChunkMesher.Build(w, cx, cy, cz, fullSlice, colors), iterations: 50);
        double median = PerfHelpers.Median(times);
        _out.WriteLine($"MESH-P1 shapes: chunk ({cx},{cy},{cz}) {shaped} shaped cells, {mesh.QuadCount} quads: median {median:F3} ms, " +
                       $"p95 {PerfHelpers.P95(times):F3} ms");
        Assert.True(median <= 6.0 * PerfHelpers.Scale, $"shaped mesher median {median:F2} ms");
    }
}
