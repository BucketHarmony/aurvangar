using System.Reflection;
using System.Text.Json;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Content;

/// <summary>All data-driven game content, loaded from the embedded data/*.json files.</summary>
public sealed partial class ContentDb
{
    public IReadOnlyList<BlockDef> Blocks { get; }
    /// <summary>CON-19: the fine block shapes, indexed by <see cref="BlockShape"/>.</summary>
    public IReadOnlyList<ShapeDef> Shapes { get; }
    public IReadOnlyList<ItemDef> Items { get; }          // index 0 is a sentinel "none"; ItemId.Value indexes this list
    public IReadOnlyList<BuildingDef> Buildings { get; }
    public PaletteDef Palette { get; }

    /// <summary>Solidity lookup indexed by block byte. Hot path for water and pathing.</summary>
    public bool[] SolidTable { get; } = new bool[256];

    /// <summary>CON-01: construction-block lookup indexed by block byte (a block with a cost).</summary>
    public bool[] ConstructionTable { get; } = new bool[256];

    private readonly ItemId[] _costItem = new ItemId[256];
    private readonly int[] _costCount = new int[256];

    private readonly Dictionary<string, ItemId> _itemsByKey;
    private readonly Dictionary<string, BuildingDef> _buildingsByKey;

    private ContentDb(List<BlockDef> blocks, List<ShapeDef> shapes, List<ItemDef> items, List<BuildingDef> buildings,
        PaletteDef palette)
    {
        Blocks = blocks;
        Shapes = shapes;
        Items = items;
        Buildings = buildings;
        Palette = palette;
        _itemsByKey = new Dictionary<string, ItemId>(StringComparer.Ordinal);
        for (int i = 1; i < items.Count; i++) _itemsByKey[items[i].Id] = new ItemId(i);
        _buildingsByKey = buildings.ToDictionary(b => b.Id, StringComparer.Ordinal);
        foreach (var b in blocks)
        {
            if (b.NumericId is < 0 or > 255) throw new InvalidDataException($"blocks.json: block id {b.NumericId} ('{b.Name}') is out of range 0..255");
            SolidTable[b.NumericId] = b.Solid;
        }
        Validate();
        IndexRecipes();
        foreach (var b in blocks)
        {
            if (!b.IsConstruction) continue;
            ConstructionTable[b.NumericId] = true;
            foreach (var (key, n) in b.Cost!) { _costItem[b.NumericId] = _itemsByKey[key]; _costCount[b.NumericId] = n; }
        }
    }

    /// <summary>CON-01: true for a construction block (a block with a cost in blocks.json).</summary>
    public bool IsConstruction(BlockId id) => ConstructionTable[(int)id];

    /// <summary>CON-01: the one cost item of a construction block and its count (default, 0 for any other block).</summary>
    public (ItemId Item, int Count) CostOf(BlockId id) => (_costItem[(int)id], _costCount[(int)id]);

    /// <summary>CON-19 (M11-T10): the cost of a construction block built in <paramref name="shape"/>: the block's cost
    /// item, and <c>max(1, ceil(count * costPercent / 100))</c> of it. Full is the block's own cost.</summary>
    public (ItemId Item, int Count) CostOf(BlockId id, BlockShape shape)
    {
        var (item, n) = CostOf(id);
        if (shape == BlockShape.Full || n == 0) return (item, n);
        return (item, Math.Max(1, (n * Shapes[(int)shape].CostPercent + 99) / 100));
    }

    /// <summary>CON-19: the shape is defined and the rotation is below its rotation count.</summary>
    public bool IsValidForm(BlockForm form) =>
        (int)form.Shape < Shapes.Count && form.Rotation < Shapes[(int)form.Shape].Rotations;

    /// <summary>Player-facing name of a block: its label, else its enum name.</summary>
    public string LabelOf(BlockId id) => (int)id < Blocks.Count ? Blocks[(int)id].Label ?? Blocks[(int)id].Name : id.ToString();

    public ItemId Item(string key) =>
        _itemsByKey.TryGetValue(key, out var id) ? id : throw new KeyNotFoundException($"items.json: unknown item '{key}'");

    public ItemDef ItemDef(ItemId id) => Items[id.Value];

