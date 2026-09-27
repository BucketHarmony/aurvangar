using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>M11-T10: fine block shapes in the sim (construction.md CON-19..22, ADR-080): the shapes in data, the
/// shaped cost, the form in the world, in plan entries, in the command, the save (v7) and the hash.</summary>
[Trait("Category", "Unit")]
public class BlockShapeTests
{
    private static ContentDb Db => TestContent.Db;

    [Fact]
    public void Shapes_InData_MatchTheEnum()
    {
        Assert.Equal(Enum.GetValues<BlockShape>().Length, Db.Shapes.Count);
        Assert.Equal(new[] { "Full", "Slab", "Stair", "Pillar" }, Db.Shapes.Select(s => s.Name));
        Assert.Equal(new[] { 1, 1, 4, 1 }, Db.Shapes.Select(s => s.Rotations));
        Assert.Equal(new[] { 100, 50, 75, 25 }, Db.Shapes.Select(s => s.CostPercent));
        Assert.All(Db.Shapes, s => Assert.False(string.IsNullOrWhiteSpace(s.Label)));
    }

    [Theory]
    // block, full, slab, stair, pillar: max(1, ceil(cost * percent / 100))
    [InlineData(BlockId.Masonry, 1, 1, 1, 1)]
    [InlineData(BlockId.Planks, 1, 1, 1, 1)]
    [InlineData(BlockId.PolishedStone, 2, 1, 2, 1)]
    [InlineData(BlockId.Rubble, 1, 1, 1, 1)]
    [InlineData(BlockId.Beam, 2, 1, 2, 1)]
    [InlineData(BlockId.Slate, 3, 2, 3, 1)]
    public void ShapedCost_IsIntegerPercentOfTheBlockCost(BlockId block, int full, int slab, int stair, int pillar)
    {
        var item = Db.CostOf(block).Item;
        Assert.Equal(Db.CostOf(block), Db.CostOf(block, BlockShape.Full));
        Assert.Equal((item, full), Db.CostOf(block, BlockShape.Full));
        Assert.Equal((item, slab), Db.CostOf(block, BlockShape.Slab));
        Assert.Equal((item, stair), Db.CostOf(block, BlockShape.Stair));
        Assert.Equal((item, pillar), Db.CostOf(block, BlockShape.Pillar));
        Assert.Equal(0, Db.CostOf(BlockId.Stone, BlockShape.Slab).Count);   // not a construction block
    }

    [Fact]
    public void Forms_PackAndValidate()
    {
        Assert.Equal(0, BlockForm.Full.Packed);
        Assert.True(default(BlockForm).IsFull);
        int valid = 0;
        foreach (var shape in Enum.GetValues<BlockShape>())
            for (byte r = 0; r < 4; r++)
            {
                var f = new BlockForm(shape, r);
                Assert.Equal(f, BlockForm.FromPacked(f.Packed));
                if (Db.IsValidForm(f)) valid++;
            }
        Assert.Equal(1 + 1 + 4 + 1, valid);
        Assert.False(Db.IsValidForm(new BlockForm(BlockShape.Slab, 1)));
        Assert.False(Db.IsValidForm(new BlockForm((BlockShape)4, 0)));
        Assert.True(Db.IsValidForm(new BlockForm(BlockShape.Stair, 3)));
    }

    [Fact]
    public void BadShapeData_ThrowsNamingTheShape()
    {
        var root = TestContent.RepoRoot;
        string Read(string f) => File.ReadAllText(Path.Combine(root, "data", f));
        string blocks = Read("blocks.json"), items = Read("items.json"), buildings = Read("buildings.json"), palette = Read("palette.json");
        const string slab = "{ \"id\": 1, \"name\": \"Slab\",   \"label\": \"Slab\",   \"rotations\": 1, \"costPercent\": 50 }";
        Assert.Contains(slab, blocks);
        var cases = new (string What, string Blocks, string Expect)[]
        {
            ("2 rotations", blocks.Replace(slab, slab.Replace("\"rotations\": 1", "\"rotations\": 2")), "shape 1"),
            ("cost 0%", blocks.Replace(slab, slab.Replace("\"costPercent\": 50", "\"costPercent\": 0")), "shape 1"),
            ("cost 150%", blocks.Replace(slab, slab.Replace("\"costPercent\": 50", "\"costPercent\": 150")), "shape 1"),
            ("renamed", blocks.Replace(slab, slab.Replace("\"name\": \"Slab\"", "\"name\": \"Half\"")), "shape 1"),
            ("no label", blocks.Replace(slab, slab.Replace("\"label\": \"Slab\"", "\"label\": \"\"")), "shape 1"),
            ("missing", blocks.Replace(slab + ",", ""), "shapes"),
            ("no list", blocks.Replace("\"shapes\"", "\"forms\""), "shapes"),
            ("full at 50%", blocks.Replace("\"label\": \"Block\",  \"rotations\": 1, \"costPercent\": 100",
                "\"label\": \"Block\",  \"rotations\": 1, \"costPercent\": 50"), "Full"),
        };
        foreach (var c in cases)
        {
            Assert.False(c.Blocks == blocks, $"{c.What}: the edit did not apply");
            var ex = Assert.Throws<InvalidDataException>(() => ContentDb.Load(c.Blocks, items, buildings, palette));
            Assert.True(ex.Message.Contains("blocks.json") && ex.Message.Contains(c.Expect),
                $"{c.What}: message '{ex.Message}' should name blocks.json and '{c.Expect}'");
        }
    }

