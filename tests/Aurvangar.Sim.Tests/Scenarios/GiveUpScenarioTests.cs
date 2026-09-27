using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Hud;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;
using static Aurvangar.Sim.Tests.Scenarios.PumpScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M7-T5 (G3 answer 7, JOB-12, VIEW-15, ADR-058): a recurring pump, haul or delivery job that can never
/// succeed gets a give-up mark, stops being reposted, shows a HUD notice, and comes back when the world changes near
/// it or a new storage is completed.</summary>
[Trait("Category", "Scenario")]
public class GiveUpScenarioTests
{
    private const int TrenchZ = 16;

    /// <summary>The M5-T4 basin world with a complete pump drawing from a full basin, and a trench two deep across
    /// the whole map at z = 16 that nobody can cross. The hub (z 20..22) and two agents (z = 24) are south of it, the
    /// pump (z = 10) north, so the pump's entrance is unreachable.</summary>
    private static Simulation TrenchWorld(bool bush = false)
    {
        var b = BasinWorld(WaterGrid.Full, agents: 0)
            .FillBox(new Int3(0, Bank - 2, TrenchZ), new Int3(31, Bank - 1, TrenchZ), BlockId.Air)
            .Agent(new Int3(10, Bank, 24)).Agent(new Int3(11, Bank, 24));
        if (bush) b.Layer(new Int3(5, Bank, 8), "b");   // a plant near the pump: restored on load (reviewer fix)
        var sim = b.Build();
        sim.Buildings.PlacePrebuilt(TestContent.Db.Building("pump"), PumpOrigin, 0);
        return sim;
    }

    private static IReadOnlyList<string> Alerts(Simulation sim) => TopBarModel.Build(sim, 1).Alerts;

    [Fact]
    public void UnreachablePumpEntrance_GivesUp_ShowsNotice_UntilBridgedNearby()
    {
        var sim = TrenchWorld();
        var pump = Pump(sim);
        int id = pump.Id.Value;

        RunUntil(sim, () => sim.GiveUps.IsGivenUp(GiveUpSource.Pump, id), 400);
        // One strike per check while no living agent's region reaches the job: checks at ticks 0, 50 and 100.
        Assert.Equal((GiveUpMarks.StrikeLimit - 1) * JobGiveUp.CheckInterval + 1, sim.Clock.Tick);
        Assert.Equal(0, sim.Counters.JobsFailed);   // never claimed, so it never failed: the region filter kept it off
        Assert.Contains(TopBarModel.UnreachablePrefix + "Water Pump", Alerts(sim));

        // Given up: no OperatePump job is posted again while nothing changes near the pump.
        sim.Tick();
        for (int i = 0; i < 600; i++)
        {
            sim.Tick();
            Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.OperatePump);
        }
        Assert.True(sim.GiveUps.IsGivenUp(GiveUpSource.Pump, id));
        Assert.Equal(0, sim.Counters.JobsFailed);

        // A walkability change far from the pump does not bring it back.
        sim.World.SetBlock(new Int3(30, Bank, 30), BlockId.Stone);
        sim.Tick();
        Assert.True(sim.GiveUps.IsGivenUp(GiveUpSource.Pump, id));

        // Bridging the trench in front of the pump (within the reset radius of its stand cell) does.
        Assert.True(TrenchZ - StandOf(sim, pump).Z <= JobGiveUp.ResetRadius);
        sim.World.SetBlock(new Int3(8, Bank - 2, TrenchZ), BlockId.Stone);
        sim.World.SetBlock(new Int3(8, Bank - 1, TrenchZ), BlockId.Stone);
        sim.Tick();
        Assert.False(sim.GiveUps.IsGivenUp(GiveUpSource.Pump, id));
        Assert.Empty(sim.GiveUps.All);
        Assert.DoesNotContain(Alerts(sim), a => a.StartsWith(TopBarModel.UnreachablePrefix));

