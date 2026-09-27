using Aurvangar.Sim.Tests.Support;
using Xunit;

// Acceptance tests for M8 (free-form construction, docs/specs/construction.md). Each test is specified by its name
// and its Placeholder text. To start a task, move its tests into their own file (e.g. BlockPlacementTests.cs), write
// the bodies, then un-skip. A placeholder that is un-skipped without a body fails, by design. Delete this file when
// it is empty.

namespace Aurvangar.Sim.Tests.Scenarios
{
    [Trait("Category", "Scenario")]
    public class MonumentScenarioTests
    {
        [Fact(Skip = "M8-T6")] public void Seed1_Monument_CompleteByDay10_AllAlive() => Placeholder.Write("M8-T6: MonumentScript (seed 1) plans and releases a hollow Masonry tower at least 7x7 and 8 high with a door and an inner stair, plus a walled courtyard, and digs a hill quarry; by tick 24000 every entry is built and all 5 dwarves are alive");
        [Fact(Skip = "M8-T6")] public void Seed1_Monument_NoDwarfWalledIn_NoFloatingBlock() => Placeholder.Write("M8-T6, CON-09/CON-14: checked every 100 ticks through the session: every living dwarf not fleeing water is in the Great Hall's region, and every built block is grounded");
        [Fact(Skip = "M8-T6")] public void Seed1_Monument_SaveLoadMidBuild_Identical() => Placeholder.Write("M8-T6, SAV-03: save at a mid-build tick; the loaded game plus the rest of MonumentScript gives equal hashes every 100 ticks for 2000 ticks");
        [Fact(Skip = "M8-T6")] public void MonumentScript_AvailableToHeadlessAndScreenshots() => Placeholder.Write("M8-T6: ScreenshotScripts.Names contains 'monument', it is timed like 'survival' (commands enqueued at their ticks), and a harness run gives the same hash as MonumentScript.Run at tick 3000");
    }
}

namespace Aurvangar.Sim.Tests.Perf
{
    [Trait("Category", "Perf")]
    [Collection(PerfCollection.Name)]
    public class MonumentPerfTests
    {
        [Fact(Skip = "M8-T6")] public void SimP1_DuringMonumentBuild() => Placeholder.Write("CON-P1: seed 1 with MonumentScript, 500 Tick() calls mid-build (many Build jobs open, dwarves on the tower): median <= 8 ms (x PERF_SCALE); print the build-poster and place-trial totals");
    }
}
