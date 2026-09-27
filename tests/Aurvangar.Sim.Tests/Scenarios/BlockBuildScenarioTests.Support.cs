using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Entities;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M10-T1 (CON-05 check 5, VIEW-22, ADR-071): a released entry that is not supported now, but will be once
/// the entries it leans on are built, waits (<c>WaitSupport</c>, not red). An entry the plan can never support stays
/// <c>NoSupport</c> (red).</summary>
public partial class BlockBuildScenarioTests
{
    [Fact]
    public void LeaningOnPlannedCells_Waits_TrulyFloating_StaysRed()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(new Int3(4, G, 5), new Int3(4, G + 1, 5), BlockId.Stone)      // a 2-high post
            .FillBox(new Int3(4, G, 15), new Int3(4, G + 1, 15), BlockId.Stone)    // another one
            .Hub(HubOrigin).Stock("stone", 30)
            .Agent(new Int3(12, G, 12)).Agent(new Int3(13, G, 12)).Build();
        sim.Tick();   // regions are built at the end of the first tick

        // A ledge off the first post, planned: (5) rests on the post, (6) and (7) on the entries beside them.
        var ledge = new[] { new Int3(5, G + 1, 5), new Int3(6, G + 1, 5), new Int3(7, G + 1, 5) };
        Designate(sim, BuildShape.Line, ledge[0], ledge[2], plan: true);
        // The same off the second post, then its first cell is cancelled: (6) and (7) float with nothing planned under
        // or beside them that reaches the ground.
        var cut = new[] { new Int3(5, G + 1, 15), new Int3(6, G + 1, 15), new Int3(7, G + 1, 15) };
        Designate(sim, BuildShape.Line, cut[0], cut[2], plan: true);
        sim.Enqueue(new CancelDesignation(cut[0], cut[0]));
        sim.Tick();
        Assert.Equal(5, sim.Plans.Count);

        // Leaning on a Planned (not released) entry also counts as plan support.
        sim.Enqueue(new ReleasePlan(ledge[1], ledge[2]));
        sim.Enqueue(new ReleasePlan(cut[1], cut[2]));
        sim.Tick();
        Assert.Equal(BuildStatus.Planned, sim.Plans.StatusOf(sim, ledge[0]));
        Assert.Equal(BuildStatus.WaitSupport, sim.Plans.StatusOf(sim, ledge[1]));
        Assert.Equal(BuildStatus.WaitSupport, sim.Plans.StatusOf(sim, ledge[2]));
        Assert.Equal(BuildStatus.NoSupport, sim.Plans.StatusOf(sim, cut[1]));
        Assert.Equal(BuildStatus.NoSupport, sim.Plans.StatusOf(sim, cut[2]));

        sim.Enqueue(new ReleasePlan(ledge[0], ledge[0]));
        sim.Tick();
        var ghosts = PlanGhostMesher.Ghosts(sim, sim.World.SizeY - 1);
        Assert.All(ghosts, g => Assert.Equal(sim.Plans.StatusOf(sim, g.Cell), g.Status));
        foreach (var g in ghosts)
            Assert.Equal(g.Cell.Z == 15, PlanGhostMesher.IsStuck(g.Status));

        // The ledge builds out from the post; the cut ledge never does and stays red.
        RunUntil(sim, () => ledge.All(c => sim.World.GetBlock(c) == BlockId.Masonry), 3000);
        Assert.Equal(2, sim.Plans.Count);
        Assert.Equal(BuildStatus.NoSupport, sim.Plans.StatusOf(sim, cut[1]));
        Assert.Equal(BuildStatus.NoSupport, sim.Plans.StatusOf(sim, cut[2]));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }
}
