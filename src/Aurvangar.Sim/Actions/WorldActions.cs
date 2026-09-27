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
    /// <summary>CON-09 (M8-T2): a construction block needs a solid block below it or beside it.</summary>
    Unsupported,
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
    /// of an agent (JOB-09 and every other agent), of a plant, of a building that holds its floor (GRV-05/06: the
    /// Great Hall, a blueprint, a site or a building being deconstructed; the floor of a complete building may be dug,
    /// and the gravity step collapses it once none is left), or of BuildingSolid that belongs to no building, and when
    /// a built block depends on it for support (CON-10, with GRV-09). A built block drops its whole cost (CON-17); a
    /// drop over air falls at the gravity step (GRV-03).</summary>
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
        if (AgentHolds(above) || _sim.Plants.IsOccupied(above)) return ActionResult.Blocked;
        if (_sim.Buildings.BuildingAt(above) is { } over ? Physics.Gravity.HoldsFloor(over)
            : world.GetBlock(above) == BlockId.BuildingSolid)
            return ActionResult.Blocked;
        if (Blocks.Support.Depends(_sim, cell)) return ActionResult.Blocked;   // CON-10: never unground a built block

        var block = world.GetBlock(cell);
        var shape = world.FormAt(cell).Shape;   // read before the write resets it (CON-19)
        world.SetBlock(cell, BlockId.Air);
        // A solid cell never holds a pile, so the drop always fits on the dug cell.
        if (def.IsConstruction)
        {
            var (item, cost) = _sim.Content.CostOf(block, shape);   // CON-17: the whole (shaped) cost comes back as one pile
            _sim.Piles.Add(cell, item, cost);
        }
        else if (def.Drop is not null) _sim.Piles.Add(cell, _sim.Content.Item(def.Drop), 1);
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
    /// cell or the cell below it (its headroom), a plant, or a pile. Materials are not consumed (ADR-027). A construction
    /// block (CON-13) goes to <see cref="PlaceBuilt"/> instead, which takes its cost from the carried stack and sets
    /// <paramref name="form"/> (CON-19). A natural block is always Full; any other form, or a form not in data, is
    /// InvalidTarget.</summary>
    public ActionResult PlaceBlock(AgentId actor, Int3 cell, BlockId block, BlockForm form = default)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        if (!InReach(a.Cell, cell)) return ActionResult.OutOfReach;
        var world = _sim.World;
        if (!world.InBounds(cell) || world.IsSolid(cell)) return ActionResult.InvalidTarget;
        if ((int)block >= _sim.Content.Blocks.Count) return ActionResult.InvalidTarget;
        var def = _sim.Content.Block(block);
        if (!def.Solid || !def.Diggable) return ActionResult.InvalidTarget;
        if (!_sim.Content.IsValidForm(form) || (!def.IsConstruction && !form.IsFull)) return ActionResult.InvalidTarget;
        if (def.IsConstruction) return PlaceBuilt(a, cell, block, form);   // CON-13 (M8-T2), CON-19 (M11-T10)
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
    /// the agent (JOB-01). Building work (construction BLD-08, deconstruction BLD-09) is
    /// <see cref="WorkOnBuilding"/>, which also runs pump cycles (BLD-13).</summary>
    public ActionResult Work(AgentId actor, WorkTarget target)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        if (!target.IsBuilding) return InReach(a.Cell, target.Cell) ? ActionResult.Ok : ActionResult.OutOfReach;
        var b = _sim.Buildings.Get(target.Building);
        if (b is null) return ActionResult.InvalidTarget;
        return WorkOnBuilding(a, b);
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
    private bool AgentHolds(Int3 c) => _sim.Agents.AnyHolds(c);

    /// <summary>Carried stack can take <paramref name="count"/> more of <paramref name="item"/> (JOB-01).</summary>
    private static ActionResult CanCarry(Agent a, ItemId item, int count)
    {
        if (!a.Carried.IsEmpty && a.Carried.Item != item) return ActionResult.WrongItem;
        int have = a.Carried.IsEmpty ? 0 : a.Carried.Count;
        return have + count > Agent.CarryCapacity ? ActionResult.InventoryFull : ActionResult.Ok;
    }
}
