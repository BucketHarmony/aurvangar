using Aurvangar.Sim;
using Aurvangar.Sim.Designations;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Hud;

/// <summary>VIEW-25 (M11-T8): the mouse label over a dig-marked cell says why it waits (<see cref="DigStatus"/>,
/// DSG-10), e.g. "Dig: would trap a dwarf".</summary>
public static class DigHover
{
    public static string Text(DigWait wait) => wait switch
    {
        DigWait.Queued => "Dig: waiting for a dwarf",
        DigWait.Digging => "Dig: being dug",
        DigWait.CellAbove => "Dig: waiting for the cell above",
        DigWait.Neighbour => "Dig: waiting for a cell beside it to be dug",
        DigWait.WouldTrap => "Dig: would trap a dwarf (a dwarf climbs 1 level; dig a stair down, T)",
        DigWait.Plant => "Dig: waiting for the plant on it",
        DigWait.Support => "Dig: something built rests on it",
        DigWait.Unreachable => "Dig: unreachable, no dwarf can get to it",
        _ => "",
    };

    /// <summary>The label for the hovered cell, or null when it has no dig mark or lies above the view level.</summary>
    public static string? For(Simulation sim, PickHit? hit, int sliceY)
    {
        if (hit is not { } h || h.Cell.Y > sliceY || sim.Designations.Count == 0) return null;
        var wait = DigStatus.Of(sim, h.Cell);
        return wait == DigWait.None ? null : Text(wait);
    }
}
