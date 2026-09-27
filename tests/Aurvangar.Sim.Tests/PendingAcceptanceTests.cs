using Aurvangar.Sim.Tests.Support;
using Xunit;

// Acceptance tests for M8 (free-form construction, docs/specs/construction.md). Each test is specified by its name
// and its Placeholder text. To start a task, move its tests into their own file (e.g. BlockPlacementTests.cs), write
// the bodies, then un-skip. A placeholder that is un-skipped without a body fails, by design. Delete this file when
// it is empty.

namespace Aurvangar.Sim.Tests
{
    [Trait("Category", "Unit")]
    public class BlockContentTests
    {
        [Fact(Skip = "M8-T2")] public void ConstructionBlocks_LoadWithCostLabelAndColour() => Placeholder.Write("CON-01: ids 8..10 Masonry/Planks/PolishedStone with the table's label, cost, buildTicks and hardness, IsConstruction true only for them, a palette colour each; Blocks.Count == 11 (update ContentDbTests' 8, see CON-02)");
        [Fact(Skip = "M8-T2")] public void ConstructionBlock_BadData_ThrowsNamingFileAndId() => Placeholder.Write("CON-02: two cost items, an unknown cost item, count 0 and 11, buildTicks 0, a drop, no label, no palette colour, and a json id with no BlockId value each throw with the file and id");
    }

    [Trait("Category", "Unit")]
    public class BuildShapeTests
    {
        [Fact(Skip = "M8-T2")] public void Shapes_ExpandToExpectedCells() => Placeholder.Write("CON-07 table: Single 1 cell; Line along the longer axis (ties go to X); Wall 5 long x 3 high = 15; Floor 4x3 = 12; HollowBox 5x4 ring x 2 = 28; HollowBox 1 wide = Wall; Stair of 4 rises one level per cell; cells in ascending (y, index) order");
        [Fact(Skip = "M8-T2")] public void DesignateBuild_Rejections_InOrder() => Placeholder.Write("CON-07: BadHeight (0 and 33), NotBuildable (Stone, BuildingSolid), TooLarge (> 4096 cells), OutOfWorld, NothingToBuild (every cell solid); each emits CommandRejected with that reason and adds no entry");
    }

    [Trait("Category", "Unit")]
    public class BlockPlacementTests
    {
        [Fact(Skip = "M8-T2")] public void PlaceBlock_ConstructionBlock_ChecksAndConsumesCarriedCost() => Placeholder.Write("CON-13 Place checks in order: InvalidTarget (solid cell), Blocked (building footprint, agent in cell or below, pile, plant), Unsupported (no solid below or beside), InventoryEmpty, WrongItem, NotEnoughItems (1 stone for PolishedStone); Ok removes the cost from the carried stack; natural blocks keep ADR-027 (free)");
        [Fact(Skip = "M8-T2")] public void CanPlan_ReasonsInOrder() => Placeholder.Write("CON-08: OutOfWorld, Solid, Building (footprint and entrance), Plant, Farm (tile below), Unsupported (floating); water in the cell is Ok; a cell supported only through another entry or through the command's own pending cells is Ok (CON-09 plan support)");
        [Fact(Skip = "M8-T2")] public void BuildingPlacement_OverPlanEntry_PlannedBlocks() => Placeholder.Write("CON-08: BLD-02 CanPlace returns PlannedBlocks when a footprint or entrance cell holds an entry, checked right after Overlaps; DesignateFarm skips a tile whose cell above has an entry");
        [Fact(Skip = "M8-T2")] public void PlanEntries_SavedAndHashed_FormatVersion6() => Placeholder.Write("CON-04: an entry changes StateHash (block and state both count); an empty store adds nothing to the hash; save/load keeps the hash equal and the entries identical; SaveGame.FormatVersion == 6 and a v5 file is refused (SAV-04)");
    }
}

