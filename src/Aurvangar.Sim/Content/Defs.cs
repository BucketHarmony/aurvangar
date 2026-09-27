using System.Text.Json.Serialization;

namespace Aurvangar.Sim.Content;

/// <summary>A block type from blocks.json. CON-01 (M8-T2): a construction block has a <see cref="Cost"/> (one item
/// type), a player-facing <see cref="Label"/> and <see cref="BuildTicks"/>; the other blocks leave them out.</summary>
public sealed record BlockDef(
    [property: JsonPropertyName("id")] int NumericId,
    string Name,
    bool Solid,
    bool Diggable,
    int Hardness,
    string? Drop,
    string? Label = null,
    Dictionary<string, int>? Cost = null,
    int BuildTicks = 0)
{
    /// <summary>CON-01: a block is a construction block if and only if it has a cost.</summary>
    public bool IsConstruction => Cost is not null;
}

/// <summary>CON-19 (M11-T10, ADR-080): a fine block shape from the <c>shapes</c> list of blocks.json. Ids and names
/// match <see cref="World.BlockShape"/>. <see cref="Rotations"/> is 1 or 4. A shaped block costs
/// <c>max(1, ceil(cost * CostPercent / 100))</c> of its block's cost item (<see cref="ContentDb.CostOf(World.BlockId, World.BlockShape)"/>).</summary>
public sealed record ShapeDef(
    [property: JsonPropertyName("id")] int NumericId,
    string Name,
    string Label,
    int Rotations,
    int CostPercent);

/// <summary>An item type from items.json.</summary>
public sealed record ItemDef(string Id, string Name, int Food, int Drink);

/// <summary>A building type from buildings.json. See docs/specs/buildings.md. <see cref="Stackable"/> (BLD-04,
/// ADR-040): may be placed on another building of the same type, blueprinted or complete, and its complete blocks
/// count as ground for any building. <see cref="Entrance"/> is null for a building with no workers and no storage
/// (the levee, M11-T1, ADR-076): its builders stand on any standable cell in reach of the footprint.
/// <see cref="StartStock"/> (M11-T2, ADR-077): a building with it is part of the starting colony (the hub and the
/// wagon, BLD-15) and holds these items at world creation. <see cref="RemovableWhenEmpty"/>: a prebuilt-only building
/// that may still be torn down once it holds nothing (the wagon, BLD-17).</summary>
public sealed record BuildingDef(
    string Id,
    string Name,
    int[] Footprint,
    int[]? Entrance,
    Dictionary<string, int> Cost,
    int BuildTicks,
    string Placement,
    StorageDef? Storage,
    int Workers,
    ProducerDef? Producer,
    bool SetsBlocks,
    bool PrebuiltOnly,
    bool Stackable = false,
    Dictionary<string, int>? StartStock = null,
    bool RemovableWhenEmpty = false,
    WorkshopDef? Workshop = null)
{
    /// <summary>False for a building with no entrance in data (the levee, ADR-076).</summary>
    public bool HasEntrance => Entrance is not null;
}

/// <summary>Storage block of a building. Capacity is total (0 = none); PerItemCapacity caps each item (0 = none).
/// <see cref="Receives"/> false (the wagon, BLD-16, ADR-077): it holds only what it started with; nothing is delivered
/// or hauled into it, but it is still a source.</summary>
public sealed record StorageDef(int Capacity, int PerItemCapacity, string[] Accepts, bool Receives = true);

/// <summary>Producer block (pump).</summary>
public sealed record ProducerDef(string Output, int CycleTicks, int MinIntakeLevel, int UnitsPerCycle, int Buffer, int HaulAt);

/// <summary>CRF-03 (M11-T4, ADR-082): the workshop block of a building. Its output sits in <c>Building.Stored</c>
/// (at most <see cref="OutputBuffer"/> items, all outputs together); an Unload job is posted at <see cref="HaulAt"/>
/// unpromised items (CRF-12). Recipes are indexed by their position in <see cref="Recipes"/>.</summary>
public sealed record WorkshopDef(int OutputBuffer, int HaulAt, RecipeDef[] Recipes);

/// <summary>CRF-04: a recipe, exactly one input item and one output item with their counts, and the work per cycle.
/// Ids are unique over all workshops.</summary>
public sealed record RecipeDef(string Id, string Name, Dictionary<string, int> Input, Dictionary<string, int> Output,
    int WorkTicks);

/// <summary>Colors for the view. Hex strings "#rrggbb".</summary>
public sealed record PaletteDef(
    Dictionary<string, string> Blocks,
    float CutFaceDarken,
    WaterPalette Water,
    Dictionary<string, string> Items,
    Dictionary<string, string> Buildings,
    Dictionary<string, string> Agents,
    Dictionary<string, string> Designations,
    Dictionary<string, string> Plants);

public sealed record WaterPalette(string Shallow, string Deep, float Alpha);
