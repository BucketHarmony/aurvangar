using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>M6-T3: berry bushes (ECO-10, JOB-11). Stone ground with its top at y=4, a hub at (2,5,20) and one bush
/// at (20,5,12). Harvest jobs are posted automatically while the food (berries + potatoes) in complete storage is
/// below 60 units (ADR-048).</summary>
public class BushTests
{
    internal static readonly Int3 BushCell = new(20, 5, 12);

    internal static Simulation BushWorld(int berries, int potatoes = 0, bool agent = true)
    {
        var b = new ScenarioBuilder(32, 32, 32).Ground(4)
            .Layer(BushCell, "b")
            .Hub(new Int3(2, 5, 20)).Stock("water", 30).Stock("berries", berries);
        if (potatoes > 0) b.Stock("potato", potatoes);
        if (agent) b.Agent(new Int3(16, 5, 16));
        return b.Build();
    }

    private static Plant Bush(Simulation sim) => sim.Plants.All.Single(p => p.Kind == PlantKind.Bush);

    private static int HubBerries(Simulation sim) =>
        sim.Buildings.All.First().Stored.GetValueOrDefault(TestContent.Db.Item("berries").Value);

    private static bool HasBushJob(Simulation sim) =>
        sim.Jobs.All.Any(j => j.Kind == JobKind.Harvest && j.Target == BushCell);

    /// <summary>ECO-10, JOB-11: a ripe bush gives 2 berries, which the harvester carries straight to storage in the
    /// same job. The bush is then Growing for exactly 1200 ticks and ripe (2 berries) again after them.</summary>
    [Fact]
    public void Harvest_Gives2Berries_RegrowsIn1200()
    {
        var sim = BushWorld(berries: 30);
        Assert.Equal(PlantSystem.BushBerries, Bush(sim).Berries);

        int t = 0;
        for (; t < 400 && Bush(sim).Berries > 0; t++) sim.Tick();
        Assert.Equal(0, Bush(sim).Berries);
        Assert.Equal(PlantSystem.BushRegrowTicks, Bush(sim).RegrowTicks);
        var a = sim.Agents.All.First();
        Assert.Equal(TestContent.Db.Item("berries"), a.Carried.Item);
        Assert.Equal(PlantSystem.BushBerries, a.Carried.Count);

        int since = 0;
        for (; since < 400 && HubBerries(sim) == 30; since++)
        {
            sim.Tick();
            Assert.Equal(0, sim.Piles.Count);   // never dropped: delivered in the harvest job itself
            Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Haul);
        }
        Assert.Equal(30 + PlantSystem.BushBerries, HubBerries(sim));
        Assert.Equal(0, sim.Counters.JobsFailed);

        // Growing: no harvest job while the bush has no berries.
        sim.RunTicks(PlantSystem.BushRegrowTicks - 1 - since);
        Assert.Equal(0, Bush(sim).Berries);
        Assert.Equal(1, Bush(sim).RegrowTicks);
        Assert.False(HasBushJob(sim));