        RunUntil(sim, () => Stored(Hub(sim), "water") >= 5, 3000);
        Assert.Empty(sim.GiveUps.All);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    private static Int3 StandOf(Simulation sim, Buildings.Building b) => Buildings.Construction.StandCell(sim, b);

    [Fact]
    public void GivenUpMark_IsSavedAndHashed()
    {
        // The bush sits within the reset radius of the pump: restoring it on load must not count as a change near it.
        var sim = TrenchWorld(bush: true);
        int id = Pump(sim).Id.Value;
        RunUntil(sim, () => sim.GiveUps.IsGivenUp(GiveUpSource.Pump, id), 400);
        sim.RunTicks(7);

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.True(loaded.GiveUps.IsGivenUp(GiveUpSource.Pump, id));

        // The mark is part of the hash: a sim without it hashes differently.
        var fresh = TrenchWorld(bush: true);
        fresh.RunTicks((int)sim.Clock.Tick);
        ulong with = fresh.StateHash();
        Assert.Equal(sim.StateHash(), with);
        fresh.GiveUps.Clear();
        Assert.NotEqual(with, fresh.StateHash());

        sim.RunTicks(300);
        loaded.RunTicks(300);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.True(loaded.GiveUps.IsGivenUp(GiveUpSource.Pump, id));
    }

    /// <summary>A far-away change that reconnects the regions (a bridge 20 cells from the pump) does not reset the
    /// mark directly, but the next region check finds the mark's cell reachable again and drops it.</summary>
    [Fact]
    public void UnreachableMark_RecoversWhenReconnectedFarAway()
    {
        var sim = TrenchWorld();
        int id = Pump(sim).Id.Value;
        RunUntil(sim, () => sim.GiveUps.IsGivenUp(GiveUpSource.Pump, id), 400);
        sim.RunTicks(20);
        var far = new Int3(28, Bank - 2, TrenchZ);
        Assert.True(far.X - StandOf(sim, Pump(sim)).X > JobGiveUp.ResetRadius);
        sim.World.SetBlock(far, BlockId.Stone);
        sim.World.SetBlock(far + Int3.Up, BlockId.Stone);
        sim.Tick();
        Assert.True(sim.GiveUps.IsGivenUp(GiveUpSource.Pump, id));   // not near: no reset yet
        RunUntil(sim, () => !sim.GiveUps.IsGivenUp(GiveUpSource.Pump, id), JobGiveUp.CheckInterval + 1);
        RunUntil(sim, () => Stored(Hub(sim), "water") >= 5, 4000);
        Assert.Empty(sim.GiveUps.All);
    }

    /// <summary>A log pile at the bottom of the trench: no haul can reach it, so its haul is given up.</summary>
    [Fact]
    public void UnreachablePile_HaulGivenUp()
    {
        var sim = TrenchWorld();
        var pile = new Int3(3, Bank - 2, TrenchZ);
        sim.Piles.Add(pile, TestContent.Db.Item("log"), 6);
        int key = sim.World.Index(pile);

        RunUntil(sim, () => sim.GiveUps.IsGivenUp(GiveUpSource.Pile, key), 400);
        sim.RunTicks(200);
        Assert.DoesNotContain(sim.Jobs.All, j => HaulSystem.IsPileHaul(j));
        Assert.Contains(TopBarModel.UnreachablePrefix + "Water Pump, log pile", Alerts(sim));
        Assert.Equal(6, sim.Piles.At(pile).Count);

        // The pile goes away (here: removed by hand), and so does its mark.
        sim.Piles.Take(pile, 6);
        sim.Tick();
        Assert.False(sim.GiveUps.IsGivenUp(GiveUpSource.Pile, key));
    }

