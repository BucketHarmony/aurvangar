using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;

namespace Aurvangar.Sim.Buildings;

/// <summary>CRF-13 workshop status, first match wins.</summary>
public enum WorkshopStatus : byte { NotBuilt, NoOrders, Working, OutputFull, NoInput, Waiting, Done }

/// <summary>A workshop's status and, for NoInput, the missing input item (for OutputFull, the output with no room).</summary>
public readonly record struct WorkshopState(WorkshopStatus Status, ItemId Item);

public static partial class Workshops
{
    /// <summary>CRF-13: derived, never stored. NotBuilt; NoOrders; Working (a Craft job is claimed); OutputFull (no room
    /// in the buffer for one more cycle of the first wanting order, and no storage has room for its output); NoInput (an
    /// order wants output but no order that does has input in reach; names the first wanting order's input); Waiting (an
    /// order wants output and has input: its job is open or waits for room); Done (every order is satisfied).</summary>
    public static WorkshopState StatusOf(Simulation sim, Building b)
    {
        if (b.State != BuildingState.Complete || b.Def.Workshop is not { } w) return new(WorkshopStatus.NotBuilt, default);
        if (b.Orders.Count == 0) return new(WorkshopStatus.NoOrders, default);
        foreach (var j in sim.Jobs.All)
            if (j.Kind == JobKind.Craft && j.IsClaimed && WorkshopOf(j) == b.Id) return new(WorkshopStatus.Working, default);

        var recipes = sim.Content.RecipesOf(b.Def);
        Content.Recipe? first = null;
        bool anyInput = false;
        foreach (var o in b.Orders)
        {
            var r = recipes[o.Recipe];
            if (Wanted(sim, o, r) <= 0) continue;
            first ??= r;
            if (Source(sim, b, r) is not null) { anyInput = true; break; }
        }
        if (first is null) return new(WorkshopStatus.Done, default);
        if (StoredTotal(b) + first.OutputCount > w.OutputBuffer && Destination(sim, b, first.Output) is null)
            return new(WorkshopStatus.OutputFull, first.Output);
        if (!anyInput) return new(WorkshopStatus.NoInput, first.Input);
        return new(WorkshopStatus.Waiting, default);
    }
}
