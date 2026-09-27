using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>CON-04, CON-08, CON-09, CON-13 (M8-T2): the Place action, plan validity, plan entries in save and hash.</summary>
[Trait("Category", "Unit")]
public class BlockPlacementTests
{
    private static ItemId Stone => TestContent.Db.Item("stone");
    private static ItemId Log => TestContent.Db.Item("log");

    [Fact]
    public void PlaceBlock_ConstructionBlock_ChecksAndConsumesCarriedCost()
    {
        var sim = new ScenarioBuilder().Ground(8)
            .Layer(new Int3(4, 9, 4), "b")                 // bush at (4,9,4)
            .Hub(new Int3(20, 9, 20))
            .Agent(new Int3(5, 9, 5))
            .Agent(new Int3(4, 9, 5))
            .Pile(new Int3(5, 9, 4), "log", 1)
            .Build();
        sim.Enqueue(new PlaceBuilding("warehouse", new Int3(6, 9, 6), 0));   // blueprint: footprint x 6..7, z 6..7 (air); entrance (6,9,5)
        sim.Tick();
        Assert.NotNull(sim.Buildings.BuildingAt(new Int3(6, 9, 6)));
        var a = sim.Agents.All.First(x => x.Cell == new Int3(5, 9, 5));
        var act = sim.Actions;
        var free = new Int3(6, 9, 4);
        ActionResult Place(Int3 c, BlockId b = BlockId.Masonry) => act.PlaceBlock(a.Id, c, b);

        a.Carried = new ItemStack(Stone, 5);
        Assert.Equal(ActionResult.InvalidTarget, Place(new Int3(6, 8, 5)));   // solid
        Assert.Equal(ActionResult.Blocked, Place(new Int3(6, 9, 6)));         // building footprint
        Assert.Equal(ActionResult.Blocked, Place(new Int3(6, 9, 5)));         // building entrance
        Assert.Equal(ActionResult.Blocked, Place(new Int3(4, 9, 5)));         // agent in the cell
        Assert.Equal(ActionResult.Blocked, Place(new Int3(5, 10, 5)));        // agent in the cell below
        Assert.Equal(ActionResult.Blocked, Place(new Int3(5, 9, 4)));         // pile
        Assert.Equal(ActionResult.Blocked, Place(new Int3(4, 9, 4)));         // plant
        Assert.Equal(ActionResult.Unsupported, Place(new Int3(6, 10, 4)));    // nothing solid below or beside
        Assert.Equal(5, a.Carried.Count);

        a.Carried = ItemStack.Empty;
        Assert.Equal(ActionResult.InventoryEmpty, Place(free));
        a.Carried = new ItemStack(Log, 3);
        Assert.Equal(ActionResult.WrongItem, Place(free));
        var cut = TestContent.Db.Item("cutstone");   // M11-T4: Polished stone costs 2 cut stone (CRF-02)
        a.Carried = new ItemStack(cut, 1);
        Assert.Equal(ActionResult.NotEnoughItems, Place(free, BlockId.PolishedStone));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(free));

        a.Carried = new ItemStack(cut, 3);
        Assert.Equal(ActionResult.Ok, Place(free, BlockId.PolishedStone));
        Assert.Equal(BlockId.PolishedStone, sim.World.GetBlock(free));
        Assert.Equal(new ItemStack(cut, 1), a.Carried);
        a.Carried = new ItemStack(Stone, 1);
        Assert.Equal(ActionResult.Ok, Place(free + Int3.Up));                 // supported by the block just placed
        Assert.True(a.Carried.IsEmpty);

