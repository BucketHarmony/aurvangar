using System.Text.Json.Nodes;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>M11-T4 content: refined items, refined block costs and workshop data (crafting.md CRF-01..05).</summary>
[Trait("Category", "Unit")]
public class CraftContentTests
{
    private static string Read(string f) => File.ReadAllText(Path.Combine(TestContent.RepoRoot, "data", f));

    /// <summary>Loads the data with buildings.json changed by <paramref name="edit"/> (a JSON tree of the file).</summary>
    internal static ContentDb LoadWith(Action<JsonNode> edit)
    {
        var root = JsonNode.Parse(Read("buildings.json"))!;
        edit(root);
        return ContentDb.Load(Read("blocks.json"), Read("items.json"), root.ToJsonString(), Read("palette.json"));
    }

    internal static JsonObject Building(JsonNode root, string id) =>
        root["buildings"]!.AsArray().Select(n => n!.AsObject()).Single(o => (string)o["id"]! == id);

    [Fact]
    public void RefinedItems_AppendedAfterWater_StoredInHallWarehouseWagon()
    {
        var db = TestContent.Db;
        string[] ids = { "log", "stone", "berries", "potato", "water", "planks", "cutstone" };
        for (int i = 0; i < ids.Length; i++) Assert.Equal(i + 1, db.Item(ids[i]).Value);
        Assert.Equal(6, db.Item("planks").Value);
        Assert.Equal(7, db.Item("cutstone").Value);
        foreach (var id in new[] { "planks", "cutstone" })
        {
            var item = db.ItemDef(db.Item(id));
            Assert.Equal(0, item.Food);
            Assert.Equal(0, item.Drink);
            Assert.True(db.Palette.Items.ContainsKey(id));
            foreach (var b in new[] { "hub", "warehouse", "wagon" }) Assert.Contains(id, db.Building(b).Storage!.Accepts);
        }
        Assert.Equal("#c79a5e", db.Palette.Items["planks"]);
        Assert.Equal("#bdb6a8", db.Palette.Items["cutstone"]);
        var stock = db.Building("wagon").StartStock!;
        Assert.Equal(4, stock.Count);
        Assert.Equal(40, stock["log"]);
        Assert.Equal(60, stock["stone"]);
        Assert.Equal(20, stock["planks"]);
        Assert.Equal(20, stock["cutstone"]);
    }

    [Fact]
    public void RefinedBlocks_CostRefinedItems_RoughKeepRaw()
    {
        var db = TestContent.Db;
        ItemIdOf("planks", 1, BlockId.Planks);
        ItemIdOf("cutstone", 2, BlockId.PolishedStone);
        ItemIdOf("cutstone", 3, BlockId.Slate);
        ItemIdOf("stone", 1, BlockId.Masonry);
        ItemIdOf("stone", 1, BlockId.Rubble);
        ItemIdOf("log", 2, BlockId.Beam);
        // CON-20 shaped costs follow the refined item.
        Assert.Equal((db.Item("cutstone"), 2), db.CostOf(BlockId.Slate, BlockShape.Slab));
        Assert.Equal((db.Item("cutstone"), 3), db.CostOf(BlockId.Slate, BlockShape.Stair));
        Assert.Equal((db.Item("cutstone"), 1), db.CostOf(BlockId.Slate, BlockShape.Pillar));

        void ItemIdOf(string item, int n, BlockId block) => Assert.Equal((db.Item(item), n), db.CostOf(block));
    }

