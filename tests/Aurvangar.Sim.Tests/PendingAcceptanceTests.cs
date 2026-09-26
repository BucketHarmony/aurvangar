using Aurvangar.Sim.Tests.Support;
using Xunit;

// Acceptance tests for M4-T5 onward. Each is specified by its name and the Placeholder text.
// To start a task: move the relevant tests into their own file (e.g. JobBoardTests.cs), write the bodies, un-skip.
// A placeholder that is un-skipped without a body fails, by design.

namespace Aurvangar.Sim.Tests
{
    public class NeedsTests
    {
        [Fact(Skip = "M5-T5")] public void Decay_Rates() => Placeholder.Write("ECO-03");
        [Fact(Skip = "M5-T5")] public void Threshold_PostsDrinkBeforeEat() => Placeholder.Write("ECO-04, JOB-05 priorities");
        [Fact(Skip = "M5-T5")] public void Consume_RestoresUntil9000() => Placeholder.Write("ECO-05");
        [Fact(Skip = "M5-T5")] public void HealthRegen_WhenFed() => Placeholder.Write("ECO-06");
    }

    public class BushTests
    {
        [Fact(Skip = "M6-T3")] public void Harvest_Gives2Berries_RegrowsIn1200() => Placeholder.Write("ECO-10");
        [Fact(Skip = "M6-T3")] public void HarvestNotPosted_WhenFoodStockAtLeast60() => Placeholder.Write("ECO-10");
    }

    public class MoistureTests
    {
        [Fact(Skip = "M6-T1")] public void Radius5Moist_Radius6Dry() => Placeholder.Write("ECO-15 / needs-economy.md moisture scenario");
        [Fact(Skip = "M6-T1")] public void HeightWindow_Respected() => Placeholder.Write("ECO-15: water 3 below surface does not moisten");
        [Fact(Skip = "M6-T1")] public void RecomputesEvery50Ticks() => Placeholder.Write("ARCH-01 step 4");
    }
}

namespace Aurvangar.Sim.Tests.Scenarios
{
    [Trait("Category", "Scenario")]
    public class StarvationScenarioTests
    {
        [Fact(Skip = "M5-T5")] public void NoFood_DiesStarvedAfter1000TicksAtZero() => Placeholder.Write("needs-economy.md scenario 2");
        [Fact(Skip = "M5-T5")] public void AllDead_ColonyLostOnce() => Placeholder.Write("ECO-07");
        [Fact(Skip = "M5-T5")] public void ThirstyAgent_PreemptsHaul_DropsCargo() => Placeholder.Write("JOB-07 / jobs-agents.md scenario 4");
    }

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
