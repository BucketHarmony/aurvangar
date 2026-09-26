using Aurvangar.Sim.Tests.Support;
using Xunit;

// Acceptance tests for M4-T5 onward. Each is specified by its name and the Placeholder text.
// To start a task: move the relevant tests into their own file (e.g. JobBoardTests.cs), write the bodies, un-skip.
// A placeholder that is un-skipped without a body fails, by design.

namespace Aurvangar.Sim.Tests.Scenarios
{
    [Trait("Category", "Scenario")]
    public class SurvivalScenarioTests
    {
        [Fact(Skip = "M6-T6")] public void Seed1_SurvivalScript_AllAliveAtDay10() => Placeholder.Write("needs-economy.md scenario 5");
        [Fact(Skip = "M6-T6")] public void Seed1_NoCommands_ColonyLost() => Placeholder.Write("control: with no player commands the colony dies (stock runs out)");
    }
}
