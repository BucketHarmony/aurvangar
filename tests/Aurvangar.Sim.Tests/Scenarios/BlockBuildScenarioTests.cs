using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M8-T2: build-block designations and jobs (construction.md CON-05, CON-07, CON-11..16, ADR-061/062). Worlds
/// are stone to y = 8, so dwarves walk on y = 9; the hub is at (20,9,20) with its entrance at (21,9,19).</summary>
[Trait("Category", "Scenario")]
public partial class BlockBuildScenarioTests
{
    private const int G = 9;
    private static readonly Int3 HubOrigin = new(20, G, 20);

    internal static List<Job> Builds(Simulation sim) => sim.Jobs.All.Where(j => j.Kind == JobKind.Build).ToList();

    private static int Placed(Simulation sim, IEnumerable<Int3> cells) =>
        cells.Count(c => sim.Content.IsConstruction(sim.World.GetBlock(c)));

    private static void Designate(Simulation sim, BuildShape shape, Int3 a, Int3 b, int height = 1,
        BlockId block = BlockId.Masonry, bool plan = false)
    {
        sim.Enqueue(new DesignateBuild(shape, a, b, height, block, plan));
        sim.Tick();
    }

    /// <summary>A 6-long, 3-high wall at z = 12 (x 10..15, y 9..11). A 2-wide stone ledge along z 13..14 (walk level
    /// y = 10) gives the builders a way to the top course; the ledge runs the width of the map.</summary>
    private static Simulation WallWorld(int stone, out List<Int3> wall)
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(new Int3(0, G, 13), new Int3(31, G, 14), BlockId.Stone)
            .Hub(HubOrigin).Stock("stone", stone)
            .Agent(new Int3(12, G, 17)).Agent(new Int3(13, G, 17)).Build();
        wall = BuildShapes.Cells(BuildShape.Wall, new Int3(10, G, 12), new Int3(15, G, 12), 3);
        Designate(sim, BuildShape.Wall, new Int3(10, G, 12), new Int3(15, G, 12), 3);
        Assert.Equal(18, sim.Plans.Count);
        return sim;
    }

    private static int StoneEverywhere(Simulation sim) =>
        Stored(Hub(sim), "stone") + CarriedTotal(sim, "stone") + PileTotal(sim, "stone");

    [Fact]
    public void Wall_BuiltBottomUpFromStorage()
    {
        var sim = WallWorld(30, out var wall);
        var placedAt = new Dictionary<Int3, long>();
        int biggestBatch = 0;
        RunUntil(sim, () => sim.Plans.Count == 0, 6000, () =>
        {
            foreach (var c in wall)
                if (!placedAt.ContainsKey(c) && sim.World.GetBlock(c) == BlockId.Masonry) placedAt[c] = sim.Clock.Tick;
            foreach (var j in Builds(sim))
                biggestBatch = Math.Max(biggestBatch, j.Steps.Count(s => s.Kind == StepKind.Place));
            Assert.Equal(30, StoneEverywhere(sim) + placedAt.Count);   // no stone made or lost
        });
        Assert.All(wall, c => Assert.Equal(BlockId.Masonry, sim.World.GetBlock(c)));
        foreach (var c in wall)
            if (c.Y > G) Assert.True(placedAt[c + Int3.Down] < placedAt[c], $"{c} was placed before the block under it");
        Assert.True(biggestBatch > 1, "no Build job ever batched more than one block");

        RunUntil(sim, () => CarriedTotal(sim, "stone") == 0 && PileTotal(sim, "stone") == 0, 2000);
        Assert.Equal(30 - wall.Count, Stored(Hub(sim), "stone"));
        Assert.Empty(Builds(sim));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    [Fact]
    public void OnlyReadyEntriesPostJobs_StatusesExplainTheRest()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(new Int3(4, G, 5), new Int3(4, G + 1, 5), BlockId.Stone)      // a 2-high post
            .FillBox(new Int3(26, G, 5), new Int3(27, G + 2, 5), BlockId.Stone)    // a 3-high pillar nobody can climb
            .Hub(HubOrigin).Stock("stone", 30)
            .Agent(new Int3(14, G, 14)).Agent(new Int3(12, G, 16)).Build();

        sim.Tick();                                // regions are built at the end of the first tick
        var waitSupport = new Int3(6, G + 1, 5);   // beside the entry (5,10,5), which the post supports
        var column = new Int3(10, G, 10);
        var belowFirst = column + Int3.Up;
        var noMaterial = new Int3(8, G, 16);       // Planks: no log anywhere
        var occupied = new Int3(14, G, 14);        // a dwarf stands here
        var noAccess = new Int3(26, G + 3, 5);     // on the pillar; its stand cell (27,12,5) is cut off
        sim.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(5, G + 1, 5), new Int3(6, G + 1, 5), 1, BlockId.Masonry, false));
        sim.Enqueue(new DesignateBuild(BuildShape.Wall, column, column, 2, BlockId.Masonry, false));
        sim.Enqueue(new DesignateBuild(BuildShape.Single, noMaterial, noMaterial, 1, BlockId.Planks, false));
        sim.Enqueue(new DesignateBuild(BuildShape.Single, occupied, occupied, 1, BlockId.Masonry, false));
        sim.Enqueue(new DesignateBuild(BuildShape.Single, noAccess, noAccess, 1, BlockId.Masonry, false));
        sim.Tick();
        Assert.Equal(7, sim.Plans.Count);

        Assert.Equal(BuildStatus.WaitSupport, sim.Plans.StatusOf(sim, waitSupport));   // M10-T1 (ADR-071): was NoSupport
        Assert.Equal(BuildStatus.BelowFirst, sim.Plans.StatusOf(sim, belowFirst));
        Assert.Equal(BuildStatus.NoMaterial, sim.Plans.StatusOf(sim, noMaterial));
        Assert.Equal(BuildStatus.Occupied, sim.Plans.StatusOf(sim, occupied));
        Assert.Equal(BuildStatus.NoAccess, sim.Plans.StatusOf(sim, noAccess));
        Assert.Equal(BuildStatus.InJob, sim.Plans.StatusOf(sim, column));   // the Ready ones got jobs
        // M10-T4 (ADR-074): the column's first block, 5 cells away one course down, is held. That is beyond the local
        // course check (M9-T2 had CourseBelow here), so the ledge reads Ready; as a 1-block batch it is not posted
        // while that course below is held (CON-12 small-batch hold).
        var ledge = new Int3(5, G + 1, 5);
        Assert.Equal(BuildStatus.Ready, sim.Plans.StatusOf(sim, ledge));
        Assert.False(BlockBuildSystem.HeldCells(sim).ContainsKey(sim.World.Index(ledge)));
        Assert.Null(sim.Plans.StatusOf(sim, new Int3(3, G, 3)));

        var held = BlockBuildSystem.HeldCells(sim);
        foreach (var c in new[] { waitSupport, belowFirst, noMaterial, occupied, noAccess })
            Assert.False(held.ContainsKey(sim.World.Index(c)), $"{c} is held by a Build job");

        // Stone does not help a Planks entry; logs do.
        Hub(sim).Stored[TestContent.Db.Item("log").Value] = 3;
        Assert.Equal(BuildStatus.Ready, sim.Plans.StatusOf(sim, noMaterial));
        sim.Tick();
        Assert.Contains(Builds(sim), j => j.Steps.Any(s => s.Kind == StepKind.Place && s.Cell == noMaterial));
        RunUntil(sim, () => sim.World.GetBlock(noMaterial) == BlockId.Planks, 2000);
        Assert.Equal(BuildStatus.NoAccess, sim.Plans.StatusOf(sim, noAccess));
    }

    /// <summary>buildings.md scenario 3 with a Masonry block across the 1-wide river instead of a levee.</summary>
    [Fact]
    public void MasonryWall_HoldsWaterLikeLevee()
    {
        const int C = 5, Bank = 6;
        var b = new ScenarioBuilder().Ground(C - 1);
        var rows = new string[32];
        for (int z = 0; z < 32; z++) rows[z] = new string(z == 10 ? '.' : 'S', 32);
        b.Layer(C, rows).Source(new Int3(0, C, 10)).Drain(new Int3(31, C, 10));
        var sim = b.Hub(new Int3(20, Bank, 20)).Stock("stone", 10)
            .Agent(new Int3(15, Bank, 15)).Agent(new Int3(16, Bank, 15)).Build();
        long Accounted() => sim.Water.TotalVolume() + sim.Water.Stats.Drained + sim.Water.Stats.Evaporated
            + sim.Water.Stats.Pumped - sim.Water.Stats.SourceAdded;

        var cell = new Int3(22, C, 10);
        var downstream = new Int3(24, C, 10);
        sim.RunTicks(600);
        Assert.True(sim.Water.GetLevel(cell) > 0, "the river does not reach the wall cell");
        Assert.True(sim.Water.GetLevel(downstream) > 0);

        long accounted = Accounted();
        sim.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(22, C, 8), new Int3(22, C, 12), 1, BlockId.Masonry, false));
        sim.Tick();
        Assert.Equal(1, sim.Plans.Count);   // the banks are solid: only the channel cell is planned
        bool pushChecked = false;
        RunUntil(sim, () => sim.World.GetBlock(cell) == BlockId.Masonry, 3000, () =>
        {
            Assert.Equal(accounted, Accounted());   // WAT-11 exact, including the tick the block goes in
            if (sim.World.GetBlock(cell) == BlockId.Masonry)
            {
                Assert.Equal(0, sim.Water.GetLevel(cell));
                pushChecked = true;
            }
        });
        Assert.True(pushChecked);
        int atCompletion = sim.Water.GetLevel(downstream);
        Assert.True(atCompletion > 0);
        sim.RunTicks(200);
        int after = sim.Water.GetLevel(downstream);
        Assert.True(after < atCompletion / 2, $"downstream level {atCompletion} -> {after}");
        Assert.Equal(accounted, Accounted());
    }

    [Fact]
    public void Cancel_MidBuild_KeepsPlacedBlocks_LosesNoStone()
    {
        var sim = WallWorld(30, out var wall);
        RunUntil(sim, () => Placed(sim, wall) >= 4 && CarriedTotal(sim, "stone") > 0, 3000);
        sim.Enqueue(new CancelDesignation(new Int3(10, G, 12), new Int3(15, G + 2, 12)));
        sim.Tick();
        int placed = Placed(sim, wall);
        Assert.True(placed is >= 4 and < 18);
        Assert.Equal(0, sim.Plans.Count);
        Assert.Empty(Builds(sim));

        RunUntil(sim, () => CarriedTotal(sim, "stone") == 0 && PileTotal(sim, "stone") == 0, 2000);
        Assert.Equal(placed, Placed(sim, wall));
        Assert.Equal(30, Stored(Hub(sim), "stone") + placed);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    [Fact]
    public void SaveLoad_MidBuild_ContinuesIdentically()
    {
        var sim = WallWorld(30, out var wall);
        RunUntil(sim, () => Placed(sim, wall) >= 2
            && Builds(sim).Any(j => j.IsClaimed) && CarriedTotal(sim, "stone") > 0, 3000);

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.Equal(sim.Plans.All.ToList(), loaded.Plans.All.ToList());
        for (int t = 1; t <= 1000; t++)
        {
            sim.Tick();
            loaded.Tick();
            if (t % 100 == 0) Assert.True(sim.StateHash() == loaded.StateHash(), $"hash differs {t} ticks after load");
        }
        Assert.Equal(18, Placed(loaded, wall));
    }
}
