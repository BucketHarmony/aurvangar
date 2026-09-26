using Aurvangar.Sim.Core;
using Aurvangar.Sim.Paths;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>PTH-13 beyond the acceptance pair in <see cref="RegionTests"/>: dirty feeds and agreement with A*.</summary>
public class RegionRuleTests
{
    /// <summary>A 2-high wall along x = 10 with a one-cell gap at z = 5 (left and right connect only through it).</summary>
    private static ScenarioBuilder WallWithGap() => new ScenarioBuilder().Ground(4)
        .FillBox(new Int3(10, 5, 0), new Int3(10, 6, 31), BlockId.Stone)
        .FillBox(new Int3(10, 5, 5), new Int3(10, 6, 5), BlockId.Air);

    private static readonly Int3 Left = new(5, 5, 5), Right = new(15, 5, 5), Gap = new(10, 5, 5);

    [Fact]
    public void DeepWaterInGap_SplitsRegions()
    {
        var sim = WallWithGap().Build();
        sim.Tick();
        Assert.Equal(sim.Regions.RegionOf(Left), sim.Regions.RegionOf(Right));

        sim.Water.AddSource(Gap);
        sim.RunTicks(3);
        Assert.True(sim.Water.IsDeep(Gap));
        Assert.True(sim.PathGrid.IsWalkable(Left) && sim.PathGrid.IsWalkable(Right));
        Assert.Equal(Regions.None, sim.Regions.RegionOf(Gap));
        Assert.NotEqual(sim.Regions.RegionOf(Left), sim.Regions.RegionOf(Right));
    }

    [Fact]
    public void TreeInGap_SplitsRegions()
    {
        var sim = WallWithGap().Build();
        sim.Tick();
        Assert.Equal(sim.Regions.RegionOf(Left), sim.Regions.RegionOf(Right));

        sim.Plants.AddTree(Gap);
        sim.Tick();
        Assert.NotEqual(sim.Regions.RegionOf(Left), sim.Regions.RegionOf(Right));
    }

    [Fact]
    public void NoChange_NoRebuild_ShallowWaterNoRebuild()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        sim.Tick();
        long rebuilds = sim.Counters.RegionRebuilds;
        Assert.Equal(1, rebuilds);
        sim.RunTicks(5);
        Assert.Equal(rebuilds, sim.Counters.RegionRebuilds);

        // Shallow water spreads and evaporates: dry/wet crossings only change the wading cost, not reachability.
        sim.Water.SetLevel(new Int3(16, 5, 16), WaterGrid.Full / 4);
        sim.RunTicks(20);
        Assert.Equal(rebuilds, sim.Counters.RegionRebuilds);
        Assert.False(sim.Regions.IsDirty);

        // A block change anywhere marks regions dirty.
        sim.World.SetBlock(new Int3(3, 5, 3), BlockId.Stone);
        sim.Tick();
        Assert.Equal(rebuilds + 1, sim.Counters.RegionRebuilds);
    }

    [Fact]
    public void OutOfBoundsAndSolid_None()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        sim.Tick();
        Assert.Equal(Regions.None, sim.Regions.RegionOf(new Int3(-1, 5, 5)));
        Assert.Equal(Regions.None, sim.Regions.RegionOf(new Int3(5, 99, 5)));
        Assert.Equal(Regions.None, sim.Regions.RegionOf(new Int3(5, 2, 5)));
        Assert.Equal(Regions.None, sim.Regions.RegionOf(new Int3(5, 7, 5)));   // air, no floor
    }

    /// <summary>Same region if and only if A* finds a path, on rough random terrain with pillars and drops of 2+.</summary>
    [Theory]
    [InlineData(11UL)]
    [InlineData(12UL)]
    [InlineData(13UL)]
    public void SameRegion_IffPathExists(ulong seed)
    {
        var rng = new Rng(seed);
        var b = new ScenarioBuilder(32, 32, 32).Ground(3);
        var heights = new int[32 * 32];
        for (int z = 0; z < 32; z++)
            for (int x = 0; x < 32; x++)
            {
                int h = 3 + rng.NextInt(0, 4);                     // 3..6: steps of 1 and cliffs of 2-3
                heights[x + z * 32] = h;
                if (h > 3) b.FillBox(new Int3(x, 4, z), new Int3(x, h, z), BlockId.Dirt);
                if (rng.Chance(1, 12)) b.FillBox(new Int3(x, h + 2, z), new Int3(x, h + 2, z), BlockId.Stone); // low ceiling
            }
        var sim = b.Build();
        sim.Tick();

        var cells = new List<Int3>();
        for (int z = 0; z < 32; z++)
            for (int x = 0; x < 32; x++)
            {
                var c = new Int3(x, heights[x + z * 32] + 1, z);
                if (sim.PathGrid.IsWalkable(c)) cells.Add(c);
            }
        Assert.True(cells.Count > 500);

        int same = 0, different = 0;
        for (int k = 0; k < 300; k++)
        {
            var a = cells[rng.NextInt(cells.Count)];
            var g = cells[rng.NextInt(cells.Count)];
            int ra = sim.Regions.RegionOf(a), rg = sim.Regions.RegionOf(g);
            Assert.NotEqual(Regions.None, ra);
            var result = sim.Pathfinder.FindPath(a, g);
            Assert.NotEqual(PathStatus.TooFar, result.Status);
            Assert.True(result.Found == (ra == rg), $"{a} r{ra} -> {g} r{rg}: {result.Status}");
            if (ra == rg) same++; else different++;
        }
        Assert.True(same > 0 && different > 0, $"degenerate sample: same {same}, different {different}");
    }
}
