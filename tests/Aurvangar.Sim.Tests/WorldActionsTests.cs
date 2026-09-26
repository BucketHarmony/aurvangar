using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>ARCH-07 action API (M4-T5): reach, preconditions, results and effects of every WorldActions method.</summary>
public class WorldActionsTests
{
    private static readonly Int3 Start = new(5, 5, 5);
    private static readonly Int3 East1 = new(6, 5, 5);

    private static (Simulation Sim, Agent Agent) Flat(Action<ScenarioBuilder>? extra = null)
    {
        var b = new ScenarioBuilder().Ground(4).Agent(Start);
        extra?.Invoke(b);
        var sim = b.Build();
        return (sim, sim.Agents.All.First());
    }

    private static ItemId Item(string key) => TestContent.Db.Item(key);

    private static Building PlaceStorage(Simulation sim, string def, Int3 origin) =>
        sim.Buildings.PlacePrebuilt(TestContent.Db.Building(def), origin, 0);

    // ---- Dig ----

    [Fact]
    public void Dig_OutOfReach_Rejected()
    {
        var far = new Int3(8, 5, 5);
        var (sim, a) = Flat(b => b.FillBox(far, far, BlockId.Stone));
        Assert.Equal(ActionResult.OutOfReach, sim.Actions.Dig(a.Id, far));
        Assert.Equal(BlockId.Stone, sim.World.GetBlock(far));
        Assert.Equal(0, sim.Piles.Count);
        Assert.Empty(sim.World.ChangedCells);
    }

