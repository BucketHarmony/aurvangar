using Aurvangar.Sim.Tests.Support;
using Xunit;

// Acceptance tests for M4-T5 onward. Each is specified by its name and the Placeholder text.
// To start a task: move the relevant tests into their own file (e.g. JobBoardTests.cs), write the bodies, un-skip.
// A placeholder that is un-skipped without a body fails, by design.

namespace Aurvangar.Sim.Tests
{
    public class BushTests
    {
        [Fact(Skip = "M6-T3")] public void Harvest_Gives2Berries_RegrowsIn1200() => Placeholder.Write("ECO-10");
        [Fact(Skip = "M6-T3")] public void HarvestNotPosted_WhenFoodStockAtLeast60() => Placeholder.Write("ECO-10");
    }

}

namespace Aurvangar.Sim.Tests.Scenarios
{
    [Trait("Category", "Scenario")]
    public class FarmScenarioTests
    {
        [Fact(Skip = "M6-T2")] public void MoistTile_MaturesAt7200() => Placeholder.Write("ECO-12");
        [Fact(Skip = "M6-T2")] public void DryTile_NeverGrows() => Placeholder.Write("ECO-12");
        [Fact(Skip = "M6-T2")] public void DryForADay_Withers() => Placeholder.Write("ECO-13");
        [Fact(Skip = "M6-T2")] public void Harvest_Yields3Potatoes() => Placeholder.Write("ECO-12");
    }

    [Trait("Category", "Scenario")]
    public class SurvivalScenarioTests
    {
        [Fact(Skip = "M6-T6")] public void Seed1_SurvivalScript_AllAliveAtDay10() => Placeholder.Write("needs-economy.md scenario 5");
        [Fact(Skip = "M6-T6")] public void Seed1_NoCommands_ColonyLost() => Placeholder.Write("control: with no player commands the colony dies (stock runs out)");
    }
}
