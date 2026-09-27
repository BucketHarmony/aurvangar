using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Screenshots;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M11-T6: the trade panel model (VIEW-29) and the HUD's refined items and workshop alerts (VIEW-30).</summary>
[Trait("Category", "Unit")]
public class TradePanelTests
{
    private const int G = 5;

    private static Simulation Flat(int logs = 40) => new ScenarioBuilder().Ground(G - 1).Hub(new Int3(12, G, 12))
        .Stock("log", logs).Stock("water", 100).Stock("berries", 100).Build();

    private static List<string> Rejections(Simulation sim, ICommand c)
    {
        sim.Events.Drain();
        sim.Enqueue(c);
        sim.Tick();
        return sim.Events.Drain().OfType<CommandRejected>().Select(r => r.Reason).ToList();
    }

    [Fact]
    public void Panel_OffersStockAndAccept_DisabledWithReason()
    {
        var sim = Flat();
        sim.Clock.Tick = 200;
        Assert.Null(TradePanelModel.Build(sim));
        Assert.Equal("Trade wagon in 1 day 4h", TradePanelModel.ButtonTooltip(sim));   // 2800 ticks

        sim.Clock.Tick = 3000;
        sim.Tick();
        Assert.True(sim.Trader.IsHere);
        sim.Clock.Tick = 4500;   // leaves at 4800
        Assert.Equal("Trade wagon: leaves in 3h", TradePanelModel.ButtonTooltip(sim));
        var panel = TradePanelModel.Build(sim)!;
        Assert.Equal("Trade wagon: leaves in 3h", panel.Title);
        Assert.Equal(4, panel.Offers.Count);
        Assert.Empty(panel.Deals);

        var logs = panel.Offers[0];
        Assert.Equal(("10 log -> 10 stone", 4, "4 lots left", 40, "have 40 log"), (logs.Text, logs.LotsLeft, logs.LotsText, logs.Have, logs.HaveText));
        Assert.True(logs.AcceptOne.Enabled);
        Assert.Equal(1, logs.AcceptOne.Lots);
        Assert.Equal(4, logs.AcceptAll.Lots);

        // No potatoes: both buttons disabled with the CRF-18 reason; the sim refuses the same command the same way.
        var potato = panel.Offers[1];
        Assert.Equal("10 potato -> 12 stone", potato.Text);
        Assert.Equal(0, potato.Have);
        Assert.False(potato.AcceptOne.Enabled);
        Assert.Equal(("NotEnough", "Not enough potato: 10 needed, 0 free"), (potato.AcceptOne.Reason, potato.AcceptOne.Tooltip));
        Assert.Equal((2, "NotEnough"), (potato.AcceptAll.Lots, potato.AcceptAll.Reason));
        Assert.Null(potato.AcceptOne.Command(1));
        Assert.Equal(new[] { "NotEnough" }, Rejections(sim, new AcceptOffer(1, potato.AcceptOne.Lots)));

        // Accept 1 of the logs offer: the sim applies it, and the deal shows.
        sim.Clock.Tick = 3002;
        Assert.Equal(new AcceptOffer(0, 1), TradePanelModel.Build(sim)!.Offers[0].AcceptOne.Command(0));
        Assert.Empty(Rejections(sim, TradePanelModel.Build(sim)!.Offers[0].AcceptOne.Command(0)!));
        panel = TradePanelModel.Build(sim)!;
        Assert.Equal(3, panel.Offers[0].LotsLeft);
        Assert.Equal(30, panel.Offers[0].Have);   // free stock: the unfetched 10 are held back (CRF-18)
        var deal = Assert.Single(panel.Deals);
        Assert.Matches(@"^1 x 10 log -> 10 stone: paid \d+/10, granted [01]/1$", deal.Text);

        // Accept all takes the 3 lots left; then the offer is exhausted and the logs are all promised.
        Assert.Equal(3, panel.Offers[0].AcceptAll.Lots);
        Assert.Empty(Rejections(sim, panel.Offers[0].AcceptAll.Command(0)!));
        panel = TradePanelModel.Build(sim)!;
        Assert.Equal(0, panel.Offers[0].LotsLeft);
        Assert.Equal(("OfferExhausted", "No lots left"), (panel.Offers[0].AcceptOne.Reason, panel.Offers[0].AcceptOne.Tooltip));
        Assert.Equal(new[] { "OfferExhausted" }, Rejections(sim, new AcceptOffer(0, 1)));
        var cut = TradePanelModel.Build(sim)!.Offers[3];
        Assert.Equal("10 log -> 6 cut stone", cut.Text);
        Assert.Equal("NotEnough", cut.AcceptOne.Reason);
        Assert.Equal(new[] { "NotEnough" }, Rejections(sim, new AcceptOffer(3, 1)));
        Assert.Equal(2, TradePanelModel.Build(sim)!.Deals.Count);

        // Accept all takes only what the colony can pay for.
        var sim2 = Flat(logs: 25);
        sim2.Clock.Tick = 3000;
        sim2.Tick();
        var two = TradePanelModel.Build(sim2)!.Offers[0].AcceptAll;
        Assert.Equal((2, true), (two.Lots, two.Enabled));

        // After the visit the panel is gone and the tooltip gives the next arrival (10200).
        sim.Clock.Tick = 4800;
        sim.Tick();
        Assert.False(sim.Trader.IsHere);
        Assert.Null(TradePanelModel.Build(sim));
        Assert.Equal("Trade wagon in 2 days 6h", TradePanelModel.ButtonTooltip(sim));   // 10200 - 4801 -> 54h

        Assert.Equal(new[] { "0h", "1h", "2h", "1 day", "1 day 4h", "2 days" },
            new long[] { 0, 100, 101, 2400, 2800, 4800 }.Select(TradePanelModel.Duration));
    }