    [Fact]
    public void Dig_Stone_DropsStonePile()
    {
        var (sim, a) = Flat(b => b.FillBox(East1, East1, BlockId.Stone));
        sim.Events.Drain();
        Assert.Equal(ActionResult.Ok, sim.Actions.Dig(a.Id, East1));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(East1));
        Assert.Equal(new ItemStack(Item("stone"), 1), sim.Piles.At(East1));
        Assert.Equal(1, sim.Piles.Count);
        Assert.Contains(sim.World.Index(East1), sim.World.ChangedCells);
        Assert.Contains(new ItemPileChanged(East1), sim.Events.Pending);
    }

    [Fact]
    public void Dig_Diagonal3D_InReach()
    {
        // 26-neighborhood: the diagonal cell one below is in reach.
        var target = new Int3(6, 4, 6);
        var (sim, a) = Flat();
        Assert.Equal(ActionResult.Ok, sim.Actions.Dig(a.Id, target));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(target));
    }

    [Fact]
    public void Dig_Bedrock_InvalidTarget()
    {
        var sim = new ScenarioBuilder().Ground(0).Agent(new Int3(5, 1, 5)).Build();
        var a = sim.Agents.All.First();
        var target = new Int3(6, 0, 5);
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.Dig(a.Id, target));
        Assert.Equal(BlockId.Bedrock, sim.World.GetBlock(target));
    }

    [Fact]
    public void Dig_AirAndBuildingSolid_InvalidTarget()
    {
        var wall = new Int3(5, 5, 6);
        var (sim, a) = Flat(b => b.FillBox(wall, wall, BlockId.BuildingSolid));
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.Dig(a.Id, East1));
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.Dig(a.Id, wall));
        Assert.Equal(BlockId.BuildingSolid, sim.World.GetBlock(wall));
    }

    [Fact]
    public void Dig_Dirt_NoPile()
    {
        var (sim, a) = Flat(b => b.FillBox(East1, East1, BlockId.Dirt));
        Assert.Equal(ActionResult.Ok, sim.Actions.Dig(a.Id, East1));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(East1));
        Assert.Equal(0, sim.Piles.Count);
    }

    [Fact]
    public void Dig_FloorOfAnyAgent_Blocked()
    {
        // JOB-09 for the digger; the same rule protects every other agent's floor.
        var (sim, a) = Flat(b => b.Agent(East1));
        var below = Start + Int3.Down;
        var belowOther = East1 + Int3.Down;
        Assert.Equal(ActionResult.Blocked, sim.Actions.Dig(a.Id, below));
        Assert.Equal(ActionResult.Blocked, sim.Actions.Dig(a.Id, belowOther));
        Assert.Equal(BlockId.Stone, sim.World.GetBlock(below));
        Assert.Equal(BlockId.Stone, sim.World.GetBlock(belowOther));
    }

    [Fact]
    public void Dig_UnderTreeBase_Blocked()
    {
        var (sim, a) = Flat(b => b.Layer(new Int3(6, 5, 5), "T"));
        Assert.Equal(ActionResult.Blocked, sim.Actions.Dig(a.Id, East1 + Int3.Down));
        Assert.Equal(BlockId.Stone, sim.World.GetBlock(East1 + Int3.Down));
    }

    [Fact]
    public void DeadOrUnknownActor_Rejected()
    {
        var (sim, a) = Flat(b => b.FillBox(East1, East1, BlockId.Stone));
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.Dig(new AgentId(99), East1));
        a.State = AgentState.Dead;
        Assert.Equal(ActionResult.AgentDead, sim.Actions.Dig(a.Id, East1));
        Assert.Equal(BlockId.Stone, sim.World.GetBlock(East1));
    }

    // ---- Chop ----

    [Fact]
    public void Chop_UnmarkedTree_InvalidTarget()
    {
        var (sim, a) = Flat(b => b.Layer(new Int3(6, 5, 5), "T"));
        var tree = sim.Plants.All.First();
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.Chop(a.Id, tree.Id));
        Assert.NotNull(sim.Plants.Get(tree.Id));
        Assert.Equal(0, sim.Piles.Count);

        tree.MarkedForChop = true;
        Assert.Equal(ActionResult.Ok, sim.Actions.Chop(a.Id, tree.Id));
        Assert.Null(sim.Plants.Get(tree.Id));
        Assert.Equal(new ItemStack(Item("log"), 4), sim.Piles.At(East1));
        for (int h = 0; h < 4; h++) Assert.False(sim.Plants.IsOccupied(East1 + Int3.Up * h));
        Assert.True(sim.PathGrid.IsStandable(East1));
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.Chop(a.Id, tree.Id)); // gone
    }

    [Fact]
    public void Chop_OutOfReachAndBush_Rejected()
    {
        var (sim, a) = Flat(b => b.Layer(new Int3(8, 5, 5), "T.b"));
        var tree = sim.Plants.All.First(p => p.Kind == Plants.PlantKind.Tree);
        var bush = sim.Plants.All.First(p => p.Kind == Plants.PlantKind.Bush);
        tree.MarkedForChop = true;
        bush.MarkedForChop = true;
        Assert.Equal(ActionResult.OutOfReach, sim.Actions.Chop(a.Id, tree.Id));
        a.Cell = new Int3(9, 5, 5);
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.Chop(a.Id, bush.Id));
        Assert.Equal(2, sim.Plants.Count);
    }

    // ---- PlaceBlock ----

    [Fact]
    public void PlaceBlock_AirCell_Ok()
    {
        var (sim, a) = Flat();
        Assert.Equal(ActionResult.Ok, sim.Actions.PlaceBlock(a.Id, East1, BlockId.Dirt));
        Assert.Equal(BlockId.Dirt, sim.World.GetBlock(East1));
        Assert.Contains(sim.World.Index(East1), sim.World.ChangedCells);
    }

    [Fact]
    public void PlaceBlock_AgentCellOrHeadroom_Blocked()
    {
        var other = new Int3(6, 5, 6);
        var (sim, a) = Flat(b => b.Agent(other));
        Assert.Equal(ActionResult.Blocked, sim.Actions.PlaceBlock(a.Id, Start, BlockId.Stone));
        Assert.Equal(ActionResult.Blocked, sim.Actions.PlaceBlock(a.Id, Start + Int3.Up, BlockId.Stone));
        Assert.Equal(ActionResult.Blocked, sim.Actions.PlaceBlock(a.Id, other, BlockId.Stone));

        // A moving agent also holds the cell it is stepping into.
        var b2 = sim.Agents.All.Last();
        b2.NextCell = new Int3(6, 5, 4);
        Assert.Equal(ActionResult.Blocked, sim.Actions.PlaceBlock(a.Id, new Int3(6, 5, 4), BlockId.Stone));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(Start));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(other));
    }

    [Fact]
    public void PlaceBlock_PileTreeSolidOrBadBlock_Rejected()
    {
        var treeCell = new Int3(4, 5, 5);
        var (sim, a) = Flat(b => b.Layer(treeCell, "T"));
        sim.Piles.Add(East1, Item("log"), 3);
        Assert.Equal(ActionResult.Blocked, sim.Actions.PlaceBlock(a.Id, East1, BlockId.Stone));
        Assert.Equal(ActionResult.Blocked, sim.Actions.PlaceBlock(a.Id, treeCell, BlockId.Stone));
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.PlaceBlock(a.Id, Start + Int3.Down, BlockId.Dirt));
        var free = new Int3(5, 5, 6);
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.PlaceBlock(a.Id, free, BlockId.Air));
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.PlaceBlock(a.Id, free, BlockId.Bedrock));
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.PlaceBlock(a.Id, free, BlockId.BuildingSolid));
        Assert.Equal(ActionResult.OutOfReach, sim.Actions.PlaceBlock(a.Id, new Int3(8, 5, 5), BlockId.Stone));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(free));
    }

    // ---- PickUp / Drop ----

    [Fact]
    public void PickUp_MixedItem_WrongItem()
    {
        var (sim, a) = Flat();
        sim.Piles.Add(East1, Item("stone"), 3);
        a.Carried = new ItemStack(Item("log"), 2);
        Assert.Equal(ActionResult.WrongItem, sim.Actions.PickUp(a.Id, East1, Item("stone"), 1));
        Assert.Equal(new ItemStack(Item("log"), 2), a.Carried);
        Assert.Equal(new ItemStack(Item("stone"), 3), sim.Piles.At(East1));
    }

    [Fact]
    public void PickUp_OverCapacity_InventoryFull()
    {
        var (sim, a) = Flat();
        sim.Piles.Add(East1, Item("stone"), 5);
        a.Carried = new ItemStack(Item("stone"), 8);
        Assert.Equal(ActionResult.InventoryFull, sim.Actions.PickUp(a.Id, East1, Item("stone"), 3));
        Assert.Equal(new ItemStack(Item("stone"), 5), sim.Piles.At(East1));
        Assert.Equal(ActionResult.Ok, sim.Actions.PickUp(a.Id, East1, Item("stone"), 2));
        Assert.Equal(new ItemStack(Item("stone"), Agent.CarryCapacity), a.Carried);
        Assert.Equal(new ItemStack(Item("stone"), 3), sim.Piles.At(East1));
    }

    [Fact]
    public void PickUp_WholePile_RemovesIt_AndChecksCountAndItem()
    {
        var (sim, a) = Flat();
        sim.Piles.Add(East1, Item("log"), 4);
        Assert.Equal(ActionResult.NotEnoughItems, sim.Actions.PickUp(a.Id, East1, Item("log"), 5));
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.PickUp(a.Id, East1, Item("stone"), 1));
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.PickUp(a.Id, East1, Item("log"), 0));
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.PickUp(a.Id, new Int3(5, 5, 6), Item("log"), 1));
        Assert.Equal(ActionResult.Ok, sim.Actions.PickUp(a.Id, East1, Item("log"), 4));
        Assert.Equal(new ItemStack(Item("log"), 4), a.Carried);
        Assert.True(sim.Piles.At(East1).IsEmpty);
        Assert.Equal(0, sim.Piles.Count);

        sim.Piles.Add(new Int3(8, 5, 5), Item("log"), 1);
        Assert.Equal(ActionResult.OutOfReach, sim.Actions.PickUp(a.Id, new Int3(8, 5, 5), Item("log"), 1));
    }

    [Fact]
    public void Drop_OnFreeOrSameItemCell_Merges()
    {
        var (sim, a) = Flat();
        Assert.Equal(ActionResult.InventoryEmpty, sim.Actions.Drop(a.Id, Start));
        a.Carried = new ItemStack(Item("stone"), 3);
        Assert.Equal(ActionResult.Ok, sim.Actions.Drop(a.Id, Start));
        Assert.True(a.Carried.IsEmpty);
        a.Carried = new ItemStack(Item("stone"), 2);
        Assert.Equal(ActionResult.Ok, sim.Actions.Drop(a.Id, Start));
        Assert.Equal(new ItemStack(Item("stone"), 5), sim.Piles.At(Start));
        Assert.Equal(1, sim.Piles.Count);
    }

    [Fact]
    public void Drop_OnOtherItemPile_UsesNearestFreeStandableCell()
    {
        // ECO-08: the log pile holds East1; the stone goes to the first free standable cell in spiral order.
        var (sim, a) = Flat();
        sim.Piles.Add(East1, Item("log"), 2);
        a.Carried = new ItemStack(Item("stone"), 3);
        Assert.Equal(ActionResult.Ok, sim.Actions.Drop(a.Id, East1));
        Assert.True(a.Carried.IsEmpty);
        Assert.Equal(new ItemStack(Item("log"), 2), sim.Piles.At(East1));
        Assert.Equal(new ItemStack(Item("stone"), 3), sim.Piles.At(new Int3(6, 5, 4)));
        Assert.Equal(2, sim.Piles.Count);
    }

    [Fact]
    public void Drop_IntoHeadroomCell_LandsOnStandableCell()
    {
        // The cell above the agent is not standable (no floor): the pile goes to the first standable spiral cell,
        // one level down from the target, which is the agent's own cell.
        var (sim, a) = Flat();
        a.Carried = new ItemStack(Item("log"), 2);
        Assert.Equal(ActionResult.Ok, sim.Actions.Drop(a.Id, Start + Int3.Up));
        Assert.True(sim.Piles.At(Start + Int3.Up).IsEmpty);
        Assert.Equal(new ItemStack(Item("log"), 2), sim.Piles.At(Start));
        foreach (var (cell, _) in sim.Piles.All) Assert.True(sim.PathGrid.IsStandable(cell));
    }

    [Fact]
    public void Dig_UnderBuilding_Blocked()
    {
        var wall = new Int3(6, 5, 5);
        var (sim, a) = Flat(b => b.FillBox(wall, wall, BlockId.BuildingSolid));
        Assert.Equal(ActionResult.Blocked, sim.Actions.Dig(a.Id, wall + Int3.Down));
        Assert.Equal(BlockId.Stone, sim.World.GetBlock(wall + Int3.Down));
    }

    [Fact]
    public void Drop_NoFreeCellWithinRadius3_Blocked()
    {
        // A one-cell pit walled in by stone: nothing else within radius 3 is standable.
        var sim = new ScenarioBuilder().Ground(10).FillBox(Start, Start + Int3.Up, BlockId.Air).Agent(Start).Build();
        var a = sim.Agents.All.First();
        sim.Piles.Add(Start, Item("log"), 1);
        a.Carried = new ItemStack(Item("stone"), 3);
        Assert.Equal(ActionResult.Blocked, sim.Actions.Drop(a.Id, Start));
        Assert.Equal(new ItemStack(Item("stone"), 3), a.Carried);
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.Drop(a.Id, Start + Int3.Down));
    }

    // ---- Storage ----

    private static (Simulation Sim, Agent Agent, Building Hub) WithHub()
    {
        // Hub footprint x 10..12, y 5..6, z 10..12; entrance (11,5,9). The agent stands on the entrance.
        var sim = new ScenarioBuilder().Ground(4).Agent(new Int3(11, 5, 9)).Build();
        var hub = PlaceStorage(sim, "hub", new Int3(10, 5, 10));
        return (sim, sim.Agents.All.First(), hub);
    }

    [Fact]
    public void PickUpFromStorage_TakesItems()
    {
        var (sim, a, hub) = WithHub();
        hub.Stored[Item("log").Value] = 5;
        Assert.Equal(ActionResult.NotEnoughItems, sim.Actions.PickUpFromStorage(a.Id, hub.Id, Item("log"), 6));
        Assert.Equal(ActionResult.NotEnoughItems, sim.Actions.PickUpFromStorage(a.Id, hub.Id, Item("stone"), 1));
        Assert.Equal(ActionResult.Ok, sim.Actions.PickUpFromStorage(a.Id, hub.Id, Item("log"), 5));
        Assert.Equal(new ItemStack(Item("log"), 5), a.Carried);
        Assert.False(hub.Stored.ContainsKey(Item("log").Value));

        hub.Stored[Item("stone").Value] = 20;
        Assert.Equal(ActionResult.WrongItem, sim.Actions.PickUpFromStorage(a.Id, hub.Id, Item("stone"), 1));
        hub.Stored[Item("log").Value] = 10;
        Assert.Equal(ActionResult.InventoryFull, sim.Actions.PickUpFromStorage(a.Id, hub.Id, Item("log"), 6));
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.PickUpFromStorage(a.Id, new BuildingId(42), Item("log"), 1));

        a.Cell = new Int3(11, 5, 7);
        Assert.Equal(ActionResult.OutOfReach, sim.Actions.PickUpFromStorage(a.Id, hub.Id, Item("stone"), 1));
        Assert.Equal(20, hub.Stored[Item("stone").Value]);
    }

    [Fact]
    public void DeliverTo_Storage_RespectsAcceptsAndCapacity()
    {
        var (sim, a, hub) = WithHub();
        Assert.Equal(ActionResult.InventoryEmpty, sim.Actions.DeliverTo(a.Id, hub.Id));
        a.Carried = new ItemStack(Item("stone"), 4);
        Assert.Equal(ActionResult.Ok, sim.Actions.DeliverTo(a.Id, hub.Id));
        Assert.True(a.Carried.IsEmpty);
        Assert.Equal(4, hub.Stored[Item("stone").Value]);

        // Hub: 100 per item. All-or-nothing: 98 + 4 does not fit.
        hub.Stored[Item("stone").Value] = 98;
        a.Carried = new ItemStack(Item("stone"), 4);
        Assert.Equal(ActionResult.StorageFull, sim.Actions.DeliverTo(a.Id, hub.Id));
        Assert.Equal(new ItemStack(Item("stone"), 4), a.Carried);
        Assert.Equal(98, hub.Stored[Item("stone").Value]);
    }

    [Fact]
    public void DeliverTo_Warehouse_RejectsWater_AndCapsTotal()
    {
        // BLD-11: warehouses reject water. Total capacity 150 across items.
        var sim = new ScenarioBuilder().Ground(4).Agent(new Int3(10, 5, 9)).Build();
        var a = sim.Agents.All.First();
        var wh = PlaceStorage(sim, "warehouse", new Int3(10, 5, 10));
        a.Carried = new ItemStack(Item("water"), 1);
        Assert.Equal(ActionResult.WrongItem, sim.Actions.DeliverTo(a.Id, wh.Id));
        wh.Stored[Item("log").Value] = 100;
        wh.Stored[Item("stone").Value] = 45;
        a.Carried = new ItemStack(Item("berries"), 6);
        Assert.Equal(ActionResult.StorageFull, sim.Actions.DeliverTo(a.Id, wh.Id));
        a.Carried = new ItemStack(Item("berries"), 5);
        Assert.Equal(ActionResult.Ok, sim.Actions.DeliverTo(a.Id, wh.Id));
        Assert.Equal(5, wh.Stored[Item("berries").Value]);
    }

    [Fact]
    public void Consume_RestoresNeed_OneUnit()
    {
        var (sim, a, hub) = WithHub();
        hub.Stored[Item("berries").Value] = 1;
        hub.Stored[Item("water").Value] = 3;
        hub.Stored[Item("log").Value] = 3;
        a.Hunger = 3000;
        a.Thirst = 9000;
        Assert.Equal(ActionResult.Ok, sim.Actions.Consume(a.Id, hub.Id, Item("berries")));   // ECO-05: +2500
        Assert.Equal(5500, a.Hunger);
        Assert.False(hub.Stored.ContainsKey(Item("berries").Value));
        Assert.Equal(ActionResult.NotEnoughItems, sim.Actions.Consume(a.Id, hub.Id, Item("berries")));
        Assert.Equal(ActionResult.Ok, sim.Actions.Consume(a.Id, hub.Id, Item("water")));      // clamps at max
        Assert.Equal(Agent.NeedMax, a.Thirst);
        Assert.Equal(2, hub.Stored[Item("water").Value]);
        Assert.Equal(ActionResult.WrongItem, sim.Actions.Consume(a.Id, hub.Id, Item("log")));
        Assert.Equal(3, hub.Stored[Item("log").Value]);
    }

    // ---- Work ----

    [Fact]
    public void Work_ChecksReach()
    {
        var (sim, a, hub) = WithHub();
        Assert.Equal(ActionResult.Ok, sim.Actions.Work(a.Id, WorkTarget.AtCell(new Int3(12, 5, 8))));
        Assert.Equal(ActionResult.OutOfReach, sim.Actions.Work(a.Id, WorkTarget.AtCell(new Int3(14, 5, 9))));
        Assert.Equal(ActionResult.Ok, sim.Actions.Work(a.Id, WorkTarget.AtBuilding(hub.Id)));
        Assert.Equal(ActionResult.InvalidTarget, sim.Actions.Work(a.Id, WorkTarget.AtBuilding(new BuildingId(42))));
    }

    // ---- Hash ----

    [Fact]
    public void Piles_AreInStateHash()
    {
        var (s1, _) = Flat();
        var (s2, _) = Flat();
        Assert.Equal(s1.StateHash(), s2.StateHash());
        s2.Piles.Add(East1, Item("log"), 1);
        Assert.NotEqual(s1.StateHash(), s2.StateHash());
        s1.Piles.Add(East1, Item("log"), 2);
        Assert.NotEqual(s1.StateHash(), s2.StateHash());
    }
}