    /// <summary>Reachable but failing every time: each delivery for the warehouse site finds the hub's logs gone when
    /// the dwarf gets there (the test takes them while the job is claimed). Five failures cancel a job (JOB-08) and
    /// count one strike; the third strike gives the site up. A newly completed storage resets every mark.</summary>
    [Fact]
    public void RepeatedlyFailingDelivery_GivenUp_NewStorageResets()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(new Int3(20, G, 20)).Stock("log", 40)
            .Agent(new Int3(15, G, 15)).Build();
        sim.Enqueue(new PlaceBuilding("warehouse", WhOrigin, 0));
        sim.Tick();
        var site = Site(sim);
        var hub = Hub(sim);
        int logId = TestContent.Db.Item("log").Value;
        bool sabotage = true;
        void Interfere()
        {
            if (!sabotage) return;
            bool claimed = sim.Jobs.All.Any(j => j.Kind == JobKind.Deliver && j.IsClaimed);
            hub.Stored[logId] = claimed ? 0 : 40;
        }

        RunUntil(sim, () => sim.GiveUps.IsGivenUp(GiveUpSource.Site, site.Id.Value), 6000, Interfere);
        // Three cancellations of five failures each; the site's other load (20 logs = two jobs) fails in between, at
        // most MaxFailures - 1 times without being cancelled.
        Assert.InRange(sim.Counters.JobsFailed, GiveUpMarks.StrikeLimit * Job.MaxFailures,
            GiveUpMarks.StrikeLimit * Job.MaxFailures + Job.MaxFailures - 1);
        Assert.Contains(TopBarModel.UnreachablePrefix + "Warehouse site", Alerts(sim));

        // Given up: the stock stays in the hub and no delivery is posted (an in-flight one may still fail once).
        long failed = sim.Counters.JobsFailed;
        sabotage = false;
        hub.Stored[logId] = 40;
        sim.RunTicks(100);
        for (int i = 0; i < 500; i++)
        {
            sim.Tick();
            Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Deliver);
        }
        Assert.InRange(sim.Counters.JobsFailed, failed, failed + 1);
        Assert.Equal(0, Delivered(site, "log"));

        // A second warehouse far away is built from the hub; its completion resets the mark.
        var farOrigin = new Int3(26, G, 4);
        Assert.True(Math.Abs(farOrigin.X - WhEntrance.X) > JobGiveUp.ResetRadius);
        hub.Stored[logId] = 60;
        sim.Enqueue(new PlaceBuilding("warehouse", farOrigin, 0));
        sim.Tick();
        var second = sim.Buildings.All.Single(b => b.Def.Id == "warehouse" && b.Origin == farOrigin);
        RunUntil(sim, () => second.State == Buildings.BuildingState.Complete, 4000,
            () => Assert.True(second.State == Buildings.BuildingState.Complete || sim.GiveUps.IsGivenUp(GiveUpSource.Site, site.Id.Value)));
        sim.Tick();
        Assert.False(sim.GiveUps.IsGivenUp(GiveUpSource.Site, site.Id.Value));
        RunUntil(sim, () => site.State == Buildings.BuildingState.Complete, 4000);
        Assert.Empty(sim.GiveUps.All);
    }

    /// <summary>A strike is forgotten when a job from the same source completes ("without ever succeeding").</summary>
    [Fact]
    public void Success_ClearsStrikes()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(new Int3(20, G, 20)).Stock("log", 40)
            .Agent(new Int3(15, G, 15)).Build();
        sim.Enqueue(new PlaceBuilding("warehouse", WhOrigin, 0));
        sim.Tick();
        var site = Site(sim);
        sim.GiveUps.Strike(GiveUpSource.Site, site.Id.Value, WhEntrance);
        sim.GiveUps.Strike(GiveUpSource.Site, site.Id.Value, WhEntrance);
        Assert.Equal(2, sim.GiveUps.StrikesOf(GiveUpSource.Site, site.Id.Value));
        Assert.False(sim.GiveUps.IsGivenUp(GiveUpSource.Site, site.Id.Value));

        RunUntil(sim, () => sim.Counters.JobsCompleted >= 1, 1000);
        Assert.Equal(0, sim.GiveUps.StrikesOf(GiveUpSource.Site, site.Id.Value));
    }
}
