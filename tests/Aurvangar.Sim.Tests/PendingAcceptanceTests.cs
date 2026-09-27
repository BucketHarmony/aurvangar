using Aurvangar.Sim.Tests.Support;
using Xunit;

// Acceptance tests for M11-T6..T7 (economy and crafting, docs/specs/crafting.md, ADR-082). Each test is specified by
// its name and its Placeholder text. To start a task, move its tests into their own file, write the bodies, then
// un-skip. A placeholder that is un-skipped without a body fails, by design. Delete this file when it is empty.

namespace Aurvangar.Sim.Tests
{
}

namespace Aurvangar.Sim.Tests.Scenarios
{
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
