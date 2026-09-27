using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M9-T2: build trips carry full batches (construction.md CON-05 course check, CON-12 chain batch; ADR-068).</summary>
public partial class BlockBuildScenarioTests
{
    private static Simulation OpenWorld(int stone) =>
        new ScenarioBuilder().Ground(G - 1)
            .FillBox(new Int3(8, G, 11), new Int3(8, G, 11), BlockId.Stone)     // a 1-high post near the line
            .FillBox(new Int3(27, G, 27), new Int3(27, G, 27), BlockId.Stone)   // one far from it
            .Hub(HubOrigin).Stock("stone", stone)
            .Agent(new Int3(12, G, 17)).Agent(new Int3(13, G, 17)).Build();

    /// <summary>CON-12 chain: a batch grows from each member, not only from the seed, so a straight 10-cell line is one
    /// job (before M9-T2 only the 5 cells within 4 of the seed joined).</summary>
    [Fact]
    public void StraightLine_OneFullBatch()
    {
        var sim = OpenWorld(30);
        sim.Tick();   // regions are built at the end of the first tick
        Designate(sim, BuildShape.Line, new Int3(5, G, 5), new Int3(14, G, 5));
        var job = Assert.Single(Builds(sim));
        Assert.Equal(10, job.Steps.Count(s => s.Kind == StepKind.Place));
        RunUntil(sim, () => sim.Plans.Count == 0, 4000);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>CON-05 course check (made local by M10-T4, ADR-074): an entry waits (<c>CourseBelow</c>) while a Build
    /// job holds a cell one course down within <see cref="BlockPlans.CourseRadius"/> = 1, and is built once those cells
    /// are placed.</summary>
    [Fact]
    public void UpperCell_WaitsForHeldCellsBesideBelow()
    {
        var sim = OpenWorld(30);
        sim.Tick();
        var near = new Int3(8, G + 1, 11);   // on the post, diagonally above the line's middle cell
        var line = BuildShapes.Cells(BuildShape.Line, new Int3(7, G, 10), new Int3(9, G, 10), 1);
        sim.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(7, G, 10), new Int3(9, G, 10), 1, BlockId.Masonry, false));
        // Rough stone, so it cannot join the line's Masonry batch.
        sim.Enqueue(new DesignateBuild(BuildShape.Single, near, near, 1, BlockId.Rubble, false));
        sim.Tick();
        Assert.Equal(BuildStatus.CourseBelow, sim.Plans.StatusOf(sim, near));

        long lineDone = -1, nearDone = -1;
        RunUntil(sim, () => sim.Plans.Count == 0, 4000, () =>
        {
            if (lineDone < 0 && line.All(c => sim.World.GetBlock(c) == BlockId.Masonry)) lineDone = sim.Clock.Tick;
            if (nearDone < 0 && sim.World.GetBlock(near) == BlockId.Rubble) nearDone = sim.Clock.Tick;
            if (sim.Plans.Get(near) is not null && line.Any(c => BlockBuildSystem.HeldCells(sim).ContainsKey(sim.World.Index(c))))
                Assert.Equal(BuildStatus.CourseBelow, sim.Plans.StatusOf(sim, near));
        });
        Assert.True(lineDone > 0 && nearDone >= lineDone, $"line done {lineDone}, near cell placed {nearDone}");
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>CON-12 small-batch hold (M10-T4, ADR-074): with the course below held within
    /// <see cref="BlockBuildSystem.HoldRadius"/>, a batch of at least 60% of a load (6 of 10) is posted at once, so the
    /// next course starts before the one below is done; a smaller one waits (still <c>Ready</c>, not held) until the held
    /// cells are placed, so the trip is not wasted on one block. Farther away nothing waits.</summary>
    [Fact]
    public void UpperCourse_FullEnoughBatchStarts_SmallOneWaitsForHeldCourseBelow()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(new Int3(8, G, 11), new Int3(13, G, 11), BlockId.Stone)    // a 6-long ridge 6 from the line
            .FillBox(new Int3(20, G, 11), new Int3(20, G, 11), BlockId.Stone)   // a post 6 from the line's end
            .FillBox(new Int3(27, G, 27), new Int3(27, G, 27), BlockId.Stone)   // one far from it
            .Hub(HubOrigin).Stock("stone", 30)
            .Agent(new Int3(12, G, 17)).Agent(new Int3(13, G, 17)).Build();
        sim.Tick();
        var ridge = BuildShapes.Cells(BuildShape.Line, new Int3(8, G + 1, 11), new Int3(13, G + 1, 11), 1);
        var near = new Int3(20, G + 1, 11);
        var far = new Int3(27, G + 1, 27);
        var line = BuildShapes.Cells(BuildShape.Line, new Int3(5, G, 5), new Int3(14, G, 5), 1);
        sim.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(5, G, 5), new Int3(14, G, 5), 1, BlockId.Masonry, false));
        sim.Enqueue(new DesignateBuild(BuildShape.Line, ridge[0], ridge[^1], 1, BlockId.Masonry, false));
        sim.Enqueue(new DesignateBuild(BuildShape.Single, near, near, 1, BlockId.Masonry, false));
        sim.Enqueue(new DesignateBuild(BuildShape.Single, far, far, 1, BlockId.Masonry, false));
        sim.Tick();
        Assert.All(ridge, c => Assert.Equal(BuildStatus.InJob, sim.Plans.StatusOf(sim, c)));
        Assert.Equal(6, Builds(sim).Single(j => j.Target == ridge[0]).Steps.Count(s => s.Kind == StepKind.Place));
        Assert.Equal(BuildStatus.InJob, sim.Plans.StatusOf(sim, far));
        Assert.Equal(BuildStatus.Ready, sim.Plans.StatusOf(sim, near));
        Assert.False(BlockBuildSystem.HeldCells(sim).ContainsKey(sim.World.Index(near)));

        long lineDone = -1, nearDone = -1;
        RunUntil(sim, () => sim.Plans.Count == 0, 6000, () =>
        {
            if (lineDone < 0 && line.All(c => sim.World.GetBlock(c) == BlockId.Masonry)) lineDone = sim.Clock.Tick;
            if (nearDone < 0 && sim.World.GetBlock(near) == BlockId.Masonry) nearDone = sim.Clock.Tick;
        });
        Assert.True(lineDone > 0 && nearDone >= lineDone, $"line done {lineDone}, near cell placed {nearDone}");
        Assert.All(ridge, c => Assert.Equal(BlockId.Masonry, sim.World.GetBlock(c)));
        Assert.Equal(BlockId.Masonry, sim.World.GetBlock(far));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }
}
