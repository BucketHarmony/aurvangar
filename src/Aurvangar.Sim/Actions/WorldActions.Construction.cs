using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Actions;

/// <summary>M5-T2 construction actions (BLD-06..09, ADR-041): delivering materials to a site, construction and
/// deconstruction work, and pile placement for items that no agent carries (refunds, piles pushed out of a site).</summary>
public sealed partial class WorldActions
{
    /// <summary>BLD-06/07: delivers carried cost items to a blueprint or construction site, at most what is still
    /// needed (the rest stays carried). The first delivery starts construction (<see cref="Construction.Start"/>);
    /// the last one posts the Construct job at once.
    /// WrongItem when the item is not in the cost or none is needed any more; Blocked while the site waits for the
    /// building below it (BLD-04).</summary>
    private ActionResult DeliverToSite(Agent a, Building b)
    {
        if (!InReach(a.Cell, b)) return ActionResult.OutOfReach;
        if (a.Carried.IsEmpty) return ActionResult.InventoryEmpty;
        var item = a.Carried.Item;
        int need = Construction.Remaining(_sim, b, item);
        if (need <= 0) return ActionResult.WrongItem;
        if (Construction.WaitsForBelow(_sim, b)) return ActionResult.Blocked;

        int n = Math.Min(need, a.Carried.Count);
        b.Delivered[item.Value] = (b.Delivered.TryGetValue(item.Value, out var had) ? had : 0) + n;
        a.Carried = a.Carried.Count == n ? ItemStack.Empty : new ItemStack(item, a.Carried.Count - n);
        if (b.State == BuildingState.Blueprint) Construction.Start(_sim, b);
        if (Construction.FullyDelivered(_sim, b)) Construction.PostConstruct(_sim, b);   // BLD-08 work can start now
        return ActionResult.Ok;
    }

    /// <summary>One tick of work on a building. A construction site needs every cost item delivered (else
    /// InvalidTarget) and completes at BuildTicks (BLD-08). A building being deconstructed is removed after
    /// half its build ticks (BLD-09); that last tick is Blocked while an agent holds a cell on top of it. A
    /// blueprint takes no work (InvalidTarget); a complete producer runs its cycle (<see cref="PumpTick"/>, BLD-13), any
    /// other complete building accepts the tick and nothing happens.</summary>
    private ActionResult WorkOnBuilding(Agent a, Building b)
    {
        if (!InReach(a.Cell, b)) return ActionResult.OutOfReach;
        switch (b.State)
        {
            case BuildingState.UnderConstruction:
                if (!Construction.FullyDelivered(_sim, b)) return ActionResult.InvalidTarget;
                if (++b.Progress >= b.Def.BuildTicks) Complete(b);
                return ActionResult.Ok;
            case BuildingState.Deconstructing:
                if (b.Progress + 1 >= Construction.DeconstructTicks(b.Def) && HoldsTop(b)) return ActionResult.Blocked;
                if (++b.Progress >= Construction.DeconstructTicks(b.Def)) TearDown(b);
                return ActionResult.Ok;
            case BuildingState.Complete:
                return b.Def.Producer is { } producer ? PumpTick(b, producer) : ActionResult.Ok;
            default:
                return ActionResult.InvalidTarget;
        }
    }

    /// <summary>BLD-08: the site becomes a complete building: walkable blocking ends, BuildingSolid is written
    /// (buildings that set blocks) and the delivered materials are used up.</summary>
    private void Complete(Building b)
    {
        foreach (var c in b.FootprintCells()) _sim.PathGrid.SetSite(c, false);
        if (b.Def.SetsBlocks)
            foreach (var c in b.FootprintCells()) _sim.World.SetBlock(c, BlockId.BuildingSolid);
        b.State = BuildingState.Complete;
        b.Delivered.Clear();
        if (b.Def.Producer is not null) b.Progress = 0;   // from now on Progress counts the production cycle (BLD-13)
        _sim.Events.Emit(new BuildingCompleted(b.Id));
    }

