using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M7-T6 (G3 answer 8, ADR-059): the strand rule protects every dwarf, not only the digger. A dig that would
/// cut any living dwarf's cell off from the Great Hall waits until that dwarf has left.</summary>
[Trait("Category", "Scenario")]
public class StrandAnyDwarfScenarioTests
{
    // A walled 3x3 yard (x, z 10..12, stand y = 9) whose only way in is a 1-wide gap at (11, 9, 9). Under the gap's
    // floor block (11, 8, 9) a 2-deep shaft is already dug, so digging that block cuts the yard off from outside.
    private static readonly Int3 GapFloor = new(11, 8, 9);
    private static readonly Int3 YardMin = new(10, 8, 10), YardMax = new(12, 8, 12);

    private static ScenarioBuilder Yard() =>
        new ScenarioBuilder().Ground(8)
            .FillBox(new Int3(9, 9, 9), new Int3(13, 10, 13), BlockId.BuildingSolid)
            .FillBox(new Int3(10, 9, 10), new Int3(12, 10, 12), BlockId.Air)
            .FillBox(new Int3(11, 9, 9), new Int3(11, 10, 9), BlockId.Air)
            .FillBox(new Int3(11, 6, 9), new Int3(11, 7, 9), BlockId.Air)
            .Hub(new Int3(20, 9, 20)).Stock("water", 30).Stock("berries", 30);

    private static void AssertAllInHubRegion(Simulation sim, Building hub)
    {
        int region = sim.Regions.RegionOf(hub.EntranceCell);
        Assert.NotEqual(Paths.Regions.None, region);
        foreach (var a in sim.Agents.All)
            Assert.True(sim.Regions.RegionOf(a.Cell) == region,
                $"tick {sim.Clock.Tick}: {a.Name} at {a.Cell} is in region {sim.Regions.RegionOf(a.Cell)}, hub is {region}");
    }

    /// <summary>The backlog scenario: a second dwarf works inside the yard (digging it out, then hauling the stone)
    /// while the first is sent to dig the gap from outside. The gap waits until the yard is empty of dwarves; no dwarf
    /// is ever cut off, nothing fails, and the gap is dug in the end.</summary>
    [Fact]
    public void SecondDwarfWorkingInsidePit_GapDigWaitsUntilItLeaves()
    {
        var sim = Yard().Agent(new Int3(11, 9, 5)).Agent(new Int3(11, 9, 11)).Build();
        var hub = sim.Buildings.All.First();
        sim.Enqueue(new DesignateDig(GapFloor, GapFloor));
        sim.Enqueue(new DesignateDig(YardMin, YardMax));

        long gapDug = -1, yardEmptied = -1;
        for (int t = 0; t < 8000 && gapDug < 0; t++)
        {
            sim.Tick();
            AssertAllInHubRegion(sim, hub);
            if (yardEmptied < 0 && !sim.World.IsSolid(new Int3(11, 8, 11))) yardEmptied = sim.Clock.Tick;
            if (!sim.World.IsSolid(GapFloor)) gapDug = sim.Clock.Tick;
        }
        Assert.True(gapDug > 0, $"the gap was never dug; jobs: {string.Join(", ", sim.Jobs.All.Select(j => $"{j.Kind}@{j.Target}"))}");
        Assert.True(yardEmptied > 0 && yardEmptied < gapDug, $"yard dug at {yardEmptied}, gap at {gapDug}");
        foreach (var a in sim.Agents.All)
            Assert.False(a.Cell.X is >= 10 and <= 12 && a.Cell.Z is >= 10 and <= 12, $"{a.Name} is inside the yard at {a.Cell}");
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>Two idle dwarves stand in the yard. Each would dig the gap from outside, but each holds it up for the
    /// other; without anything else to do they walk out towards the hall, and then the gap is dug.</summary>
    [Fact]
    public void IdleDwarvesInsidePocket_WalkOut_ThenGapIsDug()
    {
        var sim = Yard().Agent(new Int3(10, 9, 11)).Agent(new Int3(12, 9, 11)).Build();
        var hub = sim.Buildings.All.First();
        sim.Enqueue(new DesignateDig(GapFloor, GapFloor));

        for (int t = 0; t < 600 && sim.World.IsSolid(GapFloor); t++)
        {
            sim.Tick();
            AssertAllInHubRegion(sim, hub);
        }
        Assert.False(sim.World.IsSolid(GapFloor), "the gap was never dug");
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>The rule is about living dwarves in the hall's region: a dwarf already cut off elsewhere does not hold
    /// up a dig that cannot cut it any further.</summary>
    [Fact]
    public void DwarfAlreadyApart_DoesNotBlockDig()
    {
        // The second dwarf stands in a sealed cell (walled in on every side, no gap).
        var sim = Yard().Agent(new Int3(11, 9, 5))
            .FillBox(new Int3(24, 9, 2), new Int3(28, 11, 6), BlockId.BuildingSolid)
            .FillBox(new Int3(26, 9, 4), new Int3(26, 10, 4), BlockId.Air)
            .Agent(new Int3(26, 9, 4)).Build();
        sim.Enqueue(new DesignateDig(GapFloor, GapFloor));

        for (int t = 0; t < 1000 && sim.World.IsSolid(GapFloor); t++) sim.Tick();
        Assert.False(sim.World.IsSolid(GapFloor), "the gap was never dug");
        Assert.Equal(0, sim.Counters.JobsFailed);
    }
}
