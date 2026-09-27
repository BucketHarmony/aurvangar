using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Commands;

/// <summary>CRF-07 (M11-T4): sets or replaces the order for one recipe of a workshop (a replaced order's done goes back
/// to 0); <paramref name="Count"/> 0 removes it. Rejected, in this order, with NoSuchBuilding, NotAWorkshop, BadRecipe,
/// BadMode, BadCount (outside 0..999) or NothingToRemove. Orders may be set on a workshop in any state.</summary>
public sealed record SetWorkshopOrder(BuildingId Workshop, int Recipe, OrderMode Mode, int Count) : ICommand
{
    public string Tag => "SetWorkshopOrder";

    public void Apply(Simulation sim) => Workshops.SetOrder(sim, Tag, Workshop, Recipe, Mode, Count);
}
