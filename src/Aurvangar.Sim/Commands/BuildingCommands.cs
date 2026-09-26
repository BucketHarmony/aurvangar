using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Commands;

/// <summary>BLD-05: place a blueprint of building definition <paramref name="DefId"/> (BLD-01..04 validation). Emits
/// <c>BuildingPlaced</c>, or <c>CommandRejected</c> with the <see cref="PlacementResult"/> name (or
/// "UnknownBuilding") as the reason.</summary>
public sealed record PlaceBuilding(string DefId, Int3 Origin, int Rotation) : ICommand
{
    public string Tag => "PlaceBuilding";

    public void Apply(Simulation sim) => Construction.Place(sim, Tag, DefId, Origin, Rotation);
}

/// <summary>BLD-09: a blueprint or construction site is cancelled (full refund of what was delivered); a complete
/// building is deconstructed by a Deconstruct job (half refund). Rejected with a reason for the hub
/// ("PrebuiltOnly"), an unknown id, a building with another building on top, or one already being deconstructed.
/// Not a positional record: that would generate a Deconstruct method, which C# forbids on a type of that name.</summary>
public sealed record Deconstruct : ICommand
{
    public Deconstruct(BuildingId building) { Building = building; }

    public BuildingId Building { get; init; }

    public string Tag => "Deconstruct";

    public void Apply(Simulation sim) => Construction.Deconstruct(sim, Tag, Building);
}
