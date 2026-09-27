using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Actions;

/// <summary>M11-T9 gravity writes (docs/specs/gravity.md, ADR-079). <see cref="Physics.Gravity"/> decides what falls;
/// these methods move the items and remove the buildings, so every such mutation stays in the action API.</summary>
public sealed partial class WorldActions
{
    /// <summary>GRV-03: the cell a pile at <paramref name="c"/> comes to rest on: straight down while the cell below is
    /// not solid.</summary>
    public Int3 RestCell(Int3 c)
    {
        var world = _sim.World;
        while (c.Y > 0 && world.InBounds(c + Int3.Down) && !world.IsSolid(c + Int3.Down)) c += Int3.Down;
        return c;
    }

    /// <summary>GRV-03/04: moves the pile at <paramref name="from"/> to where it lands. Jobs that would still pick it
    /// up there are cancelled first (not failed). With nowhere to land, the pile stays and tries again next tick.</summary>
    internal void FallPile(Int3 from)
    {
        var s = _sim.Piles.At(from);
        if (s.IsEmpty || !FindLanding(from, s.Item, out var at)) return;
        CancelPickUps(from);
        _sim.Piles.Take(from, s.Count);
        _sim.Piles.Add(at, s.Item, s.Count);
    }

    /// <summary>GRV-07 step 4: items that fall from <paramref name="cell"/> (no pile yet). Never lost: with nowhere to
    /// land they are placed by ECO-08 from the rest cell, and fall again later if they must.</summary>
    internal void DropFalling(Int3 cell, ItemId item, int count)
    {
        if (count <= 0) return;
        if (FindLanding(cell, item, out var at)) _sim.Piles.Add(at, item, count);
        else PlacePile(RestCell(cell), item, count);
    }

    /// <summary>GRV-07: removes a building that no longer stands. A blueprint or site is cancelled (BLD-09). A complete
    /// building, or one being deconstructed: the jobs naming it are cancelled, its footprint turns to air, agents on or
    /// in it go to a cell they can stand on near its stand cell, and half its cost then its stock fall from its
    /// origin as piles.</summary>
    internal void Collapse(Building b)
    {
        if (b.State is BuildingState.Blueprint or BuildingState.UnderConstruction)
        {
            Construction.Cancel(_sim, b);
            return;
        }
        CancelJobsNaming(b.Id);
        if (b.Def.SetsBlocks)
            foreach (var c in b.FootprintCells()) _sim.World.SetBlock(c, BlockId.Air);
        Construction.MoveAgentsOut(_sim, b, SafeStand(b, Construction.StandCell(_sim, b)), alsoOnTop: true);
        foreach (var (item, n) in Construction.Cost(_sim, b.Def)) DropFalling(b.Origin, item, n / 2);
        foreach (var (item, n) in b.Stored) DropFalling(b.Origin, new ItemId(item), n);
        b.Stored.Clear();
        _sim.Buildings.Remove(b);
        _sim.Events.Emit(new BuildingRemoved(b.Id));
    }

    /// <summary>GRV-07 step 2: where agents on a collapsing building go, chosen after its footprint turned to air: the
    /// stand cell when agents can stand there and it is outside the footprint; else the first such cell of the ECO-08
    /// spiral around its rest cell; else the rest cell itself.</summary>
    private Int3 SafeStand(Building b, Int3 stand)
    {
        var grid = _sim.PathGrid;
        if (grid.IsStandable(stand) && !b.Covers(stand)) return stand;
        var rest = _sim.World.IsSolid(stand) ? stand : RestCell(stand);
        if (grid.IsStandable(rest) && !b.Covers(rest)) return rest;
        foreach (var o in PileSpiral)
            if (grid.IsStandable(rest + o) && !b.Covers(rest + o)) return rest + o;
        return rest;
    }

    /// <summary>GRV-03: the rest cell below <paramref name="from"/> when it takes the item and is outside every building;
    /// else the ECO-08 standable cell from it; else the first spiral cell that rests, is not solid, takes the item and
    /// is outside every building.</summary>
    private bool FindLanding(Int3 from, ItemId item, out Int3 at)
    {
        var rest = RestCell(from);
        if (Takes(rest)) { at = rest; return true; }
        if (FindPileCell(rest, item, out at) && at != from) return true;
        foreach (var o in PileSpiral)
        {
            at = rest + o;
            if (at != from && _sim.World.InBounds(at) && !_sim.World.IsSolid(at) && Physics.Gravity.Rests(_sim, at) && Takes(at))
                return true;
        }
        at = default;
        return false;

        bool Takes(Int3 c) => _sim.Piles.Accepts(c, item) && _sim.Buildings.BuildingAt(c) is null;
    }

    /// <summary>GRV-04: cancels the jobs that would still pick up the pile at the cell: unclaimed ones, and claimed ones
    /// whose agent has not run that PickUp step yet. Ascending job id.</summary>
    private void CancelPickUps(Int3 cell)
    {
        List<Job>? cancel = null;
        foreach (var j in _sim.Jobs.All)
        {
            int k = j.Steps.FindIndex(s => s.Kind == StepKind.PickUp && s.Cell == cell);
            if (k < 0) continue;
            var a = _sim.Agents.Get(j.ClaimedBy);
            if (!j.IsClaimed || a is null || a.CurrentJob != j.Id || a.StepIndex <= k) (cancel ??= new List<Job>()).Add(j);
        }
        if (cancel is not null)
            foreach (var j in cancel) JobRunner.Cancel(_sim, j);
    }

    /// <summary>GRV-07 step 1: cancels every job with a step on the building (go to, work on, take from, deliver to,
    /// consume from) or a storage reservation on it. Ascending job id.</summary>
    private void CancelJobsNaming(BuildingId id)
    {
        List<Job>? cancel = null;
        foreach (var j in _sim.Jobs.All)
            if (Names(j)) (cancel ??= new List<Job>()).Add(j);
        if (cancel is not null)
            foreach (var j in cancel) JobRunner.Cancel(_sim, j);

        bool Names(Job j)
        {
            foreach (var r in j.Reservations)
                if (r.Building == id) return true;
            foreach (var s in j.Steps)
            {
                if (s.Target != id.Value) continue;
                if (s.Kind is StepKind.PickUpFromStorage or StepKind.DeliverTo or StepKind.Consume) return true;
                if (s.Kind is StepKind.GoTo or StepKind.Work && s.Goal == GoalMode.Building) return true;
            }
            return false;
        }
    }
}
