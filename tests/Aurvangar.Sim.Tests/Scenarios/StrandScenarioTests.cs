using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M4-T14 (G2 answer 1b, ADR-037): a dwarf never takes or finishes a dig that would cut its standing cell
/// off from the Great Hall's region. Such a dig waits, and turns DigUnreachable once no other open dig can help.</summary>
[Trait("Category", "Scenario")]
public class StrandScenarioTests
{
    private static int HubRegion(Simulation sim, Building hub) => sim.Regions.RegionOf(hub.EntranceCell);

    private static void AssertAllInHubRegion(Simulation sim, Building hub)
    {
        int region = HubRegion(sim, hub);
        Assert.NotEqual(Paths.Regions.None, region);
        foreach (var a in sim.Agents.All)
            Assert.True(sim.Regions.RegionOf(a.Cell) == region,
                $"tick {sim.Clock.Tick}: {a.Name} at {a.Cell} is in region {sim.Regions.RegionOf(a.Cell)}, hub is {region}");
    }

    private static int AirCount(Simulation sim, Int3 min, Int3 max)
    {
        int n = 0;
        for (int y = min.Y; y <= max.Y; y++)
            for (int z = min.Z; z <= max.Z; z++)
                for (int x = min.X; x <= max.X; x++)
                    if (!sim.World.IsSolid(new Int3(x, y, z))) n++;
        return n;
    }

    /// <summary>One dwarf, a 3x3x3 pit: digging goes top-down until the last step out is the only way up. That dig is
    /// never taken (no failure), and becomes DigUnreachable once nothing else is left to dig.</summary>
    [Fact]
    public void SmallPit_DiggerNeverStranded_LastStepTurnsUnreachable()
    {
        // M5-T5: the hub is provisioned so the dwarf does not die of thirst during the 8000 ticks (ECO-06).
        var sim = new ScenarioBuilder().Ground(8).Hub(new Int3(20, 9, 20)).Stock("water", 20).Stock("berries", 20)
            .Agent(new Int3(5, 9, 5)).Build();
        var hub = sim.Buildings.All.First();
        Int3 min = new(10, 6, 10), max = new(12, 8, 12);
        sim.Enqueue(new DesignateDig(min, max));

        for (int t = 0; t < 8000; t++)
        {
            sim.Tick();
            AssertAllInHubRegion(sim, hub);
        }
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Dig);
        Assert.Contains(sim.Designations.All, m => m.Mark == DesignationMark.DigUnreachable);
        Assert.DoesNotContain(sim.Designations.All, m => m.Mark == DesignationMark.Dig && DesignationSystem.Exposed(sim.World, m.Cell));
        Assert.InRange(AirCount(sim, min, max), 20, 26);
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.Equal(AgentState.Idle, sim.Agents.All.Single().State);
    }

    /// <summary>ADR-037 review fix: a dig no one can reach (inside a walled yard) never runs, so it must not keep the
    /// pit's stranding digs from turning red.</summary>
    [Fact]
    public void UnreachableDigElsewhere_DoesNotBlockGiveUp()
    {
        var sim = new ScenarioBuilder().Ground(8).Hub(new Int3(20, 9, 20)).Agent(new Int3(5, 9, 5))
            .FillBox(new Int3(24, 9, 2), new Int3(30, 11, 8), BlockId.BuildingSolid)
            .FillBox(new Int3(25, 9, 3), new Int3(29, 11, 7), BlockId.Air)
            .Build();
        var hub = sim.Buildings.All.First();
        var yard = new Int3(27, 8, 5);
        sim.Enqueue(new DesignateDig(new Int3(10, 6, 10), new Int3(12, 8, 12)));
        sim.Enqueue(new DesignateDig(yard, yard));

        for (int t = 0; t < 8000; t++)
        {
            sim.Tick();
            AssertAllInHubRegion(sim, hub);
        }
        Assert.Contains(sim.Designations.All, m => m.Mark == DesignationMark.DigUnreachable);
        Assert.Equal(BlockId.Stone, sim.World.GetBlock(yard));
        Assert.Equal(new[] { yard }, sim.Jobs.All.Where(j => j.Kind == JobKind.Dig).Select(j => j.Target).ToArray());
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>A 1-wide trench deepened to 2: digging a trench floor cell opens a lower cell that still links its
    /// neighbors, so those digs are allowed (the check counts the cells a dig opens, not only the one it removes).</summary>
    [Fact]
    public void Trench_DeepeningAllowed_OnlyTheExitStepStays()
    {
        var sim = new ScenarioBuilder().Ground(8).Hub(new Int3(20, 9, 20)).Agent(new Int3(5, 9, 12)).Build();
        var hub = sim.Buildings.All.First();
        Int3 min = new(8, 7, 12), max = new(14, 8, 12);
        sim.Enqueue(new DesignateDig(min, max));

        for (int t = 0; t < 6000; t++)
        {
            sim.Tick();
            AssertAllInHubRegion(sim, hub);
        }
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Dig);
        // 14 cells; at most one layer-1 step per trench end may stay.
        Assert.InRange(AirCount(sim, min, max), 12, 14);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>BACKLOG M4-T14: the 10x7x5 pit from M4-T11 (seed 1, east of the hall, with every tree within 24 cells
    /// marked). No dwarf ever leaves the hub region, and the chop jobs keep being taken until every marked tree is down.</summary>
    [Fact]
    public void Seed1_DeepPit_NoDwarfTrapped_ChopsContinue()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var hub = sim.Buildings.All.First();
        int maxX = hub.FootprintCells().Max(c => c.X);
        int cx = hub.Origin.X + 1, cz = hub.Origin.Z + 1, floor = hub.Origin.Y - 1;
        int x0 = maxX + 4;
        Int3 min = new(x0, floor - 4, cz - 3), max = new(x0 + 9, floor, cz + 3);
        sim.Enqueue(new DesignateDig(min, max));
        sim.Enqueue(new DesignateChop(cx - 24, cz - 24, cx + 24, cz + 24));
        sim.Tick();
        int marked = sim.Plants.All.Count(p => p.MarkedForChop);
        Assert.True(marked >= 10, $"only {marked} trees marked");
        Assert.True(sim.Designations.Count >= 200, $"only {sim.Designations.Count} dig marks");

        // M4-T15: tree floors in the pit box are marked too, so no tree is left on an undug pillar; every marked
        // tree comes down.
        bool WorkLeft() => sim.Plants.All.Any(p => p.MarkedForChop) || sim.Jobs.All.Any(j => j.Kind == JobKind.Dig);
        for (int t = 0; t < 7200 && WorkLeft(); t++)
        {
            sim.Tick();
            AssertAllInHubRegion(sim, hub);
        }
        var standing = sim.Plants.All.Where(p => p.MarkedForChop).Select(p => $"{p.Base} unreachable={p.ChopUnreachable}").ToList();
        Assert.True(standing.Count == 0, $"tick {sim.Clock.Tick}, pit {min}..{max}, marked trees left: {string.Join("; ", standing)}");
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Dig);
        Assert.Contains(sim.Designations.All, m => m.Mark == DesignationMark.DigUnreachable);
        Assert.True(AirCount(sim, min, max) >= 250, $"pit mostly dug: {AirCount(sim, min, max)} of 350");
    }
}
