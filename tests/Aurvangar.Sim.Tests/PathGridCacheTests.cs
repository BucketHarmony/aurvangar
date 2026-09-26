using Aurvangar.Sim.Core;
using Aurvangar.Sim.Paths;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>PTH-03: the lazy flag cache. Every test checks the cache against the PTH-01/02 rules evaluated
/// directly from world, water and plants.</summary>
public class PathGridCacheTests
{
    [Fact]
    public void Cache_ComputesEachCellOnceUntilInvalidated() // PTH-03
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var c = new Int3(3, 5, 3);
        sim.Tick();                                    // first region build (PTH-13) computes every candidate cell, c too
        long before = sim.PathGrid.FlagComputations;
        Assert.True(sim.PathGrid.IsWalkable(c));
        Assert.True(sim.PathGrid.IsStandable(c));
        Assert.False(sim.PathGrid.IsWet(c));
        Assert.Equal(before, sim.PathGrid.FlagComputations);

        sim.Tick();                                    // nothing changed: no recompute, no region rebuild
        Assert.True(sim.PathGrid.IsWalkable(c));
        Assert.Equal(before, sim.PathGrid.FlagComputations);

        sim.World.SetBlock(new Int3(4, 6, 4), BlockId.Stone);  // inside c's 3x3x3 neighborhood
        Assert.True(sim.PathGrid.IsWalkable(c));
        Assert.Equal(before + 1, sim.PathGrid.FlagComputations);
        Assert.True(sim.PathGrid.IsWalkable(c));
        Assert.Equal(before + 1, sim.PathGrid.FlagComputations);
    }

    [Fact]
    public void CachedCell_UpdatesAfterDig() // PTH-01, PTH-03
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var top = new Int3(3, 5, 3);
        var floor = new Int3(3, 4, 3);
        Assert.True(sim.PathGrid.IsWalkable(top));
        Assert.False(sim.PathGrid.IsStandable(floor));

        sim.World.SetBlock(floor, BlockId.Air);        // dig the floor out from under a cached cell
        Assert.False(sim.PathGrid.IsStandable(top));   // queried before any tick
        Assert.True(sim.PathGrid.IsWalkable(floor));

        sim.Tick();                                    // the change log is consumed and cleared
        Assert.False(sim.PathGrid.IsStandable(top));
        Assert.True(sim.PathGrid.IsWalkable(floor));

        sim.World.SetBlock(new Int3(3, 3, 3), BlockId.Air);   // second dig after the log was cleared
        Assert.False(sim.PathGrid.IsStandable(floor));
        Assert.True(sim.PathGrid.IsStandable(new Int3(3, 3, 3)));
    }

    [Fact]
    public void CachedCell_UpdatesAfterWaterRises() // PTH-02, PTH-03, WAT-14
    {
        // Ground at y=4; a 2-high stone ring around the 3x3 pool x,z = 2..4; a source in one pool corner.
        var b = new ScenarioBuilder().Ground(4)
            .FillBox(new Int3(1, 5, 1), new Int3(5, 6, 5), BlockId.Stone)
            .FillBox(new Int3(2, 5, 2), new Int3(4, 6, 4), BlockId.Air)
            .Source(new Int3(2, 5, 2));
        var sim = b.Build();
        var c = new Int3(4, 5, 4);
        Assert.True(sim.PathGrid.IsWalkable(c));
        Assert.False(sim.PathGrid.IsWet(c));

        bool sawWetWalkable = false, sawDeep = false;
        for (int t = 0; t < 400 && !sawDeep; t++)
        {
            sim.Tick();
            int level = sim.Water.GetLevel(c);
            Assert.Equal(level > 0, sim.PathGrid.IsWet(c));
            Assert.Equal(level < WaterGrid.Full / 2, sim.PathGrid.IsWalkable(c));
            Assert.True(sim.PathGrid.IsStandable(c));
            if (level > 0 && level < WaterGrid.Full / 2) sawWetWalkable = true;
            if (level >= WaterGrid.Full / 2) sawDeep = true;
        }
        Assert.True(sawWetWalkable, "the pool never passed through shallow water");
        Assert.True(sawDeep, "the pool never got deep");
    }

    [Fact]
    public void CachedCell_UpdatesAfterPlantAddAndRemove() // PTH-01, WLD-07, PTH-03
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var c = new Int3(6, 5, 6);
        Assert.True(sim.PathGrid.IsStandable(c));
        var tree = sim.Plants.AddTree(c);
        Assert.False(sim.PathGrid.IsStandable(c));
        sim.Plants.Remove(tree.Id);
        Assert.True(sim.PathGrid.IsStandable(c));
    }

    [Fact]
    public void Cache_MatchesDirectEvaluation_UnderChurn() // PTH-01..03
    {
        var sim = new ScenarioBuilder(32, 32, 32).Ground(4)
            .FillBox(new Int3(10, 5, 10), new Int3(20, 8, 20), BlockId.Dirt)
            .Source(new Int3(5, 6, 5))
            .Build();
        var rng = new Rng(12345);
        var w = sim.World;
        var plants = new List<PlantId>();
        AssertCacheMatches(sim);
        for (int t = 0; t < 120; t++)
        {
            for (int k = 0; k < 6; k++)
            {
                var cell = new Int3(rng.NextInt(0, 32), rng.NextInt(3, 12), rng.NextInt(0, 32));
                switch (rng.NextInt(5))
                {
                    case 0: w.SetBlock(cell, BlockId.Air); break;
                    case 1: w.SetBlock(cell, BlockId.Stone); break;
                    case 2: sim.Water.SetLevel(cell, rng.NextInt(0, WaterGrid.Full + 1)); break;
                    case 3: if (!w.IsSolid(cell)) plants.Add(sim.Plants.AddBush(cell).Id); break;
                    case 4:
                        if (plants.Count > 0)
                        {
                            int j = rng.NextInt(plants.Count);
                            sim.Plants.Remove(plants[j]);
                            plants.RemoveAt(j);
                        }
                        break;
                }
                if (k % 2 == 0) AssertCacheMatches(sim);   // mid-tick queries see changes at once
            }
            sim.Tick();
            AssertCacheMatches(sim);
        }
    }

    [Fact]
    public void InvalidateAll_PicksUpRawWrites() // PTH-03 (loaders and generators write raw)
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var c = new Int3(3, 5, 3);
        Assert.True(sim.PathGrid.IsStandable(c));
        sim.World.SetBlockRaw(sim.World.Index(c), BlockId.Stone);   // bypasses the change log
        sim.PathGrid.InvalidateAll();
        Assert.False(sim.PathGrid.IsStandable(c));
        Assert.True(sim.PathGrid.IsStandable(c + Int3.Up));
    }

    private static void AssertCacheMatches(Simulation sim)
    {
        var w = sim.World;
        for (int y = 0; y < w.SizeY; y++)
            for (int z = 0; z < w.SizeZ; z++)
                for (int x = 0; x < w.SizeX; x++)
                {
                    var c = new Int3(x, y, z);
                    bool standable = !w.IsSolid(c) && !sim.Plants.IsOccupied(c)
                        && !w.IsSolid(c + Int3.Up) && w.IsSolid(c + Int3.Down);
                    int level = sim.Water.GetLevel(c);
                    bool walkable = standable && level < WaterGrid.Full / 2;
                    if (sim.PathGrid.IsStandable(c) != standable || sim.PathGrid.IsWalkable(c) != walkable
                        || sim.PathGrid.IsWet(c) != level > 0)
                        Assert.Fail($"cache mismatch at {c}: standable {standable}, walkable {walkable}, level {level}");
                }
    }
}
