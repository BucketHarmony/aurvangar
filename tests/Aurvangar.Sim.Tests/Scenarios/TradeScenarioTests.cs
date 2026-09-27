using System.Text.Json.Nodes;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Physics;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M11-T5: crafting.md scenarios 6..9 (trade half), CRF-15..22. Worlds are flat stone to y = 4 with the hall at
/// (12,5,12) unless a test says otherwise. Arrivals are forced by setting the clock to the arrival tick.</summary>
[Trait("Category", "Scenario")]
public class TradeScenarioTests
{
    private static readonly Int3 HallOrigin = new(12, G, 12);

    private static Simulation Flat(int logs = 40) => new ScenarioBuilder().Ground(G - 1).Hub(HallOrigin).Stock("log", logs)
        .Stock("water", 100).Stock("berries", 100).Agent(new Int3(8, G, 8)).Agent(new Int3(3, G, 26)).Build();   // apart, so they pay on different ticks

    private static Building? Trader(Simulation sim) => sim.Buildings.All.SingleOrDefault(b => b.Def.Id == "trader");

    private static Building ArriveNow(Simulation sim, long tick = 3000)
    {
        sim.Clock.Tick = tick;
        sim.Tick();
        return Trader(sim) ?? throw new Xunit.Sdk.XunitException("the trader did not arrive");
    }

    private static List<string> Rejections(Simulation sim, ICommand c)
    {
        sim.Events.Drain();
        sim.Enqueue(c);
        sim.Tick();
        return sim.Events.Drain().OfType<CommandRejected>().Where(r => r.Command == "AcceptOffer").Select(r => r.Reason).ToList();
    }

    private static bool NoTradeJobs(Simulation sim) => !sim.Jobs.All.Any(j => j.Kind == JobKind.Trade);

    private static bool Settled(Simulation sim) => sim.Piles.Count == 0 && sim.Agents.All.All(a => a.Carried.IsEmpty);

    [Fact]
    public void Trader_Data_And_Schedule()
    {
        var db = TestContent.Db;
        var def = db.TraderBuilding!;
        Assert.Equal("trader", def.Id);
        Assert.True(def.PrebuiltOnly);
        Assert.False(def.Storage!.Receives);
        var t = def.Trader!;
        Assert.Equal((3000, 7200, 1800), (t.FirstArrival, t.Interval, t.Stay));
        Assert.Equal(4, db.Offers.Count);
        Assert.Equal(new TradeOffer(0, db.Item("log"), 10, db.Item("stone"), 10, 4), db.Offers[0]);
        Assert.Equal(new TradeOffer(1, db.Item("potato"), 10, db.Item("stone"), 12, 2), db.Offers[1]);
        Assert.Equal(new TradeOffer(2, db.Item("stone"), 10, db.Item("log"), 10, 3), db.Offers[2]);
        Assert.Equal(new TradeOffer(3, db.Item("log"), 10, db.Item("cutstone"), 6, 2), db.Offers[3]);
        Assert.True(db.Palette.Buildings.ContainsKey("trader"));

        Assert.Equal(3000, Traders.NextArrival(t, 0));
        Assert.Equal(3000, Traders.NextArrival(t, 3000));
        Assert.Equal(10200, Traders.NextArrival(t, 3001));
        Assert.Equal(10200, Traders.NextArrival(t, 5000));
        Assert.Equal(10200, Traders.ArrivesAt(t, 1));
        Assert.Equal(12000, Traders.LeavesAt(t, 1));
        Assert.Equal(-1, Traders.VisitAt(t, 2999));
        Assert.Equal(0, Traders.VisitAt(t, 3000));
        Assert.Equal(0, Traders.VisitAt(t, 4799));
        Assert.Equal(-1, Traders.VisitAt(t, 4800));
        Assert.Equal(1, Traders.VisitAt(t, 10200));
    }

    public static TheoryData<string, string> BadTraders => new()
    {
        { "two", "already a trader" },
        { "receives", "receives: false" },
        { "stay", "stay 7200" },
        { "same", "offer 1 gives and gets the same item" },
        { "lots0", "offer 0 lots 0" },
        { "notAccepted", "offer 2 get item 'log' is not in the trader's accepts" },
    };

