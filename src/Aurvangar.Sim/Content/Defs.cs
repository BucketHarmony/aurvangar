using System.Text.Json.Serialization;

namespace Aurvangar.Sim.Content;

/// <summary>A block type from blocks.json.</summary>
public sealed record BlockDef(
    [property: JsonPropertyName("id")] int NumericId,
    string Name,
    bool Solid,
    bool Diggable,
    int Hardness,
    string? Drop);

/// <summary>An item type from items.json.</summary>
public sealed record ItemDef(string Id, string Name, int Food, int Drink);

/// <summary>A building type from buildings.json. See docs/specs/buildings.md. <see cref="Stackable"/> (BLD-04,
/// ADR-040): may be placed on another building of the same type, blueprinted or complete, and its complete blocks
/// count as ground for any building.</summary>
public sealed record BuildingDef(
    string Id,
    string Name,
    int[] Footprint,
    int[] Entrance,
    Dictionary<string, int> Cost,
    int BuildTicks,
    string Placement,
    StorageDef? Storage,
    int Workers,
    ProducerDef? Producer,
    bool SetsBlocks,
    bool PrebuiltOnly,
    bool Stackable = false);

/// <summary>Storage block of a building. Capacity is total (0 = none); PerItemCapacity caps each item (0 = none).</summary>
public sealed record StorageDef(int Capacity, int PerItemCapacity, string[] Accepts);

/// <summary>Producer block (pump).</summary>
public sealed record ProducerDef(string Output, int CycleTicks, int MinIntakeLevel, int UnitsPerCycle, int Buffer, int HaulAt);

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