    [Fact]
    public void World_FormResetByAnyBlockWrite_AndHashedOnlyWhenPresent()
    {
        Simulation Fresh() => new ScenarioBuilder().Ground(8).Build();
        var sim = Fresh();
        var baseHash = sim.StateHash();
        var cell = new Int3(5, 9, 5);
        Assert.False(sim.World.SetForm(cell, new BlockForm(BlockShape.Slab, 0)));   // air takes no form
        Assert.Equal(BlockForm.Full, sim.World.FormAt(cell));
        sim.World.SetBlock(cell, BlockId.Masonry);
        var fullHash = sim.StateHash();
        Assert.True(sim.World.SetForm(cell, new BlockForm(BlockShape.Stair, 2)));
        Assert.Equal(new BlockForm(BlockShape.Stair, 2), sim.World.FormAt(cell));
        var stairHash = sim.StateHash();
        sim.World.SetForm(cell, new BlockForm(BlockShape.Stair, 1));
        Assert.NotEqual(stairHash, sim.StateHash());
        sim.World.SetForm(cell, BlockForm.Full);
        Assert.Equal(0, sim.World.FormCount);
        Assert.Equal(fullHash, sim.StateHash());   // no form: hashes as before M11-T10

        sim.World.SetForm(cell, new BlockForm(BlockShape.Pillar, 0));
        sim.World.SetBlock(cell, BlockId.Slate);   // a new block is Full
        Assert.Equal(BlockForm.Full, sim.World.FormAt(cell));
        sim.World.SetForm(cell, new BlockForm(BlockShape.Pillar, 0));
        sim.World.SetBlock(cell, BlockId.Air);
        Assert.Equal(0, sim.World.FormCount);
        Assert.Equal(baseHash, sim.StateHash());
    }

    [Fact]
    public void PlanEntryForm_IsHashed()
    {
        Simulation Fresh() => new ScenarioBuilder().Ground(8).Build();
        var cell = new Int3(10, 9, 10);
        var hashes = new List<ulong>();
        foreach (var form in new[] { BlockForm.Full, new BlockForm(BlockShape.Slab, 0), new BlockForm(BlockShape.Stair, 0), new BlockForm(BlockShape.Stair, 1) })
        {
            var sim = Fresh();
            sim.Plans.Set(cell, new PlanEntry(BlockId.Masonry, PlanState.Released, form));
            hashes.Add(sim.StateHash());
        }
        Assert.Equal(4, hashes.Distinct().Count());
    }

    private static List<string> Rejections(Simulation sim) =>
        sim.Events.Drain().OfType<CommandRejected>().Where(r => r.Command == "DesignateBuild").Select(r => r.Reason).ToList();

