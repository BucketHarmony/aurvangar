using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Buildings;

/// <summary>CRF-08 (M11-T4): colony stock queries. Computed when needed, never cached, so nothing here is state.</summary>
public static class Economy
{
    /// <summary>CRF-08: the colony stock of an item: stored in complete storage buildings, held as output by any
    /// workshop, and carried by living agents. Loose piles do not count.</summary>
    public static int Stock(Simulation sim, ItemId item)
    {
        int n = 0;
        foreach (var b in sim.Buildings.All)
        {
            bool storage = b.State == BuildingState.Complete && b.Def.Storage is not null;
            if (storage || b.Def.Workshop is not null) n += WorldActions.StoredCount(b, item);
        }
        foreach (var a in sim.Agents.All)
            if (a.IsAlive && !a.Carried.IsEmpty && a.Carried.Item == item) n += a.Carried.Count;
        return n;
    }
}
