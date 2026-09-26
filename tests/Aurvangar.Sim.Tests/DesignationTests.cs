using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>DSG-01..06 commands and DesignationSystem posting rules (M4-T7).</summary>
public class DesignationTests
{
    private static Simulation Apply(Simulation sim, ICommand cmd)
    {
        sim.Enqueue(cmd);
        sim.Tick();
        return sim;
    }

    [Fact]
    public void DesignateDig_MarksOnlyDiggableSolidNotUnderBuilding()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .FillBox(new Int3(2, 4, 2), new Int3(2, 4, 2), BlockId.Dirt)
            .Build();
        var hub = sim.Buildings.PlacePrebuilt(TestContent.Db.Building("hub"), new Int3(10, 5, 10), 0);

        // y = 0 bedrock, y = 1..4 stone/dirt, y = 5..6 air and the hub's BuildingSolid, all inside the box.
        Apply(sim, new DesignateDig(new Int3(0, 0, 0), new Int3(12, 6, 12)));

        Assert.Equal(DesignationMark.None, sim.Designations.Get(new Int3(3, 0, 3)));   // bedrock
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(new Int3(3, 1, 3)));    // stone
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(new Int3(2, 4, 2)));    // dirt
        Assert.Equal(DesignationMark.None, sim.Designations.Get(new Int3(3, 5, 3)));   // air
        Assert.Equal(DesignationMark.None, sim.Designations.Get(new Int3(10, 5, 10))); // BuildingSolid
        Assert.Equal(DesignationMark.None, sim.Designations.Get(new Int3(10, 4, 10))); // under the hub
        Assert.Equal(DesignationMark.None, sim.Designations.Get(new Int3(12, 4, 12))); // under the hub
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(new Int3(10, 3, 10)));  // two below: fine
        Assert.Equal(13 * 13 * 4 - 9, sim.Designations.Count);
        Assert.NotNull(hub);
    }

    [Fact]
    public void DesignateDig_SkipsPlantFloor()
    {
        var sim = new ScenarioBuilder().Ground(4).Layer(new Int3(5, 5, 5), "Tb").Build();
        Apply(sim, new DesignateDig(new Int3(4, 4, 5), new Int3(7, 4, 5)));
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(new Int3(4, 4, 5)));
        Assert.Equal(DesignationMark.None, sim.Designations.Get(new Int3(5, 4, 5)));   // under the tree
        Assert.Equal(DesignationMark.None, sim.Designations.Get(new Int3(6, 4, 5)));   // under the bush
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(new Int3(7, 4, 5)));
    }

    [Fact]
    public void UnreachableMark_OnAir_IsCleared()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var c = new Int3(5, 4, 5);
        sim.Designations.Set(c, DesignationMark.DigUnreachable);
        sim.Tick();
        Assert.Equal(DesignationMark.DigUnreachable, sim.Designations.Get(c));   // still solid: kept
        sim.World.SetBlock(c, BlockId.Air);
        sim.Tick();
        Assert.Equal(DesignationMark.None, sim.Designations.Get(c));
    }

    [Fact]
    public void DesignateDig_BoxCornersInAnyOrder_ClampedToWorld()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        Apply(sim, new DesignateDig(new Int3(33, 4, 2), new Int3(30, 4, -5)));
        Assert.Equal(2 * 3, sim.Designations.Count);   // x 30..31, z 0..2
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(new Int3(31, 4, 0)));
    }

    [Fact]
    public void DesignateDig_OutsideWorld_Rejected()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        sim.Events.Drain();
        Apply(sim, new DesignateDig(new Int3(40, 4, 40), new Int3(50, 4, 50)));
        Assert.Equal(0, sim.Designations.Count);
        Assert.Contains(sim.Events.Drain(), e => e is CommandRejected { Command: "DesignateDig" });
        Assert.Single(sim.Commands.Log);   // logged even when rejected (replay reproduces the rejection)
    }

    [Fact]
    public void DesignateDig_ExistingUnchanged_UnreachableRetried()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var c = new Int3(5, 2, 5);   // buried: no job
        var u = new Int3(6, 2, 5);
        sim.Designations.Set(u, DesignationMark.DigUnreachable);
        Apply(sim, new DesignateDig(c, u));
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(c));
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(u));
        Assert.Equal(2, sim.Designations.Count);
        Apply(sim, new DesignateDig(c, c));
        Assert.Equal(2, sim.Designations.Count);
    }

    [Fact]
    public void System_PostsOneJobPerExposedCell()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        // Column x=5,z=5 from y=2 to y=4: only the top (air above) is exposed.
        Apply(sim, new DesignateDig(new Int3(5, 2, 5), new Int3(5, 4, 5)));
        var jobs = sim.Jobs.All.ToList();
        var job = Assert.Single(jobs);
        Assert.Equal(JobKind.Dig, job.Kind);
        Assert.Equal(new Int3(5, 4, 5), job.Target);
        Assert.Equal(new[] { StepKind.GoTo, StepKind.Work, StepKind.Dig }, job.Steps.Select(s => s.Kind));
        Assert.Equal(GoalMode.Dig, job.Steps[0].Goal);
        Assert.Equal(TestContent.Db.Block(BlockId.Stone).Hardness, job.Steps[1].Ticks);
        Assert.Equal(new[] { Reservation.OnCell(new Int3(5, 4, 5)) }, job.Reservations);

        sim.RunTicks(10);
        Assert.Single(sim.Jobs.All);   // no duplicate while open

        // Dig the top by hand: the next one down becomes exposed and gets a job; the air mark is cleared.
        sim.World.SetBlock(new Int3(5, 4, 5), BlockId.Air);
        JobRunner.Cancel(sim, job);
        sim.Tick();
        Assert.Equal(DesignationMark.None, sim.Designations.Get(new Int3(5, 4, 5)));
        Assert.Equal(new Int3(5, 3, 5), Assert.Single(sim.Jobs.All).Target);
    }

    [Fact]
    public void Priority_HigherCellsFirst_Capped()
    {
        var sim = new ScenarioBuilder().Ground(12).Build();
        // A staircase of exposed cells y = 3..12: the column above each one is carved open.
        var cells = Enumerable.Range(3, 10).Select(y => new Int3(y, y, 5)).ToList();
        foreach (var c in cells)
            for (int y = c.Y + 1; y <= 12; y++) sim.World.SetBlock(new Int3(c.X, y, c.Z), BlockId.Air);
        foreach (var c in cells) Apply(sim, new DesignateDig(c, c));

        foreach (var j in sim.Jobs.All)
        {
            int bonus = Math.Min(j.Target.Y - 3, DesignationSystem.MaxHeightBonus);
            Assert.Equal(Job.DefaultPriority(JobKind.Dig) + bonus, j.Priority);
        }
        Assert.Equal(cells.Count, sim.Jobs.Count);
        Assert.True(Job.DefaultPriority(JobKind.Dig) + DesignationSystem.MaxHeightBonus < Job.DefaultPriority(JobKind.Plant));

        // Cancelling the lowest cell raises the base: priorities follow.
        Apply(sim, new CancelDesignation(cells[0], cells[0]));
        foreach (var j in sim.Jobs.All)
            Assert.Equal(Job.DefaultPriority(JobKind.Dig) + Math.Min(j.Target.Y - 4, DesignationSystem.MaxHeightBonus), j.Priority);
    }

    [Fact]
    public void DesignateChop_MarksTreesInXZRect_AnyY()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .FillBox(new Int3(12, 5, 12), new Int3(12, 7, 12), BlockId.Stone)
            .Layer(new Int3(10, 5, 10), "Tb")
            .Layer(new Int3(12, 8, 12), "T")
            .Layer(new Int3(14, 5, 10), "T")
            .Build();
        var plants = sim.Plants.All.ToList();   // tree(10,5,10), bush(11,5,10), tree(12,8,12), tree(14,5,10)
        Apply(sim, new DesignateChop(12, 13, 10, 10));   // corners in any order: x 10..12, z 10..13

        Assert.True(plants[0].MarkedForChop);
        Assert.False(plants[1].MarkedForChop);   // bushes are harvested, not chopped
        Assert.True(plants[2].MarkedForChop);
        Assert.False(plants[3].MarkedForChop);
        var chops = sim.Jobs.All.Where(j => j.Kind == JobKind.Chop).ToList();
        Assert.Equal(new[] { plants[0].Base, plants[2].Base }, chops.Select(j => j.Target));
        Assert.Equal(new[] { StepKind.GoTo, StepKind.Work, StepKind.Chop }, chops[0].Steps.Select(s => s.Kind));
        Assert.Equal(DesignationSystem.ChopTicks, chops[0].Steps[1].Ticks);
        Assert.Equal(plants[0].Id.Value, chops[0].Steps[2].Target);
    }

    [Fact]
    public void Cancel_ClearsMarksInBoxOnly()
    {
        var sim = new ScenarioBuilder().Ground(4).Layer(new Int3(10, 5, 10), "T").Build();
        var tree = sim.Plants.All.Single();
        Apply(sim, new DesignateDig(new Int3(1, 4, 1), new Int3(3, 4, 1)));
        Apply(sim, new DesignateChop(10, 10, 10, 10));
        sim.Designations.Set(new Int3(2, 3, 1), DesignationMark.DigUnreachable);
        Assert.Equal(4, sim.Jobs.Count);

        Apply(sim, new CancelDesignation(new Int3(2, 3, 1), new Int3(3, 4, 1)));
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(new Int3(1, 4, 1)));
        Assert.Equal(1, sim.Designations.Count);
        Assert.True(tree.MarkedForChop);   // base (10,5,10) is outside the box
        Assert.Equal(2, sim.Jobs.Count);

        Apply(sim, new CancelDesignation(new Int3(10, 5, 10), new Int3(10, 5, 10)));
        Assert.False(tree.MarkedForChop);
        Assert.Single(sim.Jobs.All);
    }

    [Fact]
    public void Commands_HaveStableTags()
    {
        Assert.Equal("DesignateDig", new DesignateDig(Int3.Zero, Int3.Zero).Tag);
        Assert.Equal("DesignateChop", new DesignateChop(0, 0, 0, 0).Tag);
        Assert.Equal("CancelDesignation", new CancelDesignation(Int3.Zero, Int3.Zero).Tag);
    }

    [Fact]
    public void ChopUnreachable_IsInStateHash()
    {
        var s1 = new ScenarioBuilder().Ground(4).Layer(new Int3(10, 5, 10), "T").Build();
        var s2 = new ScenarioBuilder().Ground(4).Layer(new Int3(10, 5, 10), "T").Build();
        Assert.Equal(s1.StateHash(), s2.StateHash());
        s1.Plants.All.Single().ChopUnreachable = true;
        Assert.NotEqual(s1.StateHash(), s2.StateHash());
    }
}