    [Theory]
    [MemberData(nameof(BadTraders))]
    public void Trader_BadData_Throws(string edit, string message)
    {
        var e = Assert.Throws<InvalidDataException>(() => CraftContentTests.LoadWith(root =>
        {
            var tr = CraftContentTests.Building(root, "trader");
            var offers = tr["trader"]!["offers"]!.AsArray();
            switch (edit)
            {
                case "two":
                    var copy = JsonNode.Parse(tr.ToJsonString())!.AsObject();
                    copy["id"] = "trader2";
                    root["buildings"]!.AsArray().Add(copy);
                    break;
                case "receives": tr["storage"]!["receives"] = true; break;
                case "stay": tr["trader"]!["stay"] = 7200; break;
                case "same": offers[1]!["get"] = new JsonObject { ["potato"] = 5 }; break;
                case "lots0": offers[0]!["lots"] = 0; break;
                case "notAccepted": tr["storage"]!["accepts"] = new JsonArray("stone", "cutstone"); break;
            }
        }));
        Assert.Contains("trader", e.Message);
        Assert.Contains(message, e.Message);
    }

    /// <summary>Scenario 6, CRF-17/21, on the seed-1 start world.</summary>
    [Fact]
    public void Trader_ArrivesBesideHall_LeavesOnTime_FootprintAir()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var hall = Hub(sim);
        while (sim.Clock.Tick < 3000) { sim.Tick(); sim.Events.Drain(); }
        Assert.Null(Trader(sim));
        sim.Tick();   // the tick-3000 tick
        var trader = Trader(sim);
        Assert.NotNull(trader);
        var events = sim.Events.Drain();
        Assert.Contains(events, e => e is TraderArrived a && a.Building == trader!.Id);
        Assert.Equal(BuildingState.Complete, trader!.State);
        var cells = trader.FootprintCells().ToList();
        Assert.All(cells, c => Assert.Equal(BlockId.BuildingSolid, sim.World.GetBlock(c)));
        int gap = Gap(hall, trader);
        Assert.InRange(gap, WorldFactory.MinStartGap, WorldFactory.MaxStartGap);
        Assert.Equal(0, sim.Water.GetLevel(trader.EntranceCell));
        Assert.False(sim.PathGrid.IsWet(trader.EntranceCell));
        Assert.Equal(sim.Regions.RegionOf(hall.EntranceCell), sim.Regions.RegionOf(trader.EntranceCell));
        Assert.True(Gravity.HoldsFloor(trader));
        Assert.Empty(Grounding.UnsupportedBuildings(sim));

        sim.Enqueue(new Deconstruct(trader.Id));
        sim.Tick();
        Assert.Contains(sim.Events.Drain(), e => e is CommandRejected { Command: "Deconstruct", Reason: "PrebuiltOnly" });