    [Fact]
    public void DesignateBuild_CarriesTheForm_AndRejectsBadOnes()
    {
        var sim = new ScenarioBuilder().Ground(8).Agent(new Int3(3, 9, 3)).Build();
        var a = new Int3(10, 9, 10);
        var b = new Int3(13, 9, 10);
        sim.Events.Drain();

        sim.Enqueue(new DesignateBuild(BuildShape.Line, a, b, 1, BlockId.Masonry, true, new BlockForm((BlockShape)4, 0)));
        sim.Enqueue(new DesignateBuild(BuildShape.Line, a, b, 1, BlockId.Masonry, true, new BlockForm(BlockShape.Slab, 1)));
        sim.Enqueue(new DesignateBuild(BuildShape.Line, a, b, 1, BlockId.Masonry, true, new BlockForm(BlockShape.Stair, 4)));
        sim.Enqueue(new DesignateBuild(BuildShape.Line, a, b, 40, BlockId.Masonry, true, new BlockForm((BlockShape)4, 0)));   // height first
        sim.Enqueue(new DesignateBuild(BuildShape.Line, a, b, 1, BlockId.Stone, true, new BlockForm((BlockShape)4, 0)));      // then the block
        sim.Tick();
        Assert.Equal(new[] { "BadShape", "BadRotation", "BadRotation", "BadHeight", "NotBuildable" }, Rejections(sim));
        Assert.Equal(0, sim.Plans.Count);

        var stair = new BlockForm(BlockShape.Stair, 3);
        sim.Enqueue(new DesignateBuild(BuildShape.Line, a, b, 1, BlockId.Slate, true, stair));
        sim.Tick();
        Assert.Empty(Rejections(sim));
        Assert.Equal(4, sim.Plans.Count);
        Assert.All(sim.Plans.All, p => Assert.Equal(new PlanEntry(BlockId.Slate, PlanState.Planned, stair), p.Entry));
        Assert.Equal(new[] { (Db.Item("cutstone"), 4 * 3) }, sim.Plans.Needed(null));   // M11-T4: Slate costs cut stone

        // Repainting as slabs changes the form and the totals (Slate slab: 2 cut stone).
        sim.Enqueue(new DesignateBuild(BuildShape.Line, a, b, 1, BlockId.Slate, true, new BlockForm(BlockShape.Slab, 0)));
        sim.Tick();
        Assert.All(sim.Plans.All, p => Assert.Equal(BlockShape.Slab, p.Entry.Form.Shape));
        Assert.Equal(new[] { (Db.Item("cutstone"), 4 * 2) }, sim.Plans.Needed(null));

        // The old command (no form) still means Full.
        Assert.Equal(BlockForm.Full, new DesignateBuild(BuildShape.Single, a, a, 1, BlockId.Masonry, false).Form);
    }

    [Fact]
    public void Save_V7_KeepsFormsEntriesAndCommands()
    {
        Assert.Equal(9, SaveGame.FormatVersion);   // 7 in M11-T10; 8 since M11-T4 (workshop orders); 9 since M11-T5 (trader), forms unchanged
        var sim = new ScenarioBuilder().Ground(8).Agent(new Int3(3, 9, 3)).Build();
        var built = new Int3(6, 9, 6);
        sim.World.SetBlock(built, BlockId.Beam);
        sim.World.SetForm(built, new BlockForm(BlockShape.Stair, 1));
        sim.World.SetBlock(built + new Int3(1, 0, 0), BlockId.Masonry);   // Full: not in the forms section
        sim.Enqueue(new DesignateBuild(BuildShape.Line, new Int3(10, 9, 10), new Int3(12, 9, 10), 1, BlockId.Slate, true,
            new BlockForm(BlockShape.Pillar, 0)));
        sim.Enqueue(new DesignateBuild(BuildShape.Single, new Int3(14, 9, 10), default, 1, BlockId.Masonry, true,
            new BlockForm(BlockShape.Slab, 3)));   // rejected (BadRotation); logged raw, so it replays rejected
        sim.Tick();
        sim.Enqueue(new DesignateBuild(BuildShape.Single, new Int3(16, 9, 10), default, 1, BlockId.Planks, false,
            new BlockForm(BlockShape.Stair, 2)));   // pending at save time

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.Equal(sim.World.Forms.ToList(), loaded.World.Forms.ToList());
        Assert.Equal(new BlockForm(BlockShape.Stair, 1), loaded.World.FormAt(built));
        Assert.Equal(sim.Plans.All.ToList(), loaded.Plans.All.ToList());
        Assert.Equal(sim.Commands.Log, loaded.Commands.Log);
        Assert.Equal(new BlockForm(BlockShape.Slab, 3), ((DesignateBuild)loaded.Commands.Log[1].Item2).Form);
        Assert.Equal(1, loaded.Commands.PendingCount);
        sim.Tick(); loaded.Tick();
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.Equal(new BlockForm(BlockShape.Stair, 2), loaded.Plans.Get(new Int3(16, 9, 10))!.Value.Form);
    }
}
