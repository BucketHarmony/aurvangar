using Colony.Sim.Core;
using Colony.Sim.World;

namespace Colony.Sim.Actions;

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

/// <summary>The ONLY mutation path for work done by an actor (ARCH-07). Jobs call this; a future player avatar calls
/// this. Every method validates reach (26-neighborhood of the actor's cell) and preconditions first. M4-T5.</summary>
public sealed class WorldActions
{
    private readonly Simulation _sim;

    public WorldActions(Simulation sim) { _sim = sim; }

    /// <summary>Solid → Air. Spawns the block's drop as an item pile at the cell. Emits ChunkDirty via world dirtying.</summary>
    public ActionResult Dig(AgentId actor, Int3 cell) => throw NotYet();

    /// <summary>Removes a marked tree, drops 4 logs at its base (ECO-09).</summary>
    public ActionResult Chop(AgentId actor, PlantId tree) => throw NotYet();

    public ActionResult PlaceBlock(AgentId actor, Int3 cell, BlockId block) => throw NotYet();

    public ActionResult PickUp(AgentId actor, Int3 pileCell, ItemId item, int count) => throw NotYet();

    public ActionResult PickUpFromStorage(AgentId actor, BuildingId storage, ItemId item, int count) => throw NotYet();

    /// <summary>Drops the carried stack as a pile at (or near, ECO-08) the given cell.</summary>
    public ActionResult Drop(AgentId actor, Int3 cell) => throw NotYet();

    /// <summary>Delivers the carried stack to a storage building or construction site.</summary>
    public ActionResult DeliverTo(AgentId actor, BuildingId building) => throw NotYet();

    /// <summary>Eat or drink one unit from a storage building (ECO-05).</summary>
    public ActionResult Consume(AgentId actor, BuildingId storage, ItemId item) => throw NotYet();

    private static NotImplementedException NotYet() => new("M4-T5: WorldActions (docs/01-architecture.md ARCH-07)");
}
