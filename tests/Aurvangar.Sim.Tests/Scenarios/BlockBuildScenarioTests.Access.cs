using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Hud;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M8-T2: access rules of Build jobs: the strand rule (CON-14), stand cells on built blocks (CON-11) and
/// give-up marks (CON-15).</summary>
public partial class BlockBuildScenarioTests
{
    private static readonly Int3 HallEntrance = new(21, G, 19);

    /// <summary>construction.md scenario 4. First a 5x5 ring two high with no door, built from scratch while one dwarf
    /// starts inside: every dwarf stays in the hall's region every tick and the ring completes with nobody inside.
    /// Then a stone ring with one gap (the closing cell) and a dig mark in its middle: while a dwarf digs inside, the
    /// closing block waits (WouldStrand); the dwarf steps out when idle, and the ring closes.</summary>
    [Fact]
    public void ClosedRing_NeverWallsInADwarf()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HubOrigin).Stock("stone", 40)
            .Agent(new Int3(10, G, 10)).Agent(new Int3(14, G, 16)).Build();
        var ring = BuildShapes.Cells(BuildShape.HollowBox, new Int3(8, G, 8), new Int3(12, G, 12), 2);
        Assert.Equal(32, ring.Count);
        sim.Tick();
        Designate(sim, BuildShape.HollowBox, new Int3(8, G, 8), new Int3(12, G, 12), 2);
        Assert.Equal(32, sim.Plans.Count);
        RunUntil(sim, () => sim.Plans.Count == 0, 8000, () => AllInHallRegion(sim));
        Assert.All(ring, c => Assert.Equal(BlockId.Masonry, sim.World.GetBlock(c)));
        Assert.All(sim.Agents.All, a => Assert.False(Inside(a.Cell), $"{a.Name} is inside"));
        Assert.Equal(0, sim.Counters.JobsFailed);

        // The closing block, with a dwarf working inside.
        var gap = new Int3(8, G + 1, 10);
        var b = new ScenarioBuilder().Ground(G - 1).Hub(HubOrigin).Stock("stone", 10)
            .Agent(new Int3(10, G, 9)).Agent(new Int3(14, G, 16));
        foreach (var c in ring) b.FillBox(c, c, BlockId.Stone);
        sim = b.FillBox(gap, gap, BlockId.Air).Build();
        sim.Tick();
        Designate(sim, BuildShape.HollowBox, new Int3(8, G, 8), new Int3(12, G, 12), 2);
        Assert.Equal(1, sim.Plans.Count);   // every other ring cell is solid already
        sim.Enqueue(new Commands.DesignateDig(new Int3(10, G - 1, 10), new Int3(10, G - 1, 10)));
        bool waited = false, workedInside = false;
        RunUntil(sim, () => sim.World.GetBlock(gap) == BlockId.Masonry, 4000, () =>
        {
            AllInHallRegion(sim);
            foreach (var a in sim.Agents.All)
                if (Inside(a.Cell) && sim.Jobs.Get(a.CurrentJob) is { Kind: JobKind.Dig }) workedInside = true;
            if (sim.Plans.Count > 0 && sim.Plans.StatusOf(sim, gap) == BuildStatus.WouldStrand)
            {
                waited = true;
                Assert.Contains(sim.Agents.All, a => Inside(a.Cell) || Inside(a.NextCell));
            }
        });
        Assert.True(workedInside, "no dwarf dug inside the ring");
        Assert.True(waited, "the closing block never waited for a dwarf");
        Assert.All(sim.Agents.All, a => Assert.False(Inside(a.Cell), $"{a.Name} was walled in"));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    private static bool Inside(Int3 c) => c.X is >= 9 and <= 11 && c.Z is >= 9 and <= 11;

    private static void AllInHallRegion(Simulation sim)
    {
        int hall = sim.Regions.RegionOf(HallEntrance);
        foreach (var a in sim.Agents.All)
            Assert.True(sim.Regions.RegionOf(a.Cell) == hall, $"{a.Name} at {a.Cell} left the hall's region at tick {sim.Clock.Tick}");
    }

    /// <summary>construction.md scenario 5: a 5-high wall at z = 12 (x 11..13) with a 4-step Stair against it at z = 13,
    /// rising from (10,9,13) to (13,12,13). From the ground a dwarf reaches only the two lowest courses; every block
    /// above is placed from the stair (or the wall's own lower courses), and the stair steps rest on the wall beside them
    /// (CON-09), so wall and stair go up together (ADR-061: the plan gives the way up).</summary>
    [Fact]
    public void HighWall_WithStair_BuiltFromBuiltBlocks()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HubOrigin).Stock("stone", 40)
            .Agent(new Int3(12, G, 17)).Agent(new Int3(13, G, 17)).Build();
        var wall = BuildShapes.Cells(BuildShape.Wall, new Int3(11, G, 12), new Int3(13, G, 12), 5);
        var stair = BuildShapes.Cells(BuildShape.Stair, new Int3(10, G, 13), new Int3(13, G, 13), 1);
        sim.Enqueue(new Commands.DesignateBuild(BuildShape.Wall, new Int3(11, G, 12), new Int3(13, G, 12), 5, BlockId.Masonry, false));
        sim.Enqueue(new Commands.DesignateBuild(BuildShape.Stair, new Int3(10, G, 13), new Int3(13, G, 13), 1, BlockId.Masonry, false));
        sim.Tick();
        Assert.Equal(19, sim.Plans.Count);

        bool fromBuilt = false;
        RunUntil(sim, () => sim.Plans.Count == 0, 12000, () =>
        {
            foreach (var a in sim.Agents.All)
            {
                if (sim.Jobs.Get(a.CurrentJob) is not { Kind: JobKind.Build } j || a.StepIndex >= j.Steps.Count) continue;
                if (j.Steps[a.StepIndex].Kind is StepKind.Work or StepKind.Place
                    && sim.Content.IsConstruction(sim.World.GetBlock(a.Cell + Int3.Down)))
                    fromBuilt = true;
            }
        });
        Assert.All(wall.Concat(stair), c => Assert.Equal(BlockId.Masonry, sim.World.GetBlock(c)));
        Assert.True(fromBuilt, "no block was placed from a stand cell on a built block");
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.Equal(40 - 19, Stored(Hub(sim), "stone") + CarriedTotal(sim, "stone") + PileTotal(sim, "stone"));
    }

    /// <summary>CON-15: a trench two deep across the map at z = 16 splits the builders (north) from the hub (south).
    /// The entry sits in the trench's top cell, so it has stand cells on both banks: the builders can reach it but not
    /// the stone. It is given up after three region checks; filling the trench nearby resets it and it gets built.</summary>
    [Fact]
    public void UnreachableEntry_GivenUp_ResetNearby()
    {
        const int trenchZ = 16;
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(new Int3(0, G - 2, trenchZ), new Int3(31, G - 1, trenchZ), BlockId.Air)
            .Hub(HubOrigin).Stock("stone", 10)
            .Agent(new Int3(10, G, 10)).Agent(new Int3(11, G, 10)).Build();
        sim.Tick();   // regions are built at the end of the first tick
        var cell = new Int3(10, G - 1, trenchZ);
        Designate(sim, BuildShape.Single, cell, cell);
        int id = sim.World.Index(cell);
        Assert.Contains(Builds(sim), j => j.Target == cell);

        RunUntil(sim, () => sim.GiveUps.IsGivenUp(GiveUpSource.Build, id), 400);
        sim.Tick();   // the next re-check cancels the job whose seed was given up
        Assert.Empty(Builds(sim));
        Assert.Equal(BuildStatus.GivenUp, sim.Plans.StatusOf(sim, cell));
        Assert.Contains(TopBarModel.UnreachablePrefix + "Stone wall", TopBarModel.Build(sim, 1).Alerts);
        Assert.Equal(0, sim.Counters.JobsFailed);
        for (int i = 0; i < 300; i++)
        {
            sim.Tick();
            Assert.Empty(Builds(sim));
        }

        // Filling the trench 5 cells away (within the reset radius) brings it back; the builders cross and build it.
        Assert.True(JobGiveUp.ResetRadius >= 5);
        sim.World.SetBlock(new Int3(5, G - 2, trenchZ), BlockId.Stone);
        sim.World.SetBlock(new Int3(5, G - 1, trenchZ), BlockId.Stone);
        sim.Tick();
        Assert.False(sim.GiveUps.IsGivenUp(GiveUpSource.Build, id));
        RunUntil(sim, () => sim.World.GetBlock(cell) == BlockId.Masonry, 3000);
        Assert.Equal(0, sim.Plans.Count);
        Assert.Empty(sim.GiveUps.All);
        Assert.DoesNotContain(TopBarModel.Build(sim, 1).Alerts, a => a.StartsWith(TopBarModel.UnreachablePrefix));
    }

    /// <summary>CON-11 preference (M8-T6, ADR-066): the block beside a two-high stone pillar can be placed from the
    /// pillar's top, a free cell no dwarf can reach (a step of two), or from the ground around it, whose cells all carry
    /// plan entries. The preference for free stand cells counts only reachable ones, so the Build job goes to the
    /// ground cells: the block is placed, nothing fails and nothing is given up. (It used to go to the pillar top alone,
    /// and the job was struck as unreachable until given up, as on the monument's courtyard wall top.)</summary>
    [Fact]
    public void PreferredStand_OnlyWhereADwarfCanReach()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(new Int3(10, G, 10), new Int3(10, G + 1, 10), BlockId.Stone)
            .Hub(HubOrigin).Stock("stone", 10)
            .Agent(new Int3(14, G, 14)).Agent(new Int3(15, G, 14)).Build();
        var target = new Int3(11, G + 1, 10);
        var pillarTop = new Int3(10, G + 2, 10);
        Designate(sim, BuildShape.Wall, new Int3(10, G, 9), new Int3(12, G, 9), plan: true);
        Designate(sim, BuildShape.Wall, new Int3(12, G, 10), new Int3(12, G, 10), plan: true);
        Designate(sim, BuildShape.Wall, new Int3(10, G, 11), new Int3(12, G, 11), plan: true);
        Designate(sim, BuildShape.Wall, target, target, plan: true);
        Assert.Equal(8, sim.Plans.Count);
        Assert.True(sim.PathGrid.IsWalkable(pillarTop));
        int hall = sim.Regions.RegionOf(HallEntrance);
        Assert.NotEqual(hall, sim.Regions.RegionOf(pillarTop));

        var goals = JobGoals.BuildStandCells(sim, target, strandFree: true, prefer: true);
        Assert.NotEqual(new[] { pillarTop }, goals);   // no preference: every stand cell, the ground ones too
        Assert.Contains(goals, c => sim.Regions.RegionOf(c) == hall);

        sim.Enqueue(new Commands.ReleasePlan(target, target));
        RunUntil(sim, () => sim.World.GetBlock(target) == BlockId.Masonry, 3000);
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.Empty(sim.GiveUps.All);
        Assert.Equal(7, sim.Plans.Count);
    }
}