    [Fact]
    public void Workshops_LoadWithRecipes()
    {
        var db = TestContent.Db;
        Assert.Equal(7, db.Buildings.Count);
        foreach (var id in new[] { "sawmill", "stonecutter" })
        {
            var b = db.Building(id);
            Assert.NotNull(b.Workshop);
            Assert.Equal(20, b.Workshop!.OutputBuffer);
            Assert.Equal(10, b.Workshop.HaulAt);
            Assert.Equal(1, b.Workers);
            Assert.Equal(new[] { 0, 0, -1 }, b.Entrance);
            Assert.Equal(new[] { 2, 2, 2 }, b.Footprint);
            Assert.Equal(240, b.BuildTicks);
            Assert.Null(b.Storage);
            Assert.Null(b.Producer);
            Assert.Equal("ground", b.Placement);
            Assert.True(b.SetsBlocks);
            Assert.False(b.PrebuiltOnly);
            Assert.Single(db.RecipesOf(b));
        }
        Assert.Equal(16, db.Building("sawmill").Cost["log"]);
        Assert.Single(db.Building("sawmill").Cost);
        Assert.Equal(8, db.Building("stonecutter").Cost["log"]);
        Assert.Equal(8, db.Building("stonecutter").Cost["stone"]);
        Assert.Null(db.Building("warehouse").Workshop);
        Assert.Empty(db.RecipesOf(db.Building("warehouse")));

        var planks = db.Recipe("planks");
        Assert.Same(db.Building("sawmill"), planks.Workshop);
        Assert.Equal(0, planks.Index);
        Assert.Equal("Saw planks", planks.Name);
        Assert.Equal((db.Item("log"), 1), (planks.Input, planks.InputCount));
        Assert.Equal((db.Item("planks"), 2), (planks.Output, planks.OutputCount));
        Assert.Equal(40, planks.WorkTicks);

        var cut = db.Recipe("cutstone");
        Assert.Same(db.Building("stonecutter"), cut.Workshop);
        Assert.Equal((db.Item("stone"), 1), (cut.Input, cut.InputCount));
        Assert.Equal((db.Item("cutstone"), 1), (cut.Output, cut.OutputCount));
        Assert.Equal(50, cut.WorkTicks);
        Assert.Throws<KeyNotFoundException>(() => db.Recipe("gold"));
    }

    public static TheoryData<string, string> BadWorkshops => new()
    {
        { "workers 0", "exactly 1 worker" },
        { "workers 2", "exactly 1 worker" },
        { "no entrance", "needs an entrance" },
        { "storage", "must not have a storage" },
        { "producer", "must not have a producer" },
        { "waterEdge", "waterEdge" },
        { "no recipes", "has no recipes" },
        { "outputBuffer 0", "outputBuffer" },
        { "haulAt 0", "haulAt" },
        { "haulAt 21", "haulAt" },
        { "two inputs", "recipe 'planks' must have exactly one input" },
        { "unknown output", "recipe 'planks' has unknown output item 'gold'" },
        { "input 11", "recipe 'planks' input count 11" },
        { "output 21", "recipe 'planks' output count 21" },
        { "workTicks 0", "recipe 'planks' needs workTicks" },
        { "recipe id twice", "recipe 'planks': the recipe id is used twice" },
    };

    [Theory]
    [MemberData(nameof(BadWorkshops))]
    public void Workshop_BadData_ThrowsNamingBuildingAndRecipe(string bad, string message)
    {
        var ex = Assert.Throws<InvalidDataException>(() => LoadWith(root =>
        {
            var saw = Building(root, "sawmill");
            var w = saw["workshop"]!.AsObject();
            var recipe = w["recipes"]![0]!.AsObject();
            switch (bad)
            {
                case "workers 0": saw["workers"] = 0; break;
                case "workers 2": saw["workers"] = 2; break;
                case "no entrance": saw["entrance"] = null; break;
                case "storage": saw["storage"] = JsonNode.Parse("""{ "capacity": 10, "perItemCapacity": 0, "accepts": ["planks"] }"""); break;
                case "producer": saw["producer"] = JsonNode.Parse("""{ "output": "water", "cycleTicks": 30, "minIntakeLevel": 256, "unitsPerCycle": 64, "buffer": 10, "haulAt": 5 }"""); break;
                case "waterEdge": saw["placement"] = "waterEdge"; break;
                case "no recipes": w["recipes"] = new JsonArray(); break;
                case "outputBuffer 0": w["outputBuffer"] = 0; break;
                case "haulAt 0": w["haulAt"] = 0; break;
                case "haulAt 21": w["haulAt"] = 21; break;
                case "two inputs": recipe["input"] = JsonNode.Parse("""{ "log": 1, "stone": 1 }"""); break;
                case "unknown output": recipe["output"] = JsonNode.Parse("""{ "gold": 1 }"""); break;
                case "input 11": recipe["input"] = JsonNode.Parse("""{ "log": 11 }"""); break;
                case "output 21": recipe["output"] = JsonNode.Parse("""{ "planks": 21 }"""); break;
                case "workTicks 0": recipe["workTicks"] = 0; break;
                case "recipe id twice":
                    Building(root, "stonecutter")["workshop"]!["recipes"]![0]!["id"] = "planks";
                    break;
            }
        }));
        Assert.Contains("buildings.json", ex.Message);
        Assert.Contains("workshop '", ex.Message);
        Assert.Contains(message, ex.Message);
        if (bad != "recipe id twice") Assert.Contains("'sawmill'", ex.Message);
    }
}
