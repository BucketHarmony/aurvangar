using Aurvangar.Sim.Content;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

public class ContentDbTests
{
    [Fact]
    public void LoadEmbedded_LoadsAllFiles()
    {
        var db = TestContent.Db;
        Assert.Equal(14, db.Blocks.Count);   // M8-T2: construction blocks 8..10 (CON-01); M9-T4: 11..13
        Assert.Equal(5, db.Items.Count - 1);
        Assert.Equal(5, db.Buildings.Count);   // M11-T2: the wagon
        Assert.NotEmpty(db.Palette.Blocks);
    }

    [Fact]
    public void SolidTable_MatchesBlocks()
    {
        var db = TestContent.Db;
        Assert.False(db.SolidTable[(int)BlockId.Air]);
        Assert.True(db.SolidTable[(int)BlockId.Stone]);
        Assert.True(db.SolidTable[(int)BlockId.BuildingSolid]);
    }

    [Fact]
    public void Lookups_Work()
    {
        var db = TestContent.Db;
        Assert.Equal("water", db.ItemDef(db.Item("water")).Id);
        Assert.Equal(5000, db.ItemDef(db.Item("water")).Drink);
        Assert.Equal(20, db.Building("warehouse").Cost["log"]);
        Assert.Equal("stone", db.Block(BlockId.Stone).Drop);
        Assert.Throws<KeyNotFoundException>(() => db.Item("gold"));
    }

    [Fact]
    public void Validation_RejectsUnknownCostItem()
    {
        var root = TestContent.RepoRoot;
        string Read(string f) => File.ReadAllText(Path.Combine(root, "data", f));
        var badBuildings = Read("buildings.json").Replace("\"log\": 20", "\"gold\": 20");
        var ex = Assert.Throws<InvalidDataException>(() =>
            ContentDb.Load(Read("blocks.json"), Read("items.json"), badBuildings, Read("palette.json")));
        Assert.Contains("gold", ex.Message);
    }

    [Fact]
    public void Validation_RejectsBlockNameMismatch()
    {
        var root = TestContent.RepoRoot;
        string Read(string f) => File.ReadAllText(Path.Combine(root, "data", f));
        var badBlocks = Read("blocks.json").Replace("\"Sand\"", "\"Gravel\"");
        Assert.Throws<InvalidDataException>(() =>
            ContentDb.Load(badBlocks, Read("items.json"), Read("buildings.json"), Read("palette.json")));
    }
}