    /// <summary>BLD-13: one work tick of a production cycle. The cycle's progress is on the building (a new worker
    /// carries on). At <c>cycleTicks</c> the cycle ends: with the intake level at least <c>minIntakeLevel</c>,
    /// <c>unitsPerCycle</c> leave the intake cell (<see cref="Water.WaterGrid.Pump"/>) and one output item goes into
    /// the buffer, else nothing is made and the building flags NoWater. StorageFull (nothing changes) while the buffer
    /// is full.</summary>
    private ActionResult PumpTick(Building b, ProducerDef p)
    {
        var output = _sim.Content.Item(p.Output);
        if (StoredCount(b, output) >= p.Buffer) return ActionResult.StorageFull;
        if (++b.Progress < p.CycleTicks) return ActionResult.Ok;
        b.Progress = 0;
        var intake = BuildingShape.Intake(b.Def, b.Origin, b.Rotation);
        b.NoWater = _sim.Water.GetLevel(intake) < p.MinIntakeLevel;
        if (b.NoWater) return ActionResult.Ok;
        _sim.Water.Pump(intake, p.UnitsPerCycle);
        b.Stored[output.Value] = StoredCount(b, output) + 1;
        return ActionResult.Ok;
    }

    /// <summary>BLD-09: the footprint turns to air; half the cost (rounded down) and everything stored are dropped
    /// as piles at the building's stand cell (ECO-08), and the building is removed.</summary>
    private void TearDown(Building b)
    {
        if (b.Def.SetsBlocks)
            foreach (var c in b.FootprintCells()) _sim.World.SetBlock(c, BlockId.Air);
        var stand = Construction.StandCell(_sim, b);
        foreach (var (item, n) in Construction.Cost(_sim, b.Def))
            if (n / 2 > 0) PlacePile(stand, item, n / 2);
        foreach (var (item, n) in b.Stored) PlacePile(stand, new ItemId(item), n);
        b.Stored.Clear();
        _sim.Buildings.Remove(b);
        _sim.Events.Emit(new BuildingRemoved(b.Id));
    }

    /// <summary>A living agent stands in, or steps into, a cell right above the footprint (the worker never does:
    /// JobGoals leaves those cells out for a building being deconstructed).</summary>
    private bool HoldsTop(Building b)
    {
        foreach (var c in b.FootprintCells())
            if (!b.Covers(c + Int3.Up) && _sim.Agents.AnyHolds(c + Int3.Up)) return true;
        return false;
    }

    /// <summary>ECO-08 for items no agent carries: puts them on the cell or the nearest free standable cell within
    /// radius 3. With no such cell they go on the first non-solid cell of the same search that takes the item and lies
    /// outside every building footprint (never lost, so items are conserved).</summary>
    internal void PlacePile(Int3 cell, ItemId item, int count)
    {
        if (count <= 0) return;
        if (!FindPileCell(cell, item, out var at) && !FindAnyPileCell(cell, item, out at))
            throw new InvalidOperationException($"ECO-08: no room for a pile near {cell}");
        _sim.Piles.Add(at, item, count);
    }

    private bool FindAnyPileCell(Int3 target, ItemId item, out Int3 at)
    {
        at = target;
        if (Free(target)) return true;
        foreach (var o in PileSpiral)
        {
            at = target + o;
            if (Free(at)) return true;
        }
        return false;

        // Never inside a building footprint: completing it would write BuildingSolid over the pile.
        bool Free(Int3 c) => _sim.World.InBounds(c) && !_sim.World.IsSolid(c) && _sim.Piles.Accepts(c, item)
            && _sim.Buildings.BuildingAt(c) is null;
    }

    /// <summary>Moves a whole pile to (or near) another cell (BLD-07: piles pushed out of a new site).</summary>
    internal void MovePile(Int3 from, Int3 to)
    {
        var s = _sim.Piles.At(from);
        if (s.IsEmpty) return;
        _sim.Piles.Take(from, s.Count);
        PlacePile(to, s.Item, s.Count);
    }
}