        while (sim.Clock.Tick < 4800) { sim.Tick(); Assert.NotNull(Trader(sim)); sim.Events.Drain(); }
        sim.Tick();   // the tick-4800 tick
        Assert.Null(Trader(sim));
        Assert.False(sim.Trader.IsHere);
        Assert.All(cells, c => Assert.Equal(BlockId.Air, sim.World.GetBlock(c)));
        Assert.Contains(sim.Events.Drain(), e => e is TraderLeft l && l.Building == trader.Id);
    }

    private static int Gap(Building a, Building b)
    {
        var ac = a.FootprintCells().ToList();
        var bc = b.FootprintCells().ToList();
        int gx = Math.Max(Math.Max(ac.Min(c => c.X) - bc.Max(c => c.X), bc.Min(c => c.X) - ac.Max(c => c.X)), 0);
        int gz = Math.Max(Math.Max(ac.Min(c => c.Z) - bc.Max(c => c.Z), bc.Min(c => c.Z) - ac.Max(c => c.Z)), 0);
        return Math.Max(gx, gz);
    }

    /// <summary>Scenario 6, CRF-18..20: two lots of logs for stone are paid by Trade jobs, granted lot by lot and
    /// unloaded to the hall before the wagon leaves.</summary>
    [Fact]
    public void StoneBoughtWithLogs()
    {
        var sim = Flat();
        var trader = ArriveNow(sim);
        Assert.Empty(Rejections(sim, new AcceptOffer(0, 2)));
        Assert.Single(sim.Trader.Deals);
        Assert.Equal(2, sim.Trader.LotsLeft[0]);

        var granted = new SortedSet<int>();
        bool sawPay = false, sawUnload = false;
        RunUntil(sim, () => sim.Trader.Deals[0].Granted == 2 && trader.Stored.Count == 0 && Stored(Hub(sim), "stone") == 20
            && Settled(sim) && NoTradeJobs(sim), 1700, () =>
        {
            granted.Add(sim.Trader.Deals[0].Granted);
            sawPay |= sim.Jobs.All.Any(j => j.IsClaimed && Traders.IsPay(j));
            sawUnload |= sim.Jobs.All.Any(j => j.IsClaimed && j.Kind == JobKind.Trade && !Traders.IsPay(j));
        });
        Assert.True(sawPay && sawUnload);
        Assert.Contains(1, granted);   // lot by lot
        Assert.True(sim.Clock.Tick < 4800);
        Assert.Equal(20, sim.Trader.Deals[0].Paid);
        Assert.Equal(20, Stored(Hub(sim), "log"));
        Assert.Equal(20, Stored(Hub(sim), "stone"));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>Scenario 7, CRF-18: the rejection reasons in their order; none adds a deal. The free-stock check counts
    /// the logs an earlier deal still owes, also while they are being carried.</summary>
    [Fact]
    public void AcceptOffer_Rejections_InOrder()
    {
        var sim = Flat(logs: 30);
        Assert.Equal(new[] { "NoTrader" }, Rejections(sim, new AcceptOffer(9, 0)));
        ArriveNow(sim);
        Assert.Equal(new[] { "BadOffer" }, Rejections(sim, new AcceptOffer(4, 0)));
        Assert.Equal(new[] { "BadOffer" }, Rejections(sim, new AcceptOffer(-1, 1)));
        Assert.Equal(new[] { "BadLots" }, Rejections(sim, new AcceptOffer(0, 0)));
        Assert.Equal(new[] { "OfferExhausted" }, Rejections(sim, new AcceptOffer(0, 5)));
        Assert.Equal(new[] { "NotEnough" }, Rejections(sim, new AcceptOffer(0, 4)));   // 40 logs, 30 in stock
        Assert.Empty(sim.Trader.Deals);

        Assert.Empty(Rejections(sim, new AcceptOffer(0, 3)));   // all 30 logs are owed now
        Assert.Equal(new[] { "OfferExhausted" }, Rejections(sim, new AcceptOffer(0, 2)));
        bool sawCarried = false;
        for (int i = 0; i < 300; i++)
        {
            Assert.Equal(new[] { "NotEnough" }, Rejections(sim, new AcceptOffer(3, 1)));   // owed while being paid
            sawCarried |= CarriedTotal(sim, "log") > 0;
        }
        Assert.True(sawCarried);
        Assert.Single(sim.Trader.Deals);
        Assert.Equal(1, sim.Trader.LotsLeft[0]);
        Assert.Equal(2, sim.Trader.LotsLeft[3]);
    }

    /// <summary>Scenario 8, CRF-21: the hall is full of stone, so bought stone stays on the wagon; logs come 5 from the
    /// hall and 15 from a far wagon, so a lot is part paid when the clock is moved to the leave tick. The departure
    /// refunds the part payment as a log pile at the entrance, drops the stone, cancels the carriers' jobs; with a
    /// warehouse placed, every pile is hauled, nothing is lost and nothing floats.</summary>
    [Fact]
    public void Departure_RefundsPartialPayment_DropsUnloadedGoods()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(new Int3(6, G, 6)).Stock("stone", 100).Stock("log", 5)
            .Stock("water", 100).Stock("berries", 100).Storage("wagon", new Int3(27, G, 27)).Stock("log", 15)
            .Agent(new Int3(4, G, 4)).Agent(new Int3(5, G, 4)).Build();
        var trader = ArriveNow(sim);
        var stand = Construction.StandCell(sim, trader);
        Assert.Empty(Rejections(sim, new AcceptOffer(0, 2)));
        var deal = sim.Trader.Deals[0];
        RunUntil(sim, () => deal.Paid == 15 && CarriedTotal(sim, "log") > 0, 1700, () => Assert.True(deal.Paid < 20));
        Assert.Equal(10, Stored(trader, "stone"));   // the hall is full: nothing unloaded
        int refund = deal.Paid - deal.Granted * 10;
        Assert.Equal(5, refund);
        int carried = CarriedTotal(sim, "log");

        sim.Events.Drain();
        sim.Clock.Tick = 4800;
        sim.Tick();
        Assert.Null(Trader(sim));
        Assert.Contains(sim.Events.Drain(), e => e is TraderLeft);
        Assert.Equal(0, CarriedTotal(sim, "log"));
        Assert.True(sim.Piles.At(stand) is { IsEmpty: false } p && p.Item == Log && p.Count >= refund);
        Assert.Equal(10, PileTotal(sim, "stone"));
        Assert.Equal(refund + carried, PileTotal(sim, "log"));
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Trade);
        int logs = Stored(Hub(sim), "log") + Stored(Site(sim, "wagon"), "log") + PileTotal(sim, "log");
        Assert.Equal(10, logs);   // 20 minus the granted lot

        var wh = sim.Buildings.PlacePrebuilt(sim.Content.Building("warehouse"), new Int3(20, G, 6), 0);
        RunUntil(sim, () => Settled(sim), 3000);
        Assert.Equal(10, Stored(wh, "stone"));
        Assert.Equal(110, Stored(Hub(sim), "stone") + Stored(wh, "stone"));
        Assert.Equal(10, Stored(Hub(sim), "log") + Stored(Site(sim, "wagon"), "log") + Stored(wh, "log"));
        Assert.Empty(Grounding.FloatingPiles(sim));
        Assert.Empty(Grounding.Floating(sim));
        Assert.Empty(Grounding.UnsupportedBuildings(sim));
    }

    /// <summary>CRF-17: with the ground around the hall walled up, the arrival is skipped with TraderNoRoom; the next
    /// visit arrives once the wall is gone.</summary>
    [Fact]
    public void NoRoom_VisitSkipped()
    {
        var entrance = new Int3(13, G, 11);
        var sim = new ScenarioBuilder().Ground(G - 1).FillBox(new Int3(0, G, 0), new Int3(31, G + 2, 31), BlockId.Stone)
            .FillBox(entrance, entrance + new Int3(0, 2, 0), BlockId.Air).Hub(HallOrigin).Stock("water", 50).Build();
        Assert.Equal(entrance, Hub(sim).EntranceCell);
        int buildings = sim.Buildings.All.Count();
        sim.Clock.Tick = 3000;
        sim.Tick();
        Assert.Contains(sim.Events.Drain(), e => e is TraderNoRoom);
        Assert.Null(Trader(sim));
        Assert.False(sim.Trader.IsHere);
        Assert.Equal(buildings, sim.Buildings.All.Count());
        sim.Tick();
        Assert.Null(Trader(sim));

        var hall = Hub(sim);
        for (int y = G; y <= G + 2; y++)
            for (int z = 0; z < 32; z++)
                for (int x = 0; x < 32; x++)
                {
                    var c = new Int3(x, y, z);
                    if (sim.Buildings.BuildingAt(c) is null) sim.World.SetBlock(c, BlockId.Air);
                }
        sim.Clock.Tick = 10199;
        sim.Tick();
        Assert.Null(Trader(sim));
        sim.Tick();   // 10200
        Assert.NotNull(Trader(sim));
        Assert.Contains(sim.Events.Drain(), e => e is TraderArrived);
        Assert.Equal(hall, Hub(sim));
    }

    /// <summary>Scenario 9, CRF-22: a game saved with a half-paid deal loads identical and stays identical through the
    /// departure; the format is v9; AcceptOffer round-trips; an empty visit hashes to nothing.</summary>
    [Fact]
    public void SaveLoad_TraderInWithHalfPaidDeal_Identical_FormatVersion9()
    {
        Assert.Equal(9, SaveGame.FormatVersion);
        var sim = Flat();
        ArriveNow(sim);
        Assert.Empty(Rejections(sim, new AcceptOffer(0, 2)));
        RunUntil(sim, () => sim.Trader.Deals[0].Paid is > 0 and < 20 && sim.Jobs.All.Any(j => j.Kind == JobKind.Trade), 1500);

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        var bytes = ms.ToArray();
        var loaded = SaveGame.Load(new MemoryStream(bytes), TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.True(loaded.Trader.IsHere);
        Assert.Equal(sim.Trader.Deals[0].Paid, loaded.Trader.Deals[0].Paid);
        while (sim.Clock.Tick <= 4900)
        {
            sim.Tick(); loaded.Tick();
            if (sim.Clock.Tick % 100 == 0) Assert.Equal(sim.StateHash(), loaded.StateHash());
        }
        Assert.False(loaded.Trader.IsHere);
        Assert.Equal(sim.StateHash(), loaded.StateHash());

        BitConverter.GetBytes(8).CopyTo(bytes, 4);
        var e = Assert.Throws<InvalidDataException>(() => SaveGame.Load(new MemoryStream(bytes), TestContent.Db));
        Assert.Contains("version 8", e.Message);

        using var cs = new MemoryStream();
        using (var w = new BinaryWriter(cs, System.Text.Encoding.UTF8, leaveOpen: true)) CommandCodec.Write(w, new AcceptOffer(2, 3));
        cs.Position = 0;
        using (var r = new BinaryReader(cs)) Assert.Equal(new AcceptOffer(2, 3), CommandCodec.Read(r));

        var h1 = StateHasher.Create();
        var h2 = StateHasher.Create();
        new TraderVisit().AddToHash(ref h2);
        Assert.Equal(h1.Value, h2.Value);
    }
}
