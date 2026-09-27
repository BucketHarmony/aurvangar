using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Actions;

public sealed partial class WorldActions
{
    /// <summary>CON-13 (M8-T2): places a construction block from the carried stack. After the actor, reach and target
    /// checks of <see cref="PlaceBlock"/>: Blocked by a building footprint, entrance or stand cell; Blocked by an agent
    /// in the cell or the cell below it, a plant or a pile; Unsupported without a solid block below or beside it
    /// (CON-09); then the carried stack must hold the cost (InventoryEmpty, WrongItem, NotEnoughItems). The cost leaves
    /// the stack and the block is set.</summary>
    private ActionResult PlaceBuilt(Agent a, Int3 cell, BlockId block)
    {
        if (_sim.Buildings.BuildingAt(cell) is not null || _sim.Buildings.IsEntranceOrStand(cell)) return ActionResult.Blocked;
        if (AgentHolds(cell) || AgentHolds(cell + Int3.Down) || _sim.Plants.IsOccupied(cell) || !_sim.Piles.At(cell).IsEmpty)
            return ActionResult.Blocked;
        if (!Blocks.Support.Placement(_sim.World, cell)) return ActionResult.Unsupported;
        var (item, cost) = _sim.Content.CostOf(block);
        if (a.Carried.IsEmpty) return ActionResult.InventoryEmpty;
        if (a.Carried.Item != item) return ActionResult.WrongItem;
        if (a.Carried.Count < cost) return ActionResult.NotEnoughItems;

        a.Carried = a.Carried.Count == cost ? ItemStack.Empty : new ItemStack(item, a.Carried.Count - cost);
        _sim.World.SetBlock(cell, block);
        return ActionResult.Ok;
    }
}
