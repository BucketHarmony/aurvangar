using Aurvangar.Sim.Tests.Support;
using Xunit;

// Acceptance tests for M11-T4..T7 (economy and crafting, docs/specs/crafting.md, ADR-082). Each test is specified by
// its name and its Placeholder text. To start a task, move its tests into their own file, write the bodies, then
// un-skip. A placeholder that is un-skipped without a body fails, by design. Delete this file when it is empty.

namespace Aurvangar.Sim.Tests
{
    [Trait("Category", "Unit")]
    public class CraftContentTests
    {
        [Fact(Skip = "M11-T4")] public void RefinedItems_AppendedAfterWater_StoredInHallWarehouseWagon() => Placeholder.Write("CRF-01: items.json gains planks (ItemId 6) and cutstone (7) after water, so ids 1..5 keep their values; each has a palette colour; hub and warehouse accept both; the wagon accepts both and its startStock is 40 log, 60 stone, 20 planks, 20 cutstone");
        [Fact(Skip = "M11-T4")] public void RefinedBlocks_CostRefinedItems_RoughKeepRaw() => Placeholder.Write("CRF-02: CostOf(Planks) = (planks, 1), PolishedStone (cutstone, 2), Slate (cutstone, 3); Masonry and Rubble stay (stone, 1), Beam (log, 2); CON-20 shaped costs follow (Slate slab 2, stair 3, pillar 1)");
        [Fact(Skip = "M11-T4")] public void Workshops_LoadWithRecipes() => Placeholder.Write("CRF-03..05: sawmill and stonecutter load with Workshop set (outputBuffer 20, haulAt 10, 1 worker, entrance [0,0,-1], 2x2x2); ContentDb.Recipe(\"planks\") is 1 log -> 2 planks at 40 work ticks in the sawmill, \"cutstone\" 1 stone -> 1 cutstone at 50 in the stonecutter; stonecutter costs 8 log + 8 stone");
        [Fact(Skip = "M11-T4")] public void Workshop_BadData_ThrowsNamingBuildingAndRecipe() => Placeholder.Write("CRF-04: each throws naming the file, building and recipe: workers 0 or 2, no entrance, a storage, a producer, waterEdge, no recipes, outputBuffer 0, haulAt 0 or > outputBuffer, two input items, an unknown output, input count 11, output count > outputBuffer, workTicks 0, a recipe id used twice across workshops");
    }

    [Trait("Category", "Unit")]
    public class WorkshopOrderTests
    {
        [Fact(Skip = "M11-T4")] public void SetWorkshopOrder_SetsReplacesRemoves_InRecipeOrder() => Placeholder.Write("CRF-06/07: on a two-recipe test workshop, setting recipe 1 then recipe 0 keeps the orders in recipe order; replacing an order resets done to 0; count 0 removes; orders may be set on a blueprint");
        [Fact(Skip = "M11-T4")] public void SetWorkshopOrder_Rejections_InOrder() => Placeholder.Write("CRF-07: NoSuchBuilding, NotAWorkshop (a warehouse), BadRecipe (index 1 on the sawmill), BadMode (byte 9), BadCount (-1 and 1000), NothingToRemove (count 0 with no order); each emits CommandRejected with that reason and changes nothing");
        [Fact(Skip = "M11-T4")] public void ColonyStock_CountsStorageWorkshopOutputAndCarried_NotPiles() => Placeholder.Write("CRF-08: Economy.Stock(planks) sums complete storages (a blueprint warehouse does not count), the sawmill's Stored output and a dwarf's carried planks, and ignores a loose planks pile");
        [Fact(Skip = "M11-T4")] public void ActiveOrder_AndCycleCount() => Placeholder.Write("CRF-09: Make 20 planks with 30 logs stored -> k = min(10, 10, 10, 30) = 10; Keep 10 planks with 6 in stock -> k = 2; with 3 logs unreserved k = 3 capped by input; with 18 planks in the output buffer k = 1; an order with no input anywhere is skipped for the next one in recipe order");
        [Fact(Skip = "M11-T4")] public void WorkshopOrders_SavedAndHashed_FormatVersion8() => Placeholder.Write("CRF-22: an order changes StateHash (mode, count and done each count); a building without a workshop block hashes as before; save/load keeps orders and hash equal; SaveGame.FormatVersion == 8 and a v7 file is refused (SAV-04); SetWorkshopOrder round-trips through CommandCodec, a rejected one replays as rejected");
    }
}

namespace Aurvangar.Sim.Tests.Scenarios
{
    [Trait("Category", "Scenario")]
    public class WorkshopScenarioTests
    {
        [Fact(Skip = "M11-T4")] public void Sawmill_Make20_Exactly20PlanksToStorage_OrderRemoved() => Placeholder.Write("crafting.md scenario 1, CRF-10..12: a complete sawmill with Make 20 planks and logs in the hall: exactly 20 planks are made, all end in storage via Unload jobs, 10 logs are taken, the order is removed with WorkshopOrderDone, and no job fails");
        [Fact(Skip = "M11-T4")] public void Stonecutter_Keep10_RefillsAfterUse() => Placeholder.Write("crafting.md scenario 2, CRF-08/09: Keep 10 cutstone crafts until Stock(cutstone) == 10 (at most one batch over), status Done; building Polished stone blocks uses some and crafting starts again until 10 are back");
        [Fact(Skip = "M11-T4")] public void NoInput_StatusAndNoJob_ThenStarts() => Placeholder.Write("crafting.md scenario 3, CRF-13: a stonecutter with an order and no stone anywhere reads NoInput (naming stone) and posts no Craft job; putting stone in the hall starts it; a sawmill with no orders reads NoOrders and a blueprint NotBuilt");
        [Fact(Skip = "M11-T4")] public void Crafter_PreemptedByThirst_InputHauledBack_OrderCompletes() => Placeholder.Write("crafting.md scenario 4, CRF-14/JOB-07: a crafter made thirsty mid-job drops its logs as a pile, drinks, the logs are hauled back, and the Make order still completes with the expected count");
        [Fact(Skip = "M11-T4")] public void RefinedBlocks_BuiltFromRefinedItems_DigRefundsThem() => Placeholder.Write("crafting.md scenario 5, CRF-02/CON-17: Wood planks, Polished stone and Slate walls take planks and cutstone from storage (and no log or stone); digging them drops planks and cutstone piles that are hauled to storage");
        [Fact(Skip = "M11-T4")] public void Workshop_TornDownOrCollapsed_DropsOutput_CancelsJobs() => Placeholder.Write("CRF-14/BLD-09/GRV-07: deconstructing a sawmill holding 6 planks drops them with the refund; a collapsed stonecutter drops its output as falling piles; its Craft and Unload jobs are cancelled; nothing floats (Grounding)");
        [Fact(Skip = "M11-T4")] public void SaveLoad_MidCraftCycle_ContinuesIdentically() => Placeholder.Write("crafting.md scenario 9, SAV-03: save while a Craft job is claimed mid-Work with input carried; the loaded game matches the hash every 100 ticks for 1000 ticks");
    }

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
