using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;

namespace Aurvangar.Sim.Commands;

/// <summary>DSG-02: mark every diggable, non-building-floor cell of the box (corners in any order, clamped to the
/// world) for digging. A box entirely outside the world is rejected.</summary>
public sealed record DesignateDig(Int3 A, Int3 B) : ICommand
{
    public string Tag => "DesignateDig";

    public void Apply(Simulation sim) => DesignationSystem.DesignateDig(sim, Tag, A, B);
}

/// <summary>DSG-05: mark every tree whose base is inside the XZ rectangle (corners in any order, any Y).</summary>
public sealed record DesignateChop(int X0, int Z0, int X1, int Z1) : ICommand
{
    public string Tag => "DesignateChop";

    public void Apply(Simulation sim) => DesignationSystem.DesignateChop(sim, Tag, X0, Z0, X1, Z1);
}

/// <summary>DSG-06: clear dig marks, chop marks (trees whose base is in the box) and Empty farm tiles, and cancel their jobs.</summary>
public sealed record CancelDesignation(Int3 A, Int3 B) : ICommand
{
    public string Tag => "CancelDesignation";

    public void Apply(Simulation sim) => DesignationSystem.Cancel(sim, Tag, A, B);
}

/// <summary>ECO-11: turn the top-surface Grass/Dirt cell of every column in the XZ rectangle (corners in any order)
/// into a farm tile.</summary>
public sealed record DesignateFarm(int X0, int Z0, int X1, int Z1) : ICommand
{
    public string Tag => "DesignateFarm";

    public void Apply(Simulation sim) => Farming.FarmSystem.Designate(sim, Tag, X0, Z0, X1, Z1);
}
