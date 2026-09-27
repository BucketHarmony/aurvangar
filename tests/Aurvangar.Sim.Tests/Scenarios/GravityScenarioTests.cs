using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M11-T9 (G6 follow-up: "buildings, items, log piles with no ground beneath them should fall down to
/// ground"): GRV-01..10 in docs/specs/gravity.md. Worlds are flat stone to y = 4, so agents stand on y = 5.</summary>
[Trait("Category", "Scenario")]
public class GravityScenarioTests
{
    private static readonly Int3 Col = new(10, G, 10);

    private static ItemStack Stack(string item, int n) => new(TestContent.Db.Item(item), n);

    private static Agent AgentAt(Simulation sim, Int3 cell)
    {
        var a = sim.Agents.All.First();
        a.Cell = cell;
        a.NextCell = cell;
        return a;
    }

    /// <summary>GRV-03: a log pile on a dirt floor over a 2-deep shaft. Dig the floor and, in the same tick, the pile
    /// lies at the shaft bottom, merged with the logs already there (ECO-08).</summary>
    [Fact]
    public void Pile_FallsToTheNextFloor_AndMerges()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(Col + new Int3(0, -3, 0), Col + new Int3(0, -2, 0), BlockId.Air)
            .FillBox(Col + Int3.Down, Col + Int3.Down, BlockId.Dirt)
            .Pile(Col, "log", 4).Pile(Col + new Int3(0, -3, 0), "log", 2)
            .Agent(Col + new Int3(1, 0, 0)).Build();
        var a = sim.Agents.All.First();

        Assert.Equal(ActionResult.Ok, sim.Actions.Dig(a.Id, Col + Int3.Down));
        Assert.Equal(new[] { Col }, Grounding.FloatingPiles(sim));   // floats until the gravity step
        sim.Tick();

        Assert.True(sim.Piles.At(Col).IsEmpty);
        Assert.Equal(Stack("log", 6), sim.Piles.At(Col + new Int3(0, -3, 0)));
        Assert.Empty(Grounding.FloatingPiles(sim));
    }

    /// <summary>GRV-03: a pile that falls onto another item's pile goes to the nearest free standable cell (ECO-08).
    /// A dig drop over a cave falls too.</summary>
    [Fact]
    public void Pile_OnAnotherItem_SpiralsOut_AndDigDropFalls()
    {
        // A 3x3 room at y 2..3 under x 9..11, z 9..11; its floor (y 1) is stone. The roof cell (10,4,10) is stone.
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(new Int3(9, 2, 9), new Int3(11, 3, 11), BlockId.Air)
            .Pile(Col, "log", 3).Pile(new Int3(10, 2, 10), "berries", 1)
            .Agent(Col + new Int3(1, 0, 0)).Build();
        var a = sim.Agents.All.First();

        Assert.Equal(ActionResult.Ok, sim.Actions.Dig(a.Id, Col + Int3.Down));   // the stone roof: its drop falls
        sim.Tick();

        Assert.Empty(Grounding.FloatingPiles(sim));
        Assert.Equal(Stack("berries", 1), sim.Piles.At(new Int3(10, 2, 10)));
        Assert.Equal(3, PileTotal(sim, "log"));
        Assert.Equal(1, PileTotal(sim, "stone"));
        foreach (var (cell, _) in sim.Piles.All) Assert.Equal(2, cell.Y);
    }

    /// <summary>GRV-04: a haul of a pile that falls is withdrawn (not failed) and a new haul takes the pile from
    /// where it landed, one level down in a dug hole.</summary>
    [Fact]
    public void FallenPile_IsStillHauled_WithoutFailures()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(Col + Int3.Down, Col + Int3.Down, BlockId.Dirt)
            .Pile(Col, "log", 4)
            .Hub(new Int3(20, G, 20)).Agent(new Int3(25, G, 25)).Build();
        sim.Tick();
        Assert.Contains(sim.Jobs.All, j => j.Kind == JobKind.Haul && j.Target == Col);

        var digger = sim.Agents.Spawn(Col + new Int3(-1, 0, 0), "Digger");
        Assert.Equal(ActionResult.Ok, sim.Actions.Dig(digger.Id, Col + Int3.Down));
        sim.Tick();
        Assert.Equal(Stack("log", 4), sim.Piles.At(Col + Int3.Down));
        Assert.DoesNotContain(sim.Jobs.All, j => j.Steps.Any(s => s.Kind == StepKind.PickUp && s.Cell == Col));

        RunUntil(sim, () => Stored(Hub(sim), "log") == 4, 2000);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    /// <summary>Backlog scenario: dwarves dig out the ground under a stocked warehouse. It stands while any floor cell
    /// is left (GRV-01), then collapses into piles in its pit (GRV-07): half its cost (10 logs) and its 30 stone, which
    /// are hauled to the hall. Nothing floats at any tick, and no job fails.</summary>
    [Fact]
    public void Warehouse_DugOut_CollapsesIntoPiles()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .Hub(new Int3(20, G, 20)).Stock("log", 5)
            .Storage("warehouse", WhOrigin).Stock("stone", 30)
            .Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15)).Agent(new Int3(17, G, 15)).Build();
        var wh = Site(sim);
        var floor = wh.FootprintCells().Where(c => c.Y == G).Select(c => c + Int3.Down).ToList();

        sim.Enqueue(new DesignateDig(new Int3(9, G - 1, 9), new Int3(12, G - 1, 12)));
        sim.Tick();
        Assert.Equal(16, sim.Designations.Count);   // GRV-06: the floor of a complete warehouse is marked
        Assert.All(floor, c => Assert.Equal(DesignationMark.Dig, sim.Designations.Get(c)));

        int solidAtCollapse = -1;
        RunUntil(sim, () => sim.Buildings.Get(wh.Id) is null, 6000, () =>
        {
            Assert.Empty(Grounding.FloatingPiles(sim));
            Assert.Empty(Grounding.UnsupportedBuildings(sim));
            if (sim.Buildings.Get(wh.Id) is null && solidAtCollapse < 0)
                solidAtCollapse = floor.Count(c => sim.World.IsSolid(c));
        });
        Assert.Equal(0, solidAtCollapse);
        Assert.All(wh.FootprintCells(), c => Assert.Equal(BlockId.Air, sim.World.GetBlock(c)));
        Assert.DoesNotContain(sim.Jobs.All, j => NamesBuilding(j, wh.Id));
        Assert.Equal(10, PileTotal(sim, "log") + CarriedTotal(sim, "log") + Stored(Hub(sim), "log") - 5);
        Assert.Contains(sim.Piles.All, p => floor.Contains(p.Cell));   // in the pit

        RunUntil(sim, () => sim.Designations.Count == 0 && sim.Piles.Count == 0
            && sim.Agents.All.All(a => a.Carried.IsEmpty), 6000);
        Assert.Equal(5 + 10, Stored(Hub(sim), "log"));
        Assert.Equal(30 + 16, Stored(Hub(sim), "stone"));
        Assert.Empty(Grounding.Floating(sim));
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.All(sim.Agents.All, a => Assert.True(a.IsAlive));
    }

    /// <summary>GRV-08: undermine the bottom of a two-levee stack and both come down in the same tick; each drops 1
    /// log (half of 2), merged in one pile on the dug cell.</summary>
    [Fact]
    public void LeveeStack_CollapsesWhole()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(Col + Int3.Down, Col + Int3.Down, BlockId.Dirt)
            .Agent(Col + new Int3(1, 0, 0)).Build();
        var levee = TestContent.Db.Building("levee");
        var low = sim.Buildings.PlacePrebuilt(levee, Col, 0);
        var high = sim.Buildings.PlacePrebuilt(levee, Col + Int3.Up, 0);
        var a = sim.Agents.All.First();

        Assert.Equal(ActionResult.Ok, sim.Actions.Dig(a.Id, Col + Int3.Down));
        sim.Tick();

        Assert.Null(sim.Buildings.Get(low.Id));
        Assert.Null(sim.Buildings.Get(high.Id));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(Col));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(Col + Int3.Up));
        Assert.Equal(Stack("log", 2), sim.Piles.At(Col + Int3.Down));
        Assert.Empty(Grounding.FloatingPiles(sim));
    }

    /// <summary>GRV-07/08: a levee blueprint stacked on an undermined levee is cancelled with it; a dwarf standing on
    /// the collapsing levee is moved to its stand cell, alive, on standable ground; a dwarf beside it stays.</summary>
    [Fact]
    public void Collapse_CancelsBlueprintOnTop_AndMovesAgentsOff()
    {
        var l = new Int3(11, G, 10);
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(l + Int3.Down, l + Int3.Down, BlockId.Dirt)
            .FillBox(new Int3(12, G, 10), new Int3(12, G, 10), BlockId.Stone)   // a step up beside the levee
            .Agent(new Int3(10, G, 10)).Build();
        var levee = TestContent.Db.Building("levee");
        var low = sim.Buildings.PlacePrebuilt(levee, l, 0);
        Assert.Equal(PlacementResult.Ok, sim.Buildings.TryPlaceBlueprint(levee, l + Int3.Up, 0, out var bp));
        var digger = sim.Agents.All.First();
        var rider = sim.Agents.Spawn(l + Int3.Up, "Rider");                 // on top of the levee
        var beside = sim.Agents.Spawn(new Int3(12, G + 1, 10), "Beside");   // on the step, in reach but not on it

        Assert.Equal(ActionResult.Ok, sim.Actions.Dig(digger.Id, l + Int3.Down));
        sim.Tick();

        Assert.Null(sim.Buildings.Get(low.Id));
        Assert.Null(sim.Buildings.Get(bp!.Id));
        Assert.True(rider.IsAlive);
        Assert.NotEqual(l + Int3.Up, rider.Cell);
        Assert.True(sim.PathGrid.IsStandable(rider.Cell));
        Assert.Equal(new Int3(12, G + 1, 10), beside.Cell);
        Assert.Equal(Stack("log", 1), sim.Piles.At(l + Int3.Down));
        Assert.Empty(Grounding.UnsupportedBuildings(sim));
        Assert.Empty(Grounding.FloatingPiles(sim));
    }

    /// <summary>GRV-07: the wagon collapses like a warehouse (10 logs back plus its stock).</summary>
    [Fact]
    public void Wagon_Collapses()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .Storage("wagon", new Int3(10, G, 10)).Stock("stone", 7)
            .Agent(new Int3(9, G, 10)).Build();
        var wagon = Site(sim, "wagon");
        var a = sim.Agents.All.First();
        var floor = wagon.FootprintCells().Where(c => c.Y == G).Select(c => c + Int3.Down).ToList();
        Assert.Equal(6, floor.Count);
        foreach (var c in floor)
        {
            Assert.NotNull(sim.Buildings.Get(wagon.Id));   // GRV-01: it stands on any floor cell
            AgentAt(sim, c.X == 10 ? new Int3(9, G, c.Z) : new Int3(12, G, c.Z));
            Assert.Equal(ActionResult.Ok, sim.Actions.Dig(a.Id, c));
            sim.Tick();
        }
        Assert.Null(sim.Buildings.Get(wagon.Id));
        Assert.Equal(10, PileTotal(sim, "log"));
        Assert.Equal(7 + floor.Count, PileTotal(sim, "stone"));
        Assert.Empty(Grounding.FloatingPiles(sim));
    }

    /// <summary>GRV-05: the ground under the Great Hall is never marked, and a dig of it is Blocked.</summary>
    [Fact]
    public void Hall_IsAnchored()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(new Int3(10, G, 10)).Agent(new Int3(9, G, 10)).Build();
        var hallFloor = new Int3(10, G - 1, 10);
        var a = sim.Agents.All.First();
        Assert.Equal(ActionResult.Blocked, sim.Actions.Dig(a.Id, hallFloor));
        sim.Enqueue(new DesignateDig(hallFloor, hallFloor + new Int3(-1, 0, 0)));
        sim.Tick();
        Assert.Equal(DesignationMark.None, sim.Designations.Get(hallFloor));
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(hallFloor + new Int3(-1, 0, 0)));
    }

    /// <summary>GRV-09: a built block rests on a levee. The levee's only floor cell holds the block up through the
    /// levee, so its dig waits (CON-10, DigStatus Support) and Dig is Blocked. GRV-06: the floor of a building being
    /// deconstructed is protected.</summary>
    [Fact]
    public void BlockOnBuilding_HoldsItsLastFloor_AndDeconstructingFloorIsProtected()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(Col + Int3.Down, Col + Int3.Down, BlockId.Dirt)
            .FillBox(Col + new Int3(0, -1, 1), Col + new Int3(0, -1, 1), BlockId.Air)   // exposes the floor (DSG-03)
            .Agent(Col + new Int3(1, 0, 0)).Build();
        var levee = sim.Buildings.PlacePrebuilt(TestContent.Db.Building("levee"), Col, 0);
        sim.World.SetBlock(Col + Int3.Up, BlockId.Masonry);
        var a = sim.Agents.All.First();

        Assert.True(Blocks.Support.Depends(sim, Col + Int3.Down));
        Assert.Equal(ActionResult.Blocked, sim.Actions.Dig(a.Id, Col + Int3.Down));
        sim.Enqueue(new DesignateDig(Col + Int3.Down, Col + Int3.Down));
        sim.Tick();
        Assert.Equal(DesignationMark.Dig, sim.Designations.Get(Col + Int3.Down));
        Assert.DoesNotContain(sim.Jobs.All, j => j.Kind == JobKind.Dig);
        Assert.Equal(DigWait.Support, DigStatus.Of(sim, Col + Int3.Down));
        Assert.NotNull(sim.Buildings.Get(levee.Id));
        Assert.Empty(Grounding.Floating(sim));

        // Without the block the dig is allowed; while the levee is being deconstructed it is not.
        sim.World.SetBlock(Col + Int3.Up, BlockId.Air);
        Assert.False(Blocks.Support.Depends(sim, Col + Int3.Down));
        levee.State = BuildingState.Deconstructing;
        Assert.Equal(ActionResult.Blocked, sim.Actions.Dig(a.Id, Col + Int3.Down));
        Assert.Equal(DigWait.Support, DigStatus.Of(sim, Col + Int3.Down));
    }

    /// <summary>GRV-07 step 2 (sim-reviewer): a levee walled in on all sides has no free stand cell but the one on
    /// top of it, where the rider stands. After the collapse the rider is on standable ground (the pit
    /// under the levee), not left on air.</summary>
    [Fact]
    public void Collapse_WalledInLevee_MovesRiderToStandableGround()
    {
        // A 3x3 ring of stone two high around the levee: no cell in its reach is standable.
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(Col + new Int3(-1, 0, -1), Col + new Int3(1, 1, 1), BlockId.Stone)
            .FillBox(Col, Col + Int3.Up, BlockId.Air)
            .Agent(new Int3(20, G, 20)).Build();
        var levee = sim.Buildings.PlacePrebuilt(TestContent.Db.Building("levee"), Col, 0);
        Assert.Equal(new[] { Col + Int3.Up }, sim.Buildings.ReachStandCells(levee.Def, levee.Origin, levee.Rotation));
        Assert.Equal(Col + Int3.Up, Construction.StandCell(sim, levee));
        var rider = sim.Agents.Spawn(Col + Int3.Up, "Rider");

        sim.World.SetBlock(Col + Int3.Down, BlockId.Air);   // undermined
        sim.Tick();

        Assert.Null(sim.Buildings.Get(levee.Id));
        Assert.True(rider.IsAlive);
        Assert.True(sim.PathGrid.IsStandable(rider.Cell), $"rider at {rider.Cell}");
        Assert.Equal(Col + Int3.Down, rider.Cell);
        Assert.Empty(Grounding.Floating(sim));
    }

    /// <summary>GRV-06 (sim-reviewer): a Dig job posted on a complete warehouse's floor, then the warehouse is ordered
    /// deconstructed. The job waits (CON-10 path, Support) instead of failing; the floor stays until the warehouse is
    /// gone, then it is dug. No job fails.</summary>
    [Fact]
    public void DigPosted_ThenDeconstruct_WaitsWithoutFailures()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .Hub(new Int3(20, G, 20))
            .Storage("warehouse", WhOrigin)
            .Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15)).Build();
        var wh = Site(sim);
        var cell = WhOrigin + Int3.Down;
        sim.World.SetBlock(cell + new Int3(-1, 0, 0), BlockId.Air);   // exposes the floor cell (DSG-03)

        sim.Enqueue(new DesignateDig(cell, cell));
        sim.Tick();
        Assert.Contains(sim.Jobs.All, j => j.Kind == JobKind.Dig && j.Target == cell);

        sim.Enqueue(new Deconstruct(wh.Id));
        RunUntil(sim, () => sim.Buildings.Get(wh.Id) is null, 4000, () =>
        {
            if (sim.Buildings.Get(wh.Id) is not null) Assert.True(sim.World.IsSolid(cell));
        });
        RunUntil(sim, () => !sim.World.IsSolid(cell), 2000);
        Assert.Equal(0, sim.Counters.JobsFailed);
        Assert.Empty(Grounding.FloatingPiles(sim));
    }

    /// <summary>GRV-10: no new state. A save taken after a collapse loads to the same hash, and both runs stay equal.</summary>
    [Fact]
    public void SaveLoad_AfterCollapse_SameHash()
    {
        var sim = new ScenarioBuilder().Ground(G - 1)
            .FillBox(Col + Int3.Down, Col + Int3.Down, BlockId.Dirt)
            .Hub(new Int3(20, G, 20)).Agent(Col + new Int3(1, 0, 0)).Build();
        sim.Buildings.PlacePrebuilt(TestContent.Db.Building("levee"), Col, 0);
        var a = sim.Agents.All.First();
        Assert.Equal(ActionResult.Ok, sim.Actions.Dig(a.Id, Col + Int3.Down));
        sim.Tick();
        Assert.DoesNotContain(sim.Buildings.All, b => b.Def.Id == "levee");

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        for (int i = 0; i < 5; i++)
        {
            sim.RunTicks(100);
            loaded.RunTicks(100);
            Assert.Equal(sim.StateHash(), loaded.StateHash());
        }
    }

    /// <summary>A job step or storage reservation names the building (GRV-07 step 1).</summary>
    private static bool NamesBuilding(Job j, BuildingId id) =>
        j.Reservations.Any(r => r.Building == id)
        || j.Steps.Any(s => s.Target == id.Value && (s.Kind is StepKind.DeliverTo or StepKind.PickUpFromStorage
            or StepKind.Consume || s.Goal == GoalMode.Building));
}