    [Fact]
    public void Hud_TotalsIncludeRefinedItems_WorkshopAlerts()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(new Int3(20, G, 20)).Stock("planks", 7).Stock("cutstone", 3).Build();
        var bar = TopBarModel.Build(sim, 1);
        Assert.Equal(new[] { "Log", "Stone", "Berries", "Potato", "Water", "Planks", "Cut stone" }, bar.Totals.Select(t => t.Name));
        Assert.Equal((7, 3), (bar.Totals[5].Count, bar.Totals[6].Count));
        Assert.EndsWith("Planks 7   Cut stone 3", bar.TotalsText);

        // A sawmill with an order and no logs: NoInput joins the alerts. A blueprint with orders does not.
        var mill = sim.Buildings.PlacePrebuilt(sim.Content.Building("sawmill"), new Int3(10, G, 10), 0);
        Assert.DoesNotContain(TopBarModel.Build(sim, 1).Alerts, a => a.StartsWith("Sawmill"));
        sim.Enqueue(new SetWorkshopOrder(mill.Id, 0, OrderMode.Keep, 20));
        sim.Enqueue(new PlaceBuilding("stonecutter", new Int3(4, G, 4), 0));
        sim.Tick();
        var cutter = sim.Buildings.All.Single(b => b.Def.Id == "stonecutter");
        sim.Enqueue(new SetWorkshopOrder(cutter.Id, 0, OrderMode.Keep, 20));
        sim.Tick();
        Assert.Contains("Sawmill: needs log", TopBarModel.Build(sim, 1).Alerts);
        Assert.DoesNotContain(TopBarModel.Build(sim, 1).Alerts, a => a.StartsWith("Stonecutter"));

        // OutputFull: a full buffer and no storage that takes planks.
        var lone = new ScenarioBuilder().Ground(G - 1).Build();
        var m2 = lone.Buildings.PlacePrebuilt(lone.Content.Building("sawmill"), new Int3(10, G, 10), 0);
        m2.Stored[lone.Content.Item("planks").Value] = m2.Def.Workshop!.OutputBuffer;
        lone.Enqueue(new SetWorkshopOrder(m2.Id, 0, OrderMode.Make, 10));
        lone.Tick();
        Assert.Equal(WorkshopStatus.OutputFull, Workshops.StatusOf(lone, m2).Status);
        Assert.Contains("Sawmill: output full (planks)", TopBarModel.Build(lone, 1).Alerts);

        // HudLayout: the totals with the two new items (16 px font, ~8.5 px a character) and the wider toolbar (the
        // Trade button) still never overlap the toolbar at 1600 or 1280 px wide.
        var toolbar = new ScreenRect(8, 8, 874, 32);
        string totals = "Log 40   Stone 60   Berries 100   Potato 0   Water 100   Planks 20   Cut stone 20";
        var row = new[] { 52f, 180f, 30f, totals.Length * 8.5f, 150f }.Select(w => new FlowItem(0, w)).ToList();
        foreach (float width in new[] { 1600f, 1280f })
        {
            var arranged = HudLayout.ArrangeTopBar(width, toolbar, 16f, row, Array.Empty<FlowItem>());
            bool clear = arranged.Rect.X >= toolbar.Right + HudLayout.Gap - 0.01f || arranged.Rect.Y >= toolbar.Bottom;
            Assert.True(clear, $"top bar {arranged.Rect} overlaps the toolbar at {width}");
            Assert.True(arranged.Rect.Right <= width);
        }
    }

    [Fact]
    public void WorkshopScript_PlacesWorkshops_OrdersAndATrade()
    {
        Assert.Contains("workshop", ScreenshotScripts.Names);
        Assert.Contains("workshop", ScreenshotPresets.Names);
        Assert.True(ScreenshotScripts.IsTimed("workshop"));
        Assert.Equal("trade", ScreenshotArgs.Parse(new[] { "--script", "workshop", "--panel", "trade" }).Panel);
        Assert.Throws<ArgumentException>(() => ScreenshotArgs.Parse(new[] { "--panel", "nope" }));

        var sim = WorldFactory().Invoke();
        ScreenshotScripts.Run("workshop", sim, 3200);
        var mill = Aurvangar.ViewCore.Scripts.WorkshopScript.First(sim, "sawmill");
        var cutter = Aurvangar.ViewCore.Scripts.WorkshopScript.First(sim, "stonecutter");
        Assert.NotNull(mill);
        Assert.NotNull(cutter);
        Assert.Equal(BuildingState.Complete, mill!.State);
        Assert.Equal(BuildingState.Complete, cutter!.State);
        Assert.True(sim.Trader.IsHere);
        Assert.Single(sim.Trader.Deals);
        Assert.True(Economy.Stock(sim, sim.Content.Item("planks")) > 20, "the sawmill made planks");
        Assert.NotNull(TradePanelModel.Build(sim));
        var shot = ScreenshotPresets.For("workshop", sim);
        Assert.Equal("workshop", shot.Name);
    }

    private static Func<Simulation> WorldFactory() => () => Aurvangar.Sim.World.WorldFactory.Create(1, TestContent.Db);
}
