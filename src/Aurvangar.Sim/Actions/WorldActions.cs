using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Actions;

public enum ActionResult : byte
{
    Ok,
    OutOfReach,
    InvalidTarget,
    Blocked,
    InventoryFull,
    InventoryEmpty,
    NotEnoughItems,
    StorageFull,
    WrongItem,
    AgentDead,
}

/// <summary>What a <see cref="WorldActions.Work"/> tick is spent on: a cell (dig, chop, plant, harvest) or a building
/// (construct, deconstruct, pump).</summary>
public readonly record struct WorkTarget(Int3 Cell, BuildingId Building)
{
    public static WorkTarget AtCell(Int3 cell) => new(cell, default);
    public static WorkTarget AtBuilding(BuildingId building) => new(Int3.Zero, building);
    public bool IsBuilding => Building.IsValid;
}

/// <summary>The ONLY mutation path for work done by an actor (ARCH-07, ADR-027). Jobs call this; a future player
/// avatar calls this. Every method checks, in order: the actor exists (else InvalidTarget) and is alive (AgentDead);
/// for id targets (plant, building) that the target exists; reach (the target is the actor's cell or in its
/// 26-neighborhood; for a building, any footprint cell); then the target cell's contents and other preconditions.
/// So an out-of-reach cell target is OutOfReach whatever it holds. A non-Ok result changes nothing.</summary>
public sealed partial class WorldActions
{
    /// <summary>ECO-08 search radius for a pile that cannot go on its target cell.</summary>
    public const int PileSearchRadius = 3;

    /// <summary>ECO-09.</summary>
    public const int LogsPerTree = 4;

    private readonly Simulation _sim;

    public WorldActions(Simulation sim) { _sim = sim; }

    /// <summary>Solid, diggable (WLD-05) → Air. Spawns the block's drop as an item pile at the cell. The world records
    /// the change (ChangedCells, chunk dirtying), which water and paths consume. Blocked when the cell is the floor
    /// of an agent (JOB-09 and every other agent), of a plant, or of a building (BuildingSolid above).</summary>
    public ActionResult Dig(AgentId actor, Int3 cell)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        if (!InReach(a.Cell, cell)) return ActionResult.OutOfReach;
        var world = _sim.World;
        if (!world.InBounds(cell) || cell.Y == 0) return ActionResult.InvalidTarget;
        var def = _sim.Content.Block(world.GetBlock(cell));
        if (!def.Solid || !def.Diggable) return ActionResult.InvalidTarget;
        var above = cell + Int3.Up;
        if (AgentHolds(above) || _sim.Plants.IsOccupied(above) || world.GetBlock(above) == BlockId.BuildingSolid)
            return ActionResult.Blocked;

