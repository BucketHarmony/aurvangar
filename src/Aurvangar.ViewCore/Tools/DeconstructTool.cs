using Aurvangar.Sim;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Tools;

/// <summary>Deconstruct tool (VIEW-12, BLD-09). A click on a building sends <c>Deconstruct</c>: a blueprint or site is
/// cancelled, a complete building is torn down. The tool stays active. Engine-neutral.</summary>
public static class DeconstructTool
{
    /// <summary>The building under the pick: the one covering the picked cell (complete buildings are solid blocks),
    /// else the one covering the empty cell in front of the picked face (blueprints and sites stand on the ground).</summary>
    public static Building? Target(Simulation sim, PickHit? hit)
    {
        if (hit is not { } h) return null;
        return sim.Buildings.BuildingAt(h.Cell) ?? sim.Buildings.BuildingAt(h.Adjacent);
    }

    /// <summary>Why the building cannot be deconstructed (the same checks as the command, BLD-09), or null.</summary>
    public static string? Refusal(Simulation sim, Building b)
    {
        if (b.Def.PrebuiltOnly) return $"The {b.Def.Name} cannot be torn down";
        if (b.State == BuildingState.Deconstructing) return "Already being torn down";
        if (Construction.HasBuildingOnTop(sim, b)) return "Something is built on top of it";
        return null;
    }

    /// <summary>The hover text: what a click would do, or why it would not.</summary>
    public static string Tooltip(Simulation sim, Building b) => Refusal(sim, b) ?? b.State switch
    {
        BuildingState.Complete => $"Tear down {b.Def.Name} (half the materials back)",
        _ => $"Cancel {b.Def.Name} (all delivered materials back)",
    };

    /// <summary>The command for a click: null over nothing; a message instead of a command when refused.</summary>
    public static (ICommand? Command, string? Message) Click(Simulation sim, PickHit? hit)
    {
        if (Target(sim, hit) is not { } b) return (null, null);
        if (Refusal(sim, b) is { } why) return (null, why);
        return (new Deconstruct(b.Id), null);
    }
}
