namespace Colony.Sim.Core;

// Typed ids. Value 0 is always invalid. Allocate with IdAllocator (monotonic, saved with the game).

/// <summary>Id of an agent. A future player avatar is also an agent (one action API).</summary>
public readonly record struct AgentId(int Value) { public bool IsValid => Value > 0; public override string ToString() => $"A{Value}"; }

/// <summary>Id of a building (blueprint, construction site, or complete).</summary>
public readonly record struct BuildingId(int Value) { public bool IsValid => Value > 0; public override string ToString() => $"B{Value}"; }

/// <summary>Id of a job on the job board.</summary>
public readonly record struct JobId(int Value) { public bool IsValid => Value > 0; public override string ToString() => $"J{Value}"; }

/// <summary>Id of a plant entity (tree or bush).</summary>
public readonly record struct PlantId(int Value) { public bool IsValid => Value > 0; public override string ToString() => $"P{Value}"; }

/// <summary>Index into ContentDb.Items. Value 0 is "none".</summary>
public readonly record struct ItemId(int Value) { public bool IsValid => Value > 0; }

/// <summary>Monotonic id counter. Serialized with the save.</summary>
public sealed class IdAllocator
{
    public int Next { get; set; } = 1;
    public int Allocate() => Next++;
}
