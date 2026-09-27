using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>M4-T14 (ADR-037): the what-if connectivity test behind "digs never strand the digger".</summary>
[Trait("Category", "Unit")]
public class DigTrialTests
{
    /// <summary>Ground at y = 8 (stand at y = 9), a hub far away. A 1-wide trench x = 8..12 at z = 12 dug one deep
    /// (cells y = 8), and x = 9..12 also dug a second level (cells y = 7), so x = 8 is the only step out.</summary>
    private static Simulation Trench()
    {
        var sim = new ScenarioBuilder().Ground(8).Hub(new Int3(20, 9, 20))
            .FillBox(new Int3(8, 8, 12), new Int3(12, 8, 12), BlockId.Air)
            .FillBox(new Int3(9, 7, 12), new Int3(12, 7, 12), BlockId.Air)
            .Build();
        sim.Tick();   // regions
        return sim;
    }

    [Fact]
    public void DigUnderWalkableNothing_NeverSplits()
    {
        var sim = Trench();
        // A wall block of the trench: the cell above it is solid ground, so nothing walkable is lost.
        Assert.False(sim.DigTrial.MaySplit(new Int3(10, 7, 13)));
        Assert.False(DigStrand.Strands(sim, new Int3(10, 7, 13), new Int3(10, 7, 12)));
    }

    [Fact]
    public void LastExitStep_StrandsTheTrenchFloor_NotTheRim()
    {
        var sim = Trench();
        var step = new Int3(8, 7, 12);   // floor block of the step cell (8, 8, 12)
        Assert.True(sim.DigTrial.MaySplit(step));
        Assert.True(DigStrand.Strands(sim, step, new Int3(9, 7, 12)));    // on the trench floor: cut off
        Assert.False(DigStrand.Strands(sim, step, new Int3(7, 9, 12)));   // on the rim: still with the hall
    }

    [Fact]
    public void DeepeningAFloorCell_OpensALowerLink_NotStranding()
    {
        // Only the first level dug: x = 8..12 cells at y = 8. Digging (10, 7, 12) removes the walkable cell (10, 8, 12)
        // but opens (10, 7, 12), which still steps up to both (9, 8, 12) and (11, 8, 12).
        var sim = new ScenarioBuilder().Ground(8).Hub(new Int3(20, 9, 20))
            .FillBox(new Int3(8, 8, 12), new Int3(12, 8, 12), BlockId.Air).Build();
        sim.Tick();
        var dug = new Int3(10, 7, 12);
        Assert.False(sim.DigTrial.MaySplit(dug));
        Assert.False(DigStrand.Strands(sim, dug, new Int3(11, 8, 12)));
    }

    [Fact]
    public void NoHall_RuleIsOff()
    {
        var sim = new ScenarioBuilder().Ground(8)
            .FillBox(new Int3(8, 8, 12), new Int3(8, 8, 12), BlockId.Air)
            .FillBox(new Int3(9, 7, 12), new Int3(9, 8, 12), BlockId.Air).Build();
        sim.Tick();
        Assert.False(DigStrand.Strands(sim, new Int3(8, 7, 12), new Int3(9, 7, 12)));
    }

    /// <summary>M7-T6 (ADR-059): the exit step strands a dwarf standing on the trench floor, whoever digs it; a dwarf
    /// on the rim is not stranded, and the digger itself is left to the stand-cell rule.</summary>
    [Fact]
    public void ExitStep_StrandsOtherDwarfInTrench()
    {
        var sim = new ScenarioBuilder().Ground(8).Hub(new Int3(20, 9, 20))
            .FillBox(new Int3(8, 8, 12), new Int3(12, 8, 12), BlockId.Air)
            .FillBox(new Int3(9, 7, 12), new Int3(12, 7, 12), BlockId.Air)
            .Agent(new Int3(5, 9, 5)).Agent(new Int3(11, 7, 12)).Build();
        sim.Tick();   // regions
        var rim = sim.Agents.All.First();
        var inside = sim.Agents.All.Last();
        var step = new Int3(8, 7, 12);
        Assert.True(DigStrand.StrandsOthers(sim, step, rim.Id));
        Assert.False(DigStrand.StrandsOthers(sim, step, inside.Id));
        // A wall block of the trench cuts nobody.
        Assert.False(DigStrand.StrandsOthers(sim, new Int3(10, 7, 13), rim.Id));
    }
}