    public BuildingDef Building(string key) =>
        _buildingsByKey.TryGetValue(key, out var def) ? def : throw new KeyNotFoundException($"buildings.json: unknown building '{key}'");

    /// <summary>The building definition with this id, or null (commands validate player input with it).</summary>
    public BuildingDef? FindBuilding(string key) => _buildingsByKey.TryGetValue(key, out var def) ? def : null;

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
        var blocksFile = JsonSerializer.Deserialize<BlocksFile>(blocksJson, opts);
        var blocks = blocksFile?.Blocks ?? throw new InvalidDataException("blocks.json: empty");
        var shapes = blocksFile.Shapes ?? throw new InvalidDataException("blocks.json: no shapes list (CON-19)");
        var items = JsonSerializer.Deserialize<ItemsFile>(itemsJson, opts)?.Items
                    ?? throw new InvalidDataException("items.json: empty");
        var buildings = JsonSerializer.Deserialize<BuildingsFile>(buildingsJson, opts)?.Buildings
                        ?? throw new InvalidDataException("buildings.json: empty");
        var palette = JsonSerializer.Deserialize<PaletteDef>(paletteJson, opts)
                      ?? throw new InvalidDataException("palette.json: empty");

        var itemList = new List<ItemDef> { new("", "(none)", 0, 0) };
        itemList.AddRange(items);
        return new ContentDb(blocks.OrderBy(b => b.NumericId).ToList(), shapes.OrderBy(s => s.NumericId).ToList(), itemList,
            buildings, palette);
    }

    private void Validate()
    {
        ValidateBlocks();
        ValidateShapes();
        ValidateWorkshops();   // CRF-04 first, so a workshop error names the workshop
        ValidateBuildings();
    }

    /// <summary>CON-19: the shapes match <see cref="BlockShape"/> by id and name, both ways; rotations are 1 or 4;
    /// costPercent is 1..100; Full has one rotation and costs 100%.</summary>
    private void ValidateShapes()
    {
        var values = Enum.GetValues<BlockShape>();
        if (Shapes.Count != values.Length)
            throw new InvalidDataException($"blocks.json: {Shapes.Count} shapes, the BlockShape enum has {values.Length}");
        for (int i = 0; i < Shapes.Count; i++)
        {
            var s = Shapes[i];
            string who = $"blocks.json: shape {s.NumericId} ('{s.Name}')";
            if (s.NumericId != i || !string.Equals(s.Name, ((BlockShape)i).ToString(), StringComparison.Ordinal))
                throw new InvalidDataException($"{who} does not match BlockShape {i} ({(BlockShape)i})");
            if (s.Rotations is not (1 or 4)) throw new InvalidDataException($"{who} needs 1 or 4 rotations");
            if (s.CostPercent is < 1 or > 100) throw new InvalidDataException($"{who} costPercent must be 1..100");
            if (string.IsNullOrWhiteSpace(s.Label)) throw new InvalidDataException($"{who} needs a label");
        }
        if (Shapes[0].Rotations != 1 || Shapes[0].CostPercent != 100)
            throw new InvalidDataException("blocks.json: shape Full must have 1 rotation and costPercent 100");
    }

    /// <summary>CON-02 and the M1-T1 checks. Each error names the file and the block.</summary>
    private void ValidateBlocks()
    {
        // Block ids must match the BlockId enum exactly, both ways (the enum is used in hot paths).
        foreach (var def in Blocks)
            if (!Enum.IsDefined((BlockId)(byte)def.NumericId))
                throw new InvalidDataException($"blocks.json: block id {def.NumericId} ('{def.Name}') has no BlockId value");
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
        foreach (var def in Blocks)
        {
            string who = $"blocks.json: block {def.NumericId} ('{def.Name}')";
            if (def.NumericId != (int)BlockId.Air && !Palette.Blocks.ContainsKey(def.Name))
                throw new InvalidDataException($"{who} has no palette.json blocks colour");
            if (!def.IsConstruction)
            {
                if (def.BuildTicks != 0) throw new InvalidDataException($"{who} has buildTicks but no cost");
                continue;
            }
            if (def.Cost!.Count != 1) throw new InvalidDataException($"{who} must cost exactly one item type");
            foreach (var (key, n) in def.Cost)
            {
                if (!_itemsByKey.ContainsKey(key)) throw new InvalidDataException($"{who} costs unknown item '{key}'");
                if (n < 1 || n > Agents.Agent.CarryCapacity)
                    throw new InvalidDataException($"{who} costs {n} '{key}'; the count must be 1..{Agents.Agent.CarryCapacity}");
            }
            if (def.BuildTicks < 1) throw new InvalidDataException($"{who} needs buildTicks >= 1");
            if (!def.Solid || !def.Diggable) throw new InvalidDataException($"{who} must be solid and diggable");
            if (def.Drop is not null) throw new InvalidDataException($"{who} must not have a drop (CON-17 refunds the cost)");
            if (string.IsNullOrWhiteSpace(def.Label)) throw new InvalidDataException($"{who} needs a label");
        }
    }

    private void ValidateBuildings()
    {
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
            ValidateStartStock(b);
            if (b.RemovableWhenEmpty && b.Storage is null)
                throw new InvalidDataException($"buildings.json: building '{b.Id}' is removableWhenEmpty but has no storage");
            if (b.Footprint.Length != 3 || b.Footprint.Any(v => v <= 0))
                throw new InvalidDataException($"buildings.json: building '{b.Id}' footprint must be 3 positive ints");
            if (b.Placement is not ("ground" or "waterEdge"))
                throw new InvalidDataException($"buildings.json: building '{b.Id}' has unknown placement '{b.Placement}'");
            if (b.Entrance is null)
            {
                // ADR-076 (M11-T1): only a building nobody works in or stores at may have no entrance.
                if (b.Workers != 0 || b.Storage is not null || b.Producer is not null || b.Placement != "ground" || b.PrebuiltOnly)
                    throw new InvalidDataException($"buildings.json: building '{b.Id}' needs an entrance (it has workers, storage, a producer or a water edge)");
                continue;
            }
            if (b.Entrance.Length != 3)
                throw new InvalidDataException($"buildings.json: building '{b.Id}' entrance must be 3 ints");
            bool inside = b.Entrance[0] >= 0 && b.Entrance[0] < b.Footprint[0] && b.Entrance[2] >= 0 && b.Entrance[2] < b.Footprint[2];
            if (inside || b.Entrance[1] != 0)
                throw new InvalidDataException($"buildings.json: building '{b.Id}' entrance must be outside the footprint at y = 0");
        }
    }

    /// <summary>BLD-15 (M11-T2, ADR-077): a start building's stock is items it stores, within its caps.</summary>
    private void ValidateStartStock(BuildingDef b)
    {
        if (b.StartStock is null) return;
        string who = $"buildings.json: building '{b.Id}'";
        if (b.Storage is null) throw new InvalidDataException($"{who} has startStock but no storage");
        int total = 0;
        foreach (var (key, n) in b.StartStock)
        {
            if (!_itemsByKey.ContainsKey(key)) throw new InvalidDataException($"{who} starts with unknown item '{key}'");
            if (!b.Storage.Accepts.Contains(key, StringComparer.Ordinal))
                throw new InvalidDataException($"{who} starts with '{key}', which it does not store");
            if (n < 1 || (b.Storage.PerItemCapacity > 0 && n > b.Storage.PerItemCapacity))
                throw new InvalidDataException($"{who} starts with {n} '{key}'; the count must be 1..its per-item capacity");
            total += n;
        }
        if (b.Storage.Capacity > 0 && total > b.Storage.Capacity)
            throw new InvalidDataException($"{who} starts with {total} items, over its capacity {b.Storage.Capacity}");
    }

    private static string ReadResource(Assembly asm, string name)
    {
        using var s = asm.GetManifestResourceStream(name)
                      ?? throw new InvalidDataException($"Embedded resource '{name}' not found. Check Aurvangar.Sim.csproj EmbeddedResource.");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    private sealed record BlocksFile(List<BlockDef> Blocks, List<ShapeDef>? Shapes);
    private sealed record ItemsFile(List<ItemDef> Items);
    private sealed record BuildingsFile(List<BuildingDef> Buildings);
}
