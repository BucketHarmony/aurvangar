using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Items;

namespace Aurvangar.Sim.Actions;

/// <summary>M11-T4 crafting (CRF-11, ADR-082): one craft cycle at a workshop.</summary>
public sealed partial class WorldActions
{
    /// <summary>CRF-11: turns one cycle of carried input into output in the workshop's buffer (<see cref="Building.Stored"/>).
    /// Checks, in order: the workshop exists, is complete and has the recipe (InvalidTarget); the agent stands on its
    /// stand cell, the entrance (OutOfReach); it carries the input (InventoryEmpty, WrongItem), at least one cycle of it
    /// (NotEnoughItems); the output fits the buffer (StorageFull). A Make order for the recipe counts the outputs, and
    /// is removed with <see cref="WorkshopOrderDone"/> when it reaches its count; a Keep order or no order adds nothing.</summary>
    public ActionResult Craft(AgentId actor, BuildingId workshop, int recipe)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        var b = _sim.Buildings.Get(workshop);
        if (b is not { State: BuildingState.Complete, Def.Workshop: { } w }) return ActionResult.InvalidTarget;
        var recipes = _sim.Content.RecipesOf(b.Def);
        if (recipe < 0 || recipe >= recipes.Count) return ActionResult.InvalidTarget;
        if (a.Cell != Construction.StandCell(_sim, b)) return ActionResult.OutOfReach;
        var rc = recipes[recipe];
        if (a.Carried.IsEmpty) return ActionResult.InventoryEmpty;
        if (a.Carried.Item != rc.Input) return ActionResult.WrongItem;
        if (a.Carried.Count < rc.InputCount) return ActionResult.NotEnoughItems;
        if (Workshops.StoredTotal(b) + rc.OutputCount > w.OutputBuffer) return ActionResult.StorageFull;

        int left = a.Carried.Count - rc.InputCount;
        a.Carried = left > 0 ? new ItemStack(rc.Input, left) : ItemStack.Empty;
        b.Stored[rc.Output.Value] = StoredCount(b, rc.Output) + rc.OutputCount;
        if (b.OrderFor(recipe) is { Mode: OrderMode.Make } o)
        {
            o.Done += rc.OutputCount;
            if (o.Done >= o.Count)
            {
                b.Orders.Remove(o);
                _sim.Events.Emit(new WorkshopOrderDone(b.Id, recipe));
            }
        }
        return ActionResult.Ok;
    }
}