        // ADR-027: natural blocks stay free.
        a.Carried = new ItemStack(Log, 2);
        Assert.Equal(ActionResult.Ok, Place(new Int3(4, 9, 6), BlockId.Stone));
        Assert.Equal(new ItemStack(Log, 2), a.Carried);
    }

    [Fact]
    public void CanPlan_ReasonsInOrder()
    {
        var sim = new ScenarioBuilder().Ground(8)
            .FillBox(new Int3(12, 8, 12), new Int3(12, 8, 12), BlockId.Dirt)
            .Layer(new Int3(3, 9, 3), "b")
            .Hub(new Int3(20, 9, 20))                      // entrance (21,9,19)
            .Water(new Int3(10, 9, 10), 128)
            .Build();
        sim.Enqueue(new DesignateFarm(12, 12, 12, 12));
        sim.Tick();
        sim.Enqueue(new PlaceBuilding("warehouse", new Int3(14, 9, 14), 0));
        sim.Tick();
        Assert.NotNull(sim.Buildings.BuildingAt(new Int3(14, 9, 14)));
        Assert.NotNull(sim.Farms.Get(new Int3(12, 8, 12)));
        PlanResult Plan(Int3 c, IReadOnlyList<Int3>? pending = null) => BlockPlans.CanPlan(sim, c, pending);

        Assert.Equal(PlanResult.OutOfWorld, Plan(new Int3(-1, 9, 5)));
        Assert.Equal(PlanResult.OutOfWorld, Plan(new Int3(5, 0, 5)));
        Assert.Equal(PlanResult.Solid, Plan(new Int3(5, 8, 5)));
        Assert.Equal(PlanResult.Solid, Plan(new Int3(20, 9, 20)));      // a complete hub sets BuildingSolid
        Assert.Equal(PlanResult.Building, Plan(new Int3(14, 9, 14)));   // blueprint footprint (air)
        Assert.Equal(PlanResult.Building, Plan(new Int3(21, 9, 19)));
        Assert.Equal(PlanResult.Plant, Plan(new Int3(3, 9, 3)));
        Assert.Equal(PlanResult.Farm, Plan(new Int3(12, 9, 12)));
        Assert.Equal(PlanResult.Unsupported, Plan(new Int3(5, 11, 5)));
        Assert.Equal(PlanResult.Ok, Plan(new Int3(10, 9, 10)));   // water in the cell is fine
        Assert.Equal(PlanResult.Ok, Plan(new Int3(5, 9, 5)));

        // Supported through the command's own pending cells: a column rising from the ground.
        var column = new List<Int3> { new(5, 9, 5), new(5, 10, 5), new(5, 11, 5) };
        Assert.Equal(PlanResult.Ok, Plan(new Int3(5, 11, 5), column));
        // ... and through an existing entry (either state).
        sim.Enqueue(new DesignateBuild(BuildShape.Single, new(7, 9, 7), new(7, 9, 7), 1, BlockId.Masonry, true));
        sim.Tick();
        Assert.Equal(PlanResult.Unsupported, Plan(new Int3(7, 11, 7)));
        Assert.Equal(PlanResult.Ok, Plan(new Int3(7, 10, 7)));
        // A floating pending pair stays unsupported.
        Assert.Equal(PlanResult.Unsupported, Plan(new Int3(9, 12, 9), new List<Int3> { new(9, 12, 9), new(9, 13, 9) }));
    }

    [Fact]
    public void BuildingPlacement_OverPlanEntry_PlannedBlocks()
    {
        var sim = new ScenarioBuilder().Ground(8)
            .FillBox(new Int3(3, 8, 3), new Int3(6, 8, 6), BlockId.Dirt)
            .Build();
        var warehouse = TestContent.Db.Building("warehouse");   // footprint 2x2x2, entrance (0,0,-1)
        var origin = new Int3(10, 9, 10);
        Assert.Equal(PlacementResult.Ok, sim.Buildings.CanPlace(warehouse, origin, 0));

        sim.Enqueue(new DesignateBuild(BuildShape.Single, new(11, 9, 11), new(11, 9, 11), 1, BlockId.Masonry, true));
        sim.Tick();
        Assert.Equal(PlacementResult.PlannedBlocks, sim.Buildings.CanPlace(warehouse, origin, 0));
        Assert.Equal(PlacementResult.PlannedBlocks, sim.Buildings.CanPlace(warehouse, origin + new Int3(1, 0, 2), 0)); // entrance (11,9,11)
        // Overlaps comes first.
        var hubbed = new ScenarioBuilder().Ground(8).Hub(new Int3(10, 9, 10)).Build();
        hubbed.Enqueue(new DesignateBuild(BuildShape.Single, new(9, 9, 10), new(9, 9, 10), 1, BlockId.Masonry, true));
        hubbed.Tick();
        Assert.Equal(PlacementResult.Overlaps, hubbed.Buildings.CanPlace(warehouse, new Int3(9, 9, 10), 0));

        // DesignateFarm skips a tile whose cell above holds an entry.
        sim.Enqueue(new DesignateBuild(BuildShape.Single, new(4, 9, 4), new(4, 9, 4), 1, BlockId.Planks, true));
        sim.Tick();
        sim.Enqueue(new DesignateFarm(3, 3, 6, 6));
        sim.Tick();
        Assert.Null(sim.Farms.Get(new Int3(4, 8, 4)));
        Assert.Equal(BlockId.Dirt, sim.World.GetBlock(new Int3(4, 8, 4)));
        Assert.NotNull(sim.Farms.Get(new Int3(5, 8, 4)));
        Assert.Equal(15, sim.Farms.Count);
    }

    [Fact]
    public void PlanEntries_SavedAndHashed()
    {
        Assert.Equal(9, SaveGame.FormatVersion);   // 6 in M8-T2; 7 since M11-T10 (block forms); 8 since M11-T4 (orders); 9 since M11-T5 (trader)
        Simulation Fresh() => new ScenarioBuilder().Ground(8).Agent(new Int3(3, 9, 3)).Build();
        var empty = Fresh();
        var baseHash = empty.StateHash();
        empty.Plans.Clear();
        Assert.Equal(baseHash, empty.StateHash());   // an empty store adds nothing

        var a = Fresh(); var b = Fresh(); var c = Fresh();
        var cell = new Int3(10, 9, 10);
        a.Plans.Set(cell, new PlanEntry(BlockId.Masonry, PlanState.Planned));
        b.Plans.Set(cell, new PlanEntry(BlockId.Planks, PlanState.Planned));
        c.Plans.Set(cell, new PlanEntry(BlockId.Masonry, PlanState.Released));
        var hashes = new[] { baseHash, a.StateHash(), b.StateHash(), c.StateHash() };
        Assert.Equal(4, hashes.Distinct().Count());

        a.Plans.Set(new Int3(11, 9, 10), new PlanEntry(BlockId.PolishedStone, PlanState.Released));
        byte[] bytes;
        using (var ms = new MemoryStream()) { SaveGame.Save(a, ms); bytes = ms.ToArray(); }
        Simulation loaded;
        using (var ms = new MemoryStream(bytes)) loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(a.StateHash(), loaded.StateHash());
        Assert.Equal(a.Plans.All.ToList(), loaded.Plans.All.ToList());

        var v5 = (byte[])bytes.Clone();
        BitConverter.GetBytes(5).CopyTo(v5, 4);
        var ex = Assert.Throws<InvalidDataException>(() => SaveGame.Load(new MemoryStream(v5), TestContent.Db));
        Assert.Contains("version 5", ex.Message);
    }
}