namespace Aurvangar.Sim.Tests.Scenarios
{
    [Trait("Category", "Scenario")]
    public class BlockBuildScenarioTests
    {
        [Fact(Skip = "M8-T2")] public void Wall_BuiltBottomUpFromStorage() => Placeholder.Write("construction.md scenario 1, CON-12/13: a 6x3 Masonry wall with 30 stone in the hub is fully built; in every column the lower block is placed before the upper; stone taken from storage == blocks placed; at least one job places more than one block (batch)");
        [Fact(Skip = "M8-T2")] public void OnlyReadyEntriesPostJobs_StatusesExplainTheRest() => Placeholder.Write("construction.md scenario 2, CON-05/12: a floating entry (NoSupport), an entry above a pending one (BelowFirst), an entry with no stone stored (NoMaterial), an entry under a standing dwarf (Occupied) and an entry sealed off (NoAccess) post no Build job; StatusOf reports each; adding stone makes the NoMaterial one Ready");
        [Fact(Skip = "M8-T2")] public void MasonryWall_HoldsWaterLikeLevee() => Placeholder.Write("construction.md scenario 3, CON-16: buildings.md scenario 3 with a Masonry wall across the channel: the downstream level drops within 200 ticks of the last block; a block placed into a wet cell keeps WAT-11 conservation exact");
        [Fact(Skip = "M8-T2")] public void ClosedRing_NeverWallsInADwarf() => Placeholder.Write("construction.md scenario 4, CON-14: a HollowBox ring with no door, one dwarf working inside: the closing block waits (status WouldStrand) until the dwarf leaves; an idle dwarf inside steps out; every living dwarf stays in the hall's region every tick; the ring then completes (an empty room may be sealed)");
        [Fact(Skip = "M8-T2")] public void HighWall_WithStair_BuiltFromBuiltBlocks() => Placeholder.Write("construction.md scenario 5, CON-11: a 5-high Masonry wall with a Stair against it is fully built; at least one Place happens from a stand cell whose floor is a built block; no failed jobs");
        [Fact(Skip = "M8-T2")] public void Cancel_MidBuild_KeepsPlacedBlocks_LosesNoStone() => Placeholder.Write("construction.md scenario 6, CON-07/DSG-06: CancelDesignation over a half-built wall removes its entries and Build jobs; placed blocks stay; carried stone is dropped and hauled back; stored + piles + carried + placed x cost == the initial stone");
        [Fact(Skip = "M8-T2")] public void UnreachableEntry_GivenUp_ResetNearby() => Placeholder.Write("CON-15: an entry whose material storage is cut off from the builders gets a GiveUpSource.Build mark and the alert 'Unreachable: Stone wall'; a walkability change within 8 cells resets it and the block gets built");
        [Fact(Skip = "M8-T2")] public void SaveLoad_MidBuild_ContinuesIdentically() => Placeholder.Write("construction.md scenario 9, SAV-03: save while Build jobs are claimed and carrying; the loaded game matches the hash every 100 ticks for 1000 ticks");
    }

    [Trait("Category", "Scenario")]
    public class BlockDeconstructScenarioTests
    {
        [Fact(Skip = "M8-T3")] public void DigBuiltBlock_RefundsFullCost() => Placeholder.Write("CON-17: digging Masonry drops 1 stone, Planks 1 log and PolishedStone 2 stone as one pile on the dug cell, and the dig takes the block's hardness in ticks; the piles are hauled to storage");
        [Fact(Skip = "M8-T3")] public void DeconstructBlocks_MarksOnlyBuiltBlocks() => Placeholder.Write("CON-18: DesignateDeconstructBlocks over a wall and the ground around it marks only the wall's blocks; a box with no built block is rejected with NothingToDeconstruct");
        [Fact(Skip = "M8-T3")] public void Tower_ComesDownTopFirst_NoBlockEverUngrounded() => Placeholder.Write("construction.md scenario 7, CON-10: a marked tower and a bridge span are removed; the CON-09 invariant (every built block grounded) holds after every tick; supporting blocks wait (no failures, no DigUnreachable) and the span comes down from its free end");
        [Fact(Skip = "M8-T3")] public void DigUnderBuiltBlock_WaitsAndActionBlocked() => Placeholder.Write("CON-10: a dig mark on the terrain block under a wall posts no job while the wall stands; WorldActions.Dig on it returns Blocked; once the wall above is deconstructed the dig proceeds");
        [Fact(Skip = "M8-T3")] public void DeconstructLeveeUnderBlocks_RejectedSupportsBlocks() => Placeholder.Write("CON-10/BLD-09: Deconstruct of a complete levee with a Masonry block on top is rejected with SupportsBlocks (after BuildingOnTop in the check order)");
    }

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
