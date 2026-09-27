using Aurvangar.Sim.Content;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>CON-01/02 (M8-T2): construction block types in data/blocks.json and their validation.</summary>
[Trait("Category", "Unit")]
public class BlockContentTests
{
    [Fact]
    public void ConstructionBlocks_LoadWithCostLabelAndColour()
    {
        var db = TestContent.Db;
        Assert.Equal(11, db.Blocks.Count);
        Assert.Equal(8, (int)BlockId.Masonry);
        Assert.Equal(9, (int)BlockId.Planks);
        Assert.Equal(10, (int)BlockId.PolishedStone);
        var expected = new (BlockId Id, string Label, string Item, int Cost, int Ticks, int Hardness)[]
        {
            (BlockId.Masonry, "Stone wall", "stone", 1, 20, 40),
            (BlockId.Planks, "Wood planks", "log", 1, 15, 20),
            (BlockId.PolishedStone, "Polished stone", "stone", 2, 40, 60),
        };
        foreach (var e in expected)
        {
            var def = db.Block(e.Id);
            Assert.True(def.IsConstruction);
            Assert.Equal(e.Label, def.Label);
            Assert.Equal(e.Label, db.LabelOf(e.Id));
            Assert.Equal((db.Item(e.Item), e.Cost), db.CostOf(e.Id));
            Assert.Equal(e.Ticks, def.BuildTicks);
            Assert.Equal(e.Hardness, def.Hardness);
            Assert.True(def.Solid && def.Diggable);
            Assert.Null(def.Drop);
            Assert.True(db.Palette.Blocks.ContainsKey(e.Id.ToString()), $"no palette colour for {e.Id}");
        }
        foreach (var id in Enum.GetValues<BlockId>())
            Assert.Equal(id is BlockId.Masonry or BlockId.Planks or BlockId.PolishedStone, db.IsConstruction(id));
    }

    [Fact]
    public void ConstructionBlock_BadData_ThrowsNamingFileAndId()
    {
        var root = TestContent.RepoRoot;
        string Read(string f) => File.ReadAllText(Path.Combine(root, "data", f));
        string blocks = Read("blocks.json"), palette = Read("palette.json");
        string items = Read("items.json"), buildings = Read("buildings.json");
        const string masonryCost = "\"cost\": { \"stone\": 1 }, \"buildTicks\": 20";
        string WithCost(string replacement) => blocks.Replace(masonryCost, replacement);

        var cases = new (string What, string Blocks, string Palette, string Id)[]
        {
            ("two cost items", WithCost("\"cost\": { \"stone\": 1, \"log\": 1 }, \"buildTicks\": 20"), palette, "8"),
            ("unknown cost item", WithCost("\"cost\": { \"gold\": 1 }, \"buildTicks\": 20"), palette, "8"),
            ("count 0", WithCost("\"cost\": { \"stone\": 0 }, \"buildTicks\": 20"), palette, "8"),
            ("count 11", WithCost("\"cost\": { \"stone\": 11 }, \"buildTicks\": 20"), palette, "8"),
            ("buildTicks 0", WithCost("\"cost\": { \"stone\": 1 }, \"buildTicks\": 0"), palette, "8"),
            ("a drop", blocks.Replace("\"hardness\": 40, \"drop\": null", "\"hardness\": 40, \"drop\": \"stone\""), palette, "8"),
            ("no label", blocks.Replace("\"label\": \"Stone wall\",", ""), palette, "8"),
            ("buildTicks without cost",
                blocks.Replace("\"hardness\": 60, \"drop\": \"stone\"", "\"hardness\": 60, \"drop\": \"stone\", \"buildTicks\": 5"), palette, "2"),
            ("no palette colour", blocks, palette.Replace("\"Masonry\": \"#a39e94\",", ""), "8"),
            ("json id with no BlockId value", blocks.Replace("\"buildTicks\": 40 }",
                "\"buildTicks\": 40 },\n    { \"id\": 11, \"name\": \"Glass\", \"solid\": true, \"diggable\": true, \"hardness\": 10, \"drop\": null }"),
                palette, "11"),
        };
        foreach (var c in cases)
        {
            Assert.False(c.Blocks == blocks && c.Palette == palette, $"{c.What}: the edit did not apply");
            var ex = Assert.Throws<InvalidDataException>(() => ContentDb.Load(c.Blocks, items, buildings, c.Palette));
            Assert.True(ex.Message.Contains("blocks.json") && ex.Message.Contains($" {c.Id} "),
                $"{c.What}: message '{ex.Message}' should name blocks.json and block {c.Id}");
        }
    }
}
