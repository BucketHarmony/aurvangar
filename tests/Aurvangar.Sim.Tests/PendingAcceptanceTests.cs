using Aurvangar.Sim.Tests.Support;
using Xunit;

// Acceptance tests for M8 (free-form construction, docs/specs/construction.md). Each test is specified by its name
// and its Placeholder text. To start a task, move its tests into their own file (e.g. BlockPlacementTests.cs), write
// the bodies, then un-skip. A placeholder that is un-skipped without a body fails, by design. Delete this file when
// it is empty.

namespace Aurvangar.Sim.Tests.Scenarios
{
    [Trait("Category", "Scenario")]
    public class BlockPlanScenarioTests
    {
        [Fact(Skip = "M8-T4")] public void PlannedEntries_NotBuilt() => Placeholder.Write("construction.md scenario 8, CON-04/07: DesignateBuild with Plan = true adds Planned entries; no Build job is posted in 2000 ticks with stone in stock; StatusOf == Planned");
        [Fact(Skip = "M8-T4")] public void ReleasePlan_Box_ReleasesOnlyInside() => Placeholder.Write("CON-07 ReleasePlan: releasing half the plan builds that half only; the rest stays Planned; a box with no Planned entry is rejected with NothingToRelease; a world-sized box releases the rest");
        [Fact(Skip = "M8-T4")] public void MaterialTotals_NeededVersusStored() => Placeholder.Write("CON-06: Needed(Planned), Needed(Released) and Needed(null) per item match the entries' costs (PolishedStone counts 2 stone) and drop by the cost as each block is placed");
        [Fact(Skip = "M8-T4")] public void PlannedState_SavedAndHashed() => Placeholder.Write("CON-04: Planned vs Released changes the hash; save/load keeps states; after load ReleasePlan behaves the same as in the uninterrupted run (hash equal at +1000 ticks)");
    }

    [Trait("Category", "Scenario")]
    public class MonumentScenarioTests
    {
        [Fact(Skip = "M8-T6")] public void Seed1_Monument_CompleteByDay10_AllAlive() => Placeholder.Write("M8-T6: MonumentScript (seed 1) plans and releases a hollow Masonry tower at least 7x7 and 8 high with a door and an inner stair, plus a walled courtyard, and digs a hill quarry; by tick 24000 every entry is built and all 5 dwarves are alive");
        [Fact(Skip = "M8-T6")] public void Seed1_Monument_NoDwarfWalledIn_NoFloatingBlock() => Placeholder.Write("M8-T6, CON-09/CON-14: checked every 100 ticks through the session: every living dwarf not fleeing water is in the Great Hall's region, and every built block is grounded");
        [Fact(Skip = "M8-T6")] public void Seed1_Monument_SaveLoadMidBuild_Identical() => Placeholder.Write("M8-T6, SAV-03: save at a mid-build tick; the loaded game plus the rest of MonumentScript gives equal hashes every 100 ticks for 2000 ticks");
        [Fact(Skip = "M8-T6")] public void MonumentScript_AvailableToHeadlessAndScreenshots() => Placeholder.Write("M8-T6: ScreenshotScripts.Names contains 'monument', it is timed like 'survival' (commands enqueued at their ticks), and a harness run gives the same hash as MonumentScript.Run at tick 3000");
    }
}

namespace Aurvangar.Sim.Tests.View
{
    [Trait("Category", "Unit")]
    public class BlockToolTests
    {
        [Fact(Skip = "M8-T5")] public void Ghost_UsesBuildShapes_InvalidCellsRedWithReason() => Placeholder.Write("VIEW-21: the tool's ghost cells equal BuildShapes.Cells for each shape; the anchor is the air cell on the picked face; cells failing CanPlan are red with the CON-08 reason text; release sends DesignateBuild with the chosen block and Plan flag (P toggles)");
        [Fact(Skip = "M8-T5")] public void Height_FromSliceAndKeys() => Placeholder.Write("VIEW-21: Wall/Box height starts at SliceY - A.Y + 1 with the slice active, else 3; +/- change it by 1 within 1..32; Single/Line/Floor/Stair ignore it");
        [Fact(Skip = "M8-T5")] public void ReleaseAndDeconstructTools_SendCommands() => Placeholder.Write("VIEW-21: the Release tool sends ReleasePlan for the dragged box and 'Release all' a world box; the Deconstruct tool dragged over no building sends DesignateDeconstructBlocks");
        [Fact(Skip = "M8-T5")] public void PlanGhosts_AlphaByState_RedWhenStuck() => Placeholder.Write("VIEW-22: Released ghosts alpha 0.45, Planned 0.25 and lighter, GivenUp/NoAccess/WouldStrand/NoSupport red, hidden above SliceY; hover text names the label and status");
        [Fact(Skip = "M8-T5")] public void TopBar_PlanMaterialText() => Placeholder.Write("VIEW-23: TopBarModel.PlanText is empty with no entries, else 'Building: stone 120/64 · Planned: stone 70, log 12' with short items flagged orange");
        [Fact(Skip = "M8-T5")] public void ChunkMesher_ConstructionBlocksUsePaletteColours() => Placeholder.Write("VIEW-03, CON-01: a chunk with Masonry, Planks and PolishedStone meshes each with its palette colour (never the magenta missing colour) and they do not merge with Stone");
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