        world.SetBlock(cell, BlockId.Air);
        // A solid cell never holds a pile, so the drop always fits on the dug cell.
        if (def.Drop is not null) _sim.Piles.Add(cell, _sim.Content.Item(def.Drop), 1);
        return ActionResult.Ok;
    }

    /// <summary>Removes a tree marked for chopping; drops 4 logs at its base (ECO-09). Reach is to the base cell.</summary>
    public ActionResult Chop(AgentId actor, PlantId tree)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        var p = _sim.Plants.Get(tree);
        if (p is null || p.Kind != PlantKind.Tree) return ActionResult.InvalidTarget;
        if (!InReach(a.Cell, p.Base)) return ActionResult.OutOfReach;
        if (!p.MarkedForChop) return ActionResult.InvalidTarget;

        _sim.Plants.Remove(p.Id);
        // The base held the trunk, so it holds no pile: the logs always fit there.
        _sim.Piles.Add(p.Base, _sim.Content.Item("log"), LogsPerTree);
        return ActionResult.Ok;
    }

    /// <summary>Air → a natural block (a solid, diggable type). Blocked by an agent standing in or stepping into the
    /// cell or the cell below it (its headroom), a plant, or a pile. Materials are not consumed (ADR-027).</summary>
    public ActionResult PlaceBlock(AgentId actor, Int3 cell, BlockId block)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        if (!InReach(a.Cell, cell)) return ActionResult.OutOfReach;
        var world = _sim.World;
        if (!world.InBounds(cell) || world.IsSolid(cell)) return ActionResult.InvalidTarget;
        if ((int)block >= _sim.Content.Blocks.Count) return ActionResult.InvalidTarget;
        var def = _sim.Content.Block(block);
        if (!def.Solid || !def.Diggable) return ActionResult.InvalidTarget;
        if (AgentHolds(cell) || AgentHolds(cell + Int3.Down) || _sim.Plants.IsOccupied(cell) || !_sim.Piles.At(cell).IsEmpty)
            return ActionResult.Blocked;

        world.SetBlock(cell, block);
        return ActionResult.Ok;
    }

    /// <summary>Takes <paramref name="count"/> of the pile's item into the carried stack (one item type, cap 10).</summary>
    public ActionResult PickUp(AgentId actor, Int3 pileCell, ItemId item, int count)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        if (!InReach(a.Cell, pileCell)) return ActionResult.OutOfReach;
        var pile = _sim.Piles.At(pileCell);
        if (pile.IsEmpty || pile.Item != item || count <= 0) return ActionResult.InvalidTarget;
        r = CanCarry(a, item, count);
        if (r != ActionResult.Ok) return r;
        if (pile.Count < count) return ActionResult.NotEnoughItems;

        _sim.Piles.Take(pileCell, count);
        a.Carried = new ItemStack(item, a.Carried.Count + count);
        return ActionResult.Ok;
    }

    /// <summary>Drops the whole carried stack as a pile at the cell, or (ECO-08) at the nearest free standable cell
    /// within radius 3 when the cell is not standable or holds another item. Blocked when there is no such cell.</summary>
    public ActionResult Drop(AgentId actor, Int3 cell)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        if (!InReach(a.Cell, cell)) return ActionResult.OutOfReach;
        var world = _sim.World;
        if (!world.InBounds(cell) || world.IsSolid(cell)) return ActionResult.InvalidTarget;
        if (a.Carried.IsEmpty) return ActionResult.InventoryEmpty;
        var item = a.Carried.Item;
        if (!FindPileCell(cell, item, out var at)) return ActionResult.Blocked;

        _sim.Piles.Add(at, item, a.Carried.Count);
        a.Carried = ItemStack.Empty;
        return ActionResult.Ok;
    }

    /// <summary>One tick of work on a cell or building. Validates the actor and reach; the step's progress lives on
    /// the agent (JOB-01). Construction progress (BLD-08) and pump cycles (BLD-13) are added here by M5-T2 / M5-T4.</summary>
    public ActionResult Work(AgentId actor, WorkTarget target)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        if (!target.IsBuilding) return InReach(a.Cell, target.Cell) ? ActionResult.Ok : ActionResult.OutOfReach;
        var b = _sim.Buildings.Get(target.Building);
        if (b is null) return ActionResult.InvalidTarget;
        return InReach(a.Cell, b) ? ActionResult.Ok : ActionResult.OutOfReach;
    }

    // ---- helpers ----

    /// <summary>The acting agent, or why it cannot act.</summary>
    private ActionResult Actor(AgentId id, out Agent agent)
    {
        var found = _sim.Agents.Get(id);
        agent = found!;
        if (found is null) return ActionResult.InvalidTarget;
        return agent.IsAlive ? ActionResult.Ok : ActionResult.AgentDead;
    }

    /// <summary>ARCH-07 reach: the target is the actor's cell or one of its 26 neighbors.</summary>
    public static bool InReach(Int3 from, Int3 target) =>
        Math.Abs(target.X - from.X) <= 1 && Math.Abs(target.Y - from.Y) <= 1 && Math.Abs(target.Z - from.Z) <= 1;

    /// <summary>Reach to a building: any footprint cell is in reach.</summary>
    public static bool InReach(Int3 from, Building b)
    {
        foreach (var c in b.FootprintCells())
            if (InReach(from, c)) return true;
        return false;
    }

    /// <summary>A living agent stands in the cell or is stepping into it.</summary>
    private bool AgentHolds(Int3 c)
    {
        foreach (var a in _sim.Agents.All)
            if (a.IsAlive && (a.Cell == c || a.NextCell == c)) return true;
        return false;
    }

    /// <summary>Carried stack can take <paramref name="count"/> more of <paramref name="item"/> (JOB-01).</summary>
    private static ActionResult CanCarry(Agent a, ItemId item, int count)
    {
        if (!a.Carried.IsEmpty && a.Carried.Item != item) return ActionResult.WrongItem;
        int have = a.Carried.IsEmpty ? 0 : a.Carried.Count;
        return have + count > Agent.CarryCapacity ? ActionResult.InventoryFull : ActionResult.Ok;
    }
}
