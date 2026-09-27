using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Actions;

/// <summary>M11-T5 trade wagon writes (CRF-17..21, ADR-082): the Pay step, and the trader's arrival and departure.
/// <see cref="Traders"/> decides when; these methods move the items and place or remove the building.</summary>
public sealed partial class WorldActions
{
    /// <summary>CRF-19: pays carried give items into a deal. Checks, in order: the trader is here and complete and the deal
    /// exists and still owes something (InvalidTarget); the agent stands on the trader's stand cell, its entrance
    /// (OutOfReach); it carries the offer's give item (InventoryEmpty, WrongItem). Moves <c>min(carried, unpaid)</c> into
    /// the deal's <c>paid</c>; while a whole lot more is paid than granted, grants it: its goods go into the trader's
    /// storage. Anything left over stays carried.</summary>
    public ActionResult Pay(AgentId actor, BuildingId trader, int deal)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        var visit = _sim.Trader;
        var b = _sim.Buildings.Get(trader);
        if (!visit.IsHere || visit.Building != trader || b is not { State: BuildingState.Complete }) return ActionResult.InvalidTarget;
        if (deal < 0 || deal >= visit.Deals.Count) return ActionResult.InvalidTarget;
        var d = visit.Deals[deal];
        var offer = _sim.Content.Offers[d.Offer];
        int unpaid = d.Lots * offer.GiveCount - d.Paid;
        if (unpaid <= 0) return ActionResult.InvalidTarget;
        if (a.Cell != Construction.StandCell(_sim, b)) return ActionResult.OutOfReach;
        if (a.Carried.IsEmpty) return ActionResult.InventoryEmpty;
        if (a.Carried.Item != offer.Give) return ActionResult.WrongItem;

        int n = Math.Min(a.Carried.Count, unpaid);
        int left = a.Carried.Count - n;
        a.Carried = left > 0 ? new ItemStack(offer.Give, left) : ItemStack.Empty;
        d.Paid += n;
        while (d.Granted < d.Lots && d.Paid >= (d.Granted + 1) * offer.GiveCount)
        {
            d.Granted++;
            b.Stored[offer.Get.Value] = StoredCount(b, offer.Get) + offer.GetCount;
        }
        return ActionResult.Ok;
    }

    /// <summary>CRF-17: places the trader complete beside the Great Hall with the BLD-15 start-site search, or returns
    /// null (no hall, or no site). As a new site does (BLD-07), it clears dig marks on its floor, pushes loose piles out
    /// of its footprint and moves agents in it to its stand cell.</summary>
    internal Building? TraderArrive(BuildingDef def)
    {
        var hall = Jobs.DigStrand.Hall(_sim);
        if (hall is null) return null;
        var b = WorldFactory.PlaceBesideHall(_sim, hall, def);
        if (b is null) return null;
        Construction.ClearGroundMarks(_sim, b);
        var stand = Construction.StandCell(_sim, b);
        foreach (var c in b.FootprintCells())
        {
            if (_sim.Piles.At(c).IsEmpty) continue;
            CancelPickUps(c);
            MovePile(c, stand);
        }
        Construction.MoveAgentsOut(_sim, b, stand, alsoOnTop: false);
        return b;
    }

    /// <summary>CRF-21, in order: every job naming the trader is cancelled (carried items are dropped where the dwarf
    /// stands); the paid units of each deal's ungranted lots are refunded as a pile of the give item at the stand cell;
    /// the goods not yet unloaded are dropped there too; the footprint turns to air (agents on it move off), the
    /// building is removed and the visit is cleared.</summary>
    internal void TraderDepart()
    {
        var visit = _sim.Trader;
        var b = _sim.Buildings.Get(visit.Building);
        if (b is null) { visit.Clear(); return; }
        CancelJobsNaming(b.Id);
        var stand = Construction.StandCell(_sim, b);
        foreach (var d in visit.Deals)
        {
            var offer = _sim.Content.Offers[d.Offer];
            PlacePile(stand, offer.Give, d.Paid - d.Granted * offer.GiveCount);   // a refund of 0 places nothing
        }
        foreach (var (item, n) in b.Stored) PlacePile(stand, new ItemId(item), n);
        b.Stored.Clear();
        if (b.Def.SetsBlocks)
            foreach (var c in b.FootprintCells()) _sim.World.SetBlock(c, BlockId.Air);
        Construction.MoveAgentsOut(_sim, b, SafeStand(b, stand), alsoOnTop: true);
        _sim.Buildings.Remove(b);
        visit.Clear();
        _sim.Events.Emit(new BuildingRemoved(b.Id));
        _sim.Events.Emit(new TraderLeft(b.Id));
    }
}
