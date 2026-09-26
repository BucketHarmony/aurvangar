using System.Reflection;
using System.Text.Json;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Content;

/// <summary>All data-driven game content, loaded from the embedded data/*.json files.</summary>
public sealed class ContentDb
{
    public IReadOnlyList<BlockDef> Blocks { get; }
    public IReadOnlyList<ItemDef> Items { get; }          // index 0 is a sentinel "none"; ItemId.Value indexes this list
    public IReadOnlyList<BuildingDef> Buildings { get; }
    public PaletteDef Palette { get; }

    /// <summary>Solidity lookup indexed by block byte. Hot path for water and pathing.</summary>
    public bool[] SolidTable { get; } = new bool[256];

    private readonly Dictionary<string, ItemId> _itemsByKey;
    private readonly Dictionary<string, BuildingDef> _buildingsByKey;

    private ContentDb(List<BlockDef> blocks, List<ItemDef> items, List<BuildingDef> buildings, PaletteDef palette)
    {
        Blocks = blocks;
        Items = items;
        Buildings = buildings;
        Palette = palette;
        _itemsByKey = new Dictionary<string, ItemId>(StringComparer.Ordinal);
        for (int i = 1; i < items.Count; i++) _itemsByKey[items[i].Id] = new ItemId(i);
        _buildingsByKey = buildings.ToDictionary(b => b.Id, StringComparer.Ordinal);
        foreach (var b in blocks) SolidTable[b.NumericId] = b.Solid;
        Validate();
    }

    public ItemId Item(string key) =>
        _itemsByKey.TryGetValue(key, out var id) ? id : throw new KeyNotFoundException($"items.json: unknown item '{key}'");

    public ItemDef ItemDef(ItemId id) => Items[id.Value];

    public BuildingDef Building(string key) =>
        _buildingsByKey.TryGetValue(key, out var def) ? def : throw new KeyNotFoundException($"buildings.json: unknown building '{key}'");

    public BlockDef Block(BlockId id) => Blocks[(int)id];

    /// <summary>Load the content embedded in Aurvangar.Sim.dll.</summary>
    public static ContentDb LoadEmbedded()
    {
        var asm = typeof(ContentDb).Assembly;
        return Load(
            ReadResource(asm, "data/blocks.json"),
            ReadResource(asm, "data/items.json"),
            ReadResource(asm, "data/buildings.json"),
            ReadResource(asm, "data/palette.json"));
    }

    /// <summary>Load from JSON strings (used by tests to inject bad data).</summary>
    public static ContentDb Load(string blocksJson, string itemsJson, string buildingsJson, string paletteJson)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };
        var blocks = JsonSerializer.Deserialize<BlocksFile>(blocksJson, opts)?.Blocks
                     ?? throw new InvalidDataException("blocks.json: empty");
        var items = JsonSerializer.Deserialize<ItemsFile>(itemsJson, opts)?.Items
                    ?? throw new InvalidDataException("items.json: empty");
        var buildings = JsonSerializer.Deserialize<BuildingsFile>(buildingsJson, opts)?.Buildings
                        ?? throw new InvalidDataException("buildings.json: empty");
        var palette = JsonSerializer.Deserialize<PaletteDef>(paletteJson, opts)
                      ?? throw new InvalidDataException("palette.json: empty");

        var itemList = new List<ItemDef> { new("", "(none)", 0, 0) };
        itemList.AddRange(items);
        return new ContentDb(blocks.OrderBy(b => b.NumericId).ToList(), itemList, buildings, palette);
    }

    private void Validate()
    {
        // Block ids must match the BlockId enum exactly (the enum is used in hot paths).
        foreach (BlockId id in Enum.GetValues<BlockId>())
        {
            var def = Blocks.FirstOrDefault(b => b.NumericId == (int)id)
                      ?? throw new InvalidDataException($"blocks.json: missing block id {(int)id} ({id})");
            if (!string.Equals(def.Name, id.ToString(), StringComparison.Ordinal))
                throw new InvalidDataException($"blocks.json: id {(int)id} is named '{def.Name}', enum says '{id}'");
            if (def.Drop is not null && !_itemsByKey.ContainsKey(def.Drop))
                throw new InvalidDataException($"blocks.json: block '{def.Name}' drops unknown item '{def.Drop}'");
        }
        if (Blocks.Select(b => b.NumericId).Distinct().Count() != Blocks.Count)
            throw new InvalidDataException("blocks.json: duplicate block id");
        if (Items.Skip(1).Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() != Items.Count - 1)
            throw new InvalidDataException("items.json: duplicate item id");
        if (Buildings.Select(b => b.Id).Distinct(StringComparer.Ordinal).Count() != Buildings.Count)
            throw new InvalidDataException("buildings.json: duplicate building id");
        foreach (var b in Buildings)
        {
            foreach (var key in b.Cost.Keys)
                if (!_itemsByKey.ContainsKey(key))
                    throw new InvalidDataException($"buildings.json: building '{b.Id}' costs unknown item '{key}'");
            if (b.Storage is not null)
                foreach (var key in b.Storage.Accepts)
                    if (!_itemsByKey.ContainsKey(key))
                        throw new InvalidDataException($"buildings.json: building '{b.Id}' stores unknown item '{key}'");
            if (b.Producer is not null && !_itemsByKey.ContainsKey(b.Producer.Output))
                throw new InvalidDataException($"buildings.json: building '{b.Id}' produces unknown item '{b.Producer.Output}'");
            if (b.Footprint.Length != 3 || b.Footprint.Any(v => v <= 0))
                throw new InvalidDataException($"buildings.json: building '{b.Id}' footprint must be 3 positive ints");
            if (b.Entrance.Length != 3)
                throw new InvalidDataException($"buildings.json: building '{b.Id}' entrance must be 3 ints");
        }
    }

    private static string ReadResource(Assembly asm, string name)
    {
        using var s = asm.GetManifestResourceStream(name)
                      ?? throw new InvalidDataException($"Embedded resource '{name}' not found. Check Aurvangar.Sim.csproj EmbeddedResource.");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    private sealed record BlocksFile(List<BlockDef> Blocks);
    private sealed record ItemsFile(List<ItemDef> Items);
    private sealed record BuildingsFile(List<BuildingDef> Buildings);
}
