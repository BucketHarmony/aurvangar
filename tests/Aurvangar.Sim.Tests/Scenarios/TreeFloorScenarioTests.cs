using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.Tests.Support;
using Xunit;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M4-T15 (G2 answer 2, ADR-038): a dig drag also marks the floors under trees; each waits until its tree is
/// felled and is then dug like any other cell, so dig + chop over a wooded box leaves no one-cell pillars.</summary>
[Trait("Category", "Scenario")]
public class TreeFloorScenarioTests
{
    [Fact]
    public void DigAndChop_WoodedBox_NoPillarsLeft()
    {
        var sim = new ScenarioBuilder().Ground(8)
            .Layer(new Int3(8, 9, 8),
                "......",
                ".T....",
                "....T.",
                "......",
                "..T...",
                ".....T")
            .Hub(new Int3(20, 9, 20)).Agent(new Int3(4, 9, 4)).Agent(new Int3(5, 9, 4))
            .Build();
        Int3 min = new(8, 7, 8), max = new(13, 8, 13);
        Assert.Equal(4, sim.Plants.All.Count(p => p.Kind == PlantKind.Tree));
        sim.Enqueue(new DesignateDig(min, max));
        sim.Enqueue(new DesignateChop(min.X, min.Z, max.X, max.Z));
        sim.Tick();
        Assert.Equal(6 * 6 * 2, sim.Designations.Count);   // tree floors included

        for (int t = 0; t < 12000 && sim.Designations.All.Any(m => m.Mark == DesignationMark.Dig); t++) sim.Tick();

        Assert.DoesNotContain(sim.Plants.All, p => p.Kind == PlantKind.Tree);
        // Every column of the box is dug through its top layer: no one-cell pillar stands where a tree was.
        for (int z = min.Z; z <= max.Z; z++)
            for (int x = min.X; x <= max.X; x++)
                Assert.False(sim.World.IsSolid(new Int3(x, max.Y, z)), $"pillar left at ({x}, {max.Y}, {z})");
        Assert.DoesNotContain(sim.Designations.All, m => m.Mark == DesignationMark.Dig);
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Dig);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>A 4-deep pit with a tree in the middle, dug by 3 dwarves: the digs below the tree wait in steps
    /// (TreeFloors), so the tree stays in reach and is felled, and then its floor is dug too.</summary>
    [Fact]
    public void DeepPit_TreeInMiddle_StaysInReachAndIsFelled()
    {
        var sim = new ScenarioBuilder().Ground(8)
            .Layer(new Int3(12, 9, 12), "T")
            .Hub(new Int3(24, 9, 24)).Stock("water", 80).Stock("berries", 60)   // M5-T5: needs over up to 20000 ticks
            .Agent(new Int3(4, 9, 4)).Agent(new Int3(5, 9, 4)).Agent(new Int3(6, 9, 4))
            .Build();
        var hub = sim.Buildings.All.First();
        Int3 min = new(8, 5, 8), max = new(16, 8, 16);
        // Mark the dig first; the chop comes later, so already posted digs below the tree must be withdrawn.
        sim.Enqueue(new DesignateDig(min, max));
        sim.RunTicks(30);
        sim.Enqueue(new DesignateChop(12, 12, 12, 12));

        bool WorkLeft() => sim.Jobs.All.Any(j => j.Kind is JobKind.Dig or JobKind.Chop)
            || sim.Designations.All.Any(m => m.Mark == DesignationMark.Dig && DesignationSystem.Exposed(sim.World, m.Cell));
        for (int t = 0; t < 20000 && WorkLeft(); t++)
        {
            sim.Tick();
            foreach (var a in sim.Agents.All)
                Assert.Equal(sim.Regions.RegionOf(hub.EntranceCell), sim.Regions.RegionOf(a.Cell));
        }
        Assert.DoesNotContain(sim.Plants.All, p => p.Kind == PlantKind.Tree);
        Assert.False(sim.World.IsSolid(new Int3(12, 8, 12)), "the tree's floor was dug");
        Assert.DoesNotContain(sim.Designations.All, m => m.Mark == DesignationMark.Dig && DesignationSystem.Exposed(sim.World, m.Cell));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }
}
