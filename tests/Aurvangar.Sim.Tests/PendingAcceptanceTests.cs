using Aurvangar.Sim.Tests.Support;
using Xunit;

// Acceptance tests for M11-T5..T7 (economy and crafting, docs/specs/crafting.md, ADR-082). Each test is specified by
// its name and its Placeholder text. To start a task, move its tests into their own file, write the bodies, then
// un-skip. A placeholder that is un-skipped without a body fails, by design. Delete this file when it is empty.

namespace Aurvangar.Sim.Tests
{
}

namespace Aurvangar.Sim.Tests.Scenarios
{
    [Trait("Category", "Scenario")]
    public class TradeScenarioTests
    {
        [Fact(Skip = "M11-T5")] public void Trader_Data_And_Schedule() => Placeholder.Write("CRF-15/16: the trader def loads with 4 offers and firstArrival 3000, interval 7200, stay 1800; bad trader data (two traders, receives true, stay >= interval, same give and get item, lots 0, a get item not accepted) throws naming the offer; Traders.NextArrival(0) == 3000, (5000) == 10200, LeavesAt for visit 1 == 12000");
        [Fact(Skip = "M11-T5")] public void Trader_ArrivesBesideHall_LeavesOnTime_FootprintAir() => Placeholder.Write("crafting.md scenario 6, CRF-17/21: seed 1: at tick 3000 a complete trader building stands at a BLD-15 site beside the hall (dry, entrance reachable from the hall's), TraderArrived is emitted; at 4800 it is gone, its footprint is Air, TraderLeft is emitted; it cannot be deconstructed (PrebuiltOnly) and its floor cannot be dug");
        [Fact(Skip = "M11-T5")] public void StoneBoughtWithLogs() => Placeholder.Write("crafting.md scenario 6, CRF-18..20 (M11-T5 backlog): AcceptOffer(0, 2) during the visit: 20 logs are carried onto the wagon by Trade jobs, 20 stone appear in its storage lot by lot and are unloaded to the hall before it leaves; colony log -20 and stone +20; no job fails");
        [Fact(Skip = "M11-T5")] public void AcceptOffer_Rejections_InOrder() => Placeholder.Write("crafting.md scenario 7, CRF-18: NoTrader before 3000, BadOffer (index 4), BadLots (0), OfferExhausted (5 lots of offer 0, and 1 more after 4 are taken), NotEnough (more logs than free stock, counting logs owed by an earlier deal); each emits CommandRejected and adds no deal");
        [Fact(Skip = "M11-T5")] public void Departure_RefundsPartialPayment_DropsUnloadedGoods() => Placeholder.Write("crafting.md scenario 8, CRF-21: a deal half paid at the leave tick refunds the paid units of the ungranted lot as a log pile at the entrance; bought stone not yet unloaded drops as a pile; carrying dwarves' jobs are cancelled and their items dropped; all piles are hauled and nothing floats");
        [Fact(Skip = "M11-T5")] public void NoRoom_VisitSkipped() => Placeholder.Write("CRF-17: with every BLD-15 site around the hall blocked, the arrival tick emits TraderNoRoom and places nothing; the next visit arrives when a site is free again");
        [Fact(Skip = "M11-T5")] public void SaveLoad_TraderInWithHalfPaidDeal_Identical_FormatVersion9() => Placeholder.Write("crafting.md scenario 9, CRF-22: save with a trader in and a deal half paid; the loaded game matches the hash every 100 ticks through the departure; SaveGame.FormatVersion == 9 and a v8 file is refused; AcceptOffer round-trips through CommandCodec; with no trader in, the Traders section adds nothing to StateHash");
    }

    [Trait("Category", "Scenario")]
    public class EconomyScenarioTests
    {
        [Fact(Skip = "M11-T7")] public void Seed1_Economy_HallOfRefinedBlocksByDay10_AllAlive() => Placeholder.Write("crafting.md scenario 10, M11-T7: EconomyScript (seed 1) builds a Sawmill and a Stonecutter, sets Keep orders for planks and cutstone, accepts a stone-for-logs offer at the first trader visit, and releases a small hall of Wood planks, Polished stone and Slate blocks; by tick 24000 every entry is built, both Keep orders were met at least once, at least one deal was fully granted, and all 5 dwarves are alive");
        [Fact(Skip = "M11-T7")] public void Seed1_Economy_NoDwarfWalledIn_NothingFloats() => Placeholder.Write("M11-T7, CON-09/CON-14/GRV: checked every 100 ticks: every living dwarf not fleeing is in the hall's region, every built block is grounded, no pile or building floats");
        [Fact(Skip = "M11-T7")] public void EconomyScript_AvailableToHeadlessAndScreenshots() => Placeholder.Write("M11-T7: ScreenshotScripts.Names contains 'economy', it is timed like 'survival', and a harness run gives the same hash as EconomyScript.Run at tick 6000; the headless runner accepts --script economy");
    }
}

namespace Aurvangar.Sim.Tests.View
{
    [Trait("Category", "Unit")]
    public class WorkshopPanelTests
    {
        [Fact(Skip = "M11-T6")] public void Panel_ShowsStatusAndRecipes_SendsOrders() => Placeholder.Write("VIEW-28: WorkshopPanelModel for a sawmill lists 'Saw planks: 1 log -> 2 planks', the status text ('Sawmill: needs log' for NoInput), Make and Keep toggles, count +/- (Shift 5) and done/count for Make; each change sends one SetWorkshopOrder and the sim applies it; clearing sends count 0");
    }

    [Trait("Category", "Unit")]
    public class TradePanelTests
    {
        [Fact(Skip = "M11-T6")] public void Panel_OffersStockAndAccept_DisabledWithReason() => Placeholder.Write("VIEW-29: with a trader in, TradePanelModel shows 'Trade wagon: leaves in 3h', one row per offer ('10 log -> 10 stone', lots left, free stock of log), Accept 1 / Accept all send AcceptOffer and the sim applies them; a row the sim would reject is disabled with the CRF-18 reason; with no trader the button tooltip gives the next arrival ('Trade wagon in 1 day 4h')");
        [Fact(Skip = "M11-T6")] public void Hud_TotalsIncludeRefinedItems_WorkshopAlerts() => Placeholder.Write("VIEW-30: the top bar lists every item in data order with Planks and Cut stone after Water; NoInput and OutputFull workshops join the alerts; HudLayout still never overlaps the toolbar with the two extra items");
    }
}

namespace Aurvangar.Sim.Tests.Perf
{
    [Trait("Category", "Perf")]
    [Collection(PerfCollection.Name)]
    public class EconomyPerfTests
    {
        [Fact(Skip = "M11-T7")] public void SimP1_EconomyWithTraderIn() => Placeholder.Write("CRF-P1: seed 1 with EconomyScript, 500 Tick() calls while a trader is in and both workshops work: median <= 8 ms (x PERF_SCALE); print the Workshops and Traders totals");
    }
}
