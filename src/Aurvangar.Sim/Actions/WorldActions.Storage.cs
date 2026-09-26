using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Items;

namespace Aurvangar.Sim.Actions;

/// <summary>Storage actions (BLD-10, BLD-11, ECO-05) and ECO-08 pile placement.</summary>
public sealed partial class WorldActions
{
    /// <summary>Takes <paramref name="count"/> of an item from a complete storage building into the carried stack.</summary>
    public ActionResult PickUpFromStorage(AgentId actor, BuildingId storage, ItemId item, int count)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        var b = StorageBuilding(storage);
        if (b is null || !item.IsValid || count <= 0) return ActionResult.InvalidTarget;
        if (!InReach(a.Cell, b)) return ActionResult.OutOfReach;
        r = CanCarry(a, item, count);
        if (r != ActionResult.Ok) return r;
        if (StoredCount(b, item) < count) return ActionResult.NotEnoughItems;

        RemoveStored(b, item, count);
        a.Carried = new ItemStack(item, a.Carried.Count + count);
        return ActionResult.Ok;
    }

    /// <summary>Delivers the whole carried stack to a complete storage building: the building must accept the item
    /// (BLD-11) and have room for all of it under its total and per-item caps, else nothing moves (ADR-027).
    /// Construction-site delivery (BLD-06/07) is added by M5-T2. M4-T8: honor BLD-10 Reserved in/out counts.</summary>
    public ActionResult DeliverTo(AgentId actor, BuildingId building)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        var b = StorageBuilding(building);
        if (b is null) return ActionResult.InvalidTarget;
        if (!InReach(a.Cell, b)) return ActionResult.OutOfReach;
        if (a.Carried.IsEmpty) return ActionResult.InventoryEmpty;
        var item = a.Carried.Item;
        int n = a.Carried.Count;
        if (!Accepts(b, item)) return ActionResult.WrongItem;
        if (FreeCapacity(b, item) < n) return ActionResult.StorageFull;

        b.Stored[item.Value] = StoredCount(b, item) + n;
        a.Carried = ItemStack.Empty;
        return ActionResult.Ok;
    }

    /// <summary>Eat or drink one unit from a storage building (ECO-05); the need is clamped at its maximum.</summary>
    public ActionResult Consume(AgentId actor, BuildingId storage, ItemId item)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        var b = StorageBuilding(storage);
        if (b is null || !item.IsValid || item.Value >= _sim.Content.Items.Count) return ActionResult.InvalidTarget;
        if (!InReach(a.Cell, b)) return ActionResult.OutOfReach;
        var def = _sim.Content.ItemDef(item);
        if (def.Food <= 0 && def.Drink <= 0) return ActionResult.WrongItem;
        if (StoredCount(b, item) < 1) return ActionResult.NotEnoughItems;

        RemoveStored(b, item, 1);
        a.Hunger = Math.Min(Agent.NeedMax, a.Hunger + def.Food);
        a.Thirst = Math.Min(Agent.NeedMax, a.Thirst + def.Drink);
        return ActionResult.Ok;
    }

    // ---- storage helpers ----

    /// <summary>A complete building with storage, or null.</summary>
    private Building? StorageBuilding(BuildingId id)
    {
        var b = _sim.Buildings.Get(id);
        return b is { State: BuildingState.Complete, Def.Storage: not null } ? b : null;
    }

    public static int StoredCount(Building b, ItemId item) => b.Stored.TryGetValue(item.Value, out var n) ? n : 0;

    /// <summary>Removes items; an emptied entry is removed so the hash does not depend on history.</summary>
    private static void RemoveStored(Building b, ItemId item, int count)
    {
        int left = StoredCount(b, item) - count;
        if (left > 0) b.Stored[item.Value] = left;
        else b.Stored.Remove(item.Value);
    }

    private bool Accepts(Building b, ItemId item)
    {
        var key = _sim.Content.ItemDef(item).Id;
        foreach (var k in b.Def.Storage!.Accepts)
            if (string.Equals(k, key, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>Room for <paramref name="item"/> under the per-item cap and the total cap (0 = no cap).</summary>
    public static int FreeCapacity(Building b, ItemId item)
    {
        var s = b.Def.Storage!;
        int free = int.MaxValue;
        if (s.PerItemCapacity > 0) free = s.PerItemCapacity - StoredCount(b, item);
        if (s.Capacity > 0)
        {
            int total = 0;
            foreach (var v in b.Stored.Values) total += v;
            free = Math.Min(free, s.Capacity - total);
        }
        return Math.Max(free, 0);
    }

    // ---- ECO-08 pile placement ----

    /// <summary>Offsets tried after the target cell, nearest first: horizontal Chebyshev ring, then Manhattan
    /// distance in x/z, then same level before one up before one down, then z, then x.</summary>
    private static readonly Int3[] PileSpiral = BuildPileSpiral();

    private static Int3[] BuildPileSpiral()
    {
        var list = new List<Int3>();
        for (int dy = -1; dy <= 1; dy++)
            for (int dz = -PileSearchRadius; dz <= PileSearchRadius; dz++)
                for (int dx = -PileSearchRadius; dx <= PileSearchRadius; dx++)
                    if (dx != 0 || dy != 0 || dz != 0) list.Add(new Int3(dx, dy, dz));
        static int YRank(int dy) => dy == 0 ? 0 : dy > 0 ? 1 : 2;
        return list
            .OrderBy(o => Math.Max(Math.Abs(o.X), Math.Abs(o.Z)))
            .ThenBy(o => Math.Abs(o.X) + Math.Abs(o.Z))
            .ThenBy(o => YRank(o.Y))
            .ThenBy(o => o.Z)
            .ThenBy(o => o.X)
            .ToArray();
    }

    /// <summary>ECO-08: the target cell when it is standable (PTH-01: also plant-free) and holds no other item; else
    /// the first spiral cell that is. Piles therefore always rest on a floor where haulers can stand.</summary>
    private bool FindPileCell(Int3 target, ItemId item, out Int3 at)
    {
        var world = _sim.World;
        if (world.InBounds(target) && _sim.PathGrid.IsStandable(target) && _sim.Piles.Accepts(target, item))
        {
            at = target;
            return true;
        }
        foreach (var o in PileSpiral)
        {
            var c = target + o;
            if (world.InBounds(c) && _sim.PathGrid.IsStandable(c) && _sim.Piles.Accepts(c, item))
            {
                at = c;
                return true;
            }
        }
        at = default;
        return false;
    }
}