        sim.Tick();
        Assert.Equal(PlantSystem.BushBerries, Bush(sim).Berries);
        Assert.Equal(0, Bush(sim).RegrowTicks);
        Assert.True(HasBushJob(sim), "a ripe bush gets a new harvest job while food is below 60");
    }

    /// <summary>ECO-10: no harvest job is posted while the colony's food in storage (berries + potatoes, in units)
    /// is at least 60; one is posted as soon as it drops below 60.</summary>
    [Fact]
    public void HarvestNotPosted_WhenFoodStockAtLeast60()
    {
        var sim = BushWorld(berries: 40, potatoes: 20);
        Assert.Equal(60, BushHarvest.FoodInStorage(sim));
        for (int i = 0; i < 300; i++)
        {
            sim.Tick();
            Assert.False(HasBushJob(sim));
        }
        Assert.Equal(PlantSystem.BushBerries, Bush(sim).Berries);
        Assert.True(sim.Agents.All.First().Carried.IsEmpty);

        sim.Buildings.All.First().Stored[TestContent.Db.Item("potato").Value] = 19;   // 59
        sim.Tick();
        Assert.True(HasBushJob(sim));
    }

    /// <summary>ECO-10: an unclaimed harvest job is withdrawn once food in storage reaches 60 again, and a
    /// storage that is not complete does not count.</summary>
    [Fact]
    public void UnclaimedHarvest_Withdrawn_WhenFoodReaches60()
    {
        var sim = BushWorld(berries: 59, agent: false);
        sim.Tick();
        var job = Assert.Single(sim.Jobs.All, j => j.Kind == JobKind.Harvest);
        Assert.Equal(BushCell, job.Target);
        Assert.Equal(new[] { StepKind.GoTo, StepKind.Work, StepKind.HarvestBush }, job.Steps.Select(s => s.Kind));
        Assert.Equal(JobKind.Harvest, job.Kind);
        Assert.Equal(Job.DefaultPriority(JobKind.Harvest), job.Priority);

        sim.Buildings.All.First().Stored[TestContent.Db.Item("berries").Value] = 60;
        sim.Tick();
        Assert.False(HasBushJob(sim));

        sim.Buildings.All.First().Stored[TestContent.Db.Item("berries").Value] = 10;
        sim.Tick();
        Assert.True(HasBushJob(sim));
        sim.RunTicks(50);
        Assert.Single(sim.Jobs.All, j => j.Kind == JobKind.Harvest);   // one job per ripe bush, not reposted
    }

    /// <summary>ECO-10: WorldActions.HarvestBush only takes a ripe bush in reach.</summary>
    [Fact]
    public void HarvestBush_Action_Rules()
    {
        var sim = BushWorld(berries: 100);
        var a = sim.Agents.All.First();
        var bush = Bush(sim);
        Assert.Equal(Actions.ActionResult.OutOfReach, sim.Actions.HarvestBush(a.Id, bush.Id));

        var near = sim.Agents.Spawn(BushCell + new Int3(1, 0, 0), "Near");
        Assert.Equal(Actions.ActionResult.Ok, sim.Actions.HarvestBush(near.Id, bush.Id));
        Assert.Equal(PlantSystem.BushBerries, near.Carried.Count);
        Assert.Equal(0, bush.Berries);
        Assert.Equal(PlantSystem.BushRegrowTicks, bush.RegrowTicks);
        Assert.Equal(Actions.ActionResult.InvalidTarget, sim.Actions.HarvestBush(near.Id, bush.Id));   // growing

        var tree = sim.Plants.AddTree(BushCell + new Int3(0, 0, 2));
        Assert.Equal(Actions.ActionResult.InvalidTarget, sim.Actions.HarvestBush(near.Id, tree.Id));
    }

    /// <summary>SAV-03, ARCH-06: a load mid-regrowth (with a bush job on the board) hashes equal and stays equal.</summary>
    [Fact]
    public void SaveLoad_MidRegrow_HashEqual()
    {
        var sim = BushWorld(berries: 30);
        for (int i = 0; i < 400 && Bush(sim).Berries > 0; i++) sim.Tick();
        sim.RunTicks(3);   // carrying the berries home

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        for (int i = 0; i < 15; i++)
        {
            sim.RunTicks(100);
            loaded.RunTicks(100);
            Assert.Equal(sim.StateHash(), loaded.StateHash());
        }
        Assert.Equal(30 + 2 * PlantSystem.BushBerries, HubBerries(loaded));   // regrown and harvested again
    }

    /// <summary>ARCH-06: bush berries and regrowth are hashed.</summary>
    [Fact]
    public void BushState_IsHashed()
    {
        var sim = BushWorld(berries: 100);
        ulong h = sim.StateHash();
        Bush(sim).RegrowTicks = 5;
        Assert.NotEqual(h, sim.StateHash());
    }
}
