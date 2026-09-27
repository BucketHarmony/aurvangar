using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Tools;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M11-T2 (G6: "can they arrive with a wagon full of building resources for us to start with?"): the colony
/// starts with a Wagon, a prebuilt storage building beside the Great Hall, defined in data (BLD-15..17, ADR-077). It
/// holds the starting building supplies, takes no new stock, is a source for builders, and may be torn down for logs
/// once empty. The starting stock of every start building is data (<c>startStock</c>).</summary>
[Trait("Category", "Scenario")]
public class WagonScenarioTests
{
    private static ContentDb Db => TestContent.Db;

    private static Building Wagon(Simulation sim) => sim.Buildings.All.Single(b => b.Def.Id == "wagon");

    private static Dictionary<int, int> StockOf(BuildingDef def) =>
        (def.StartStock ?? new Dictionary<string, int>()).ToDictionary(kv => Db.Item(kv.Key).Value, kv => kv.Value);

    [Fact]
    public void Wagon_InData_IsAPrebuiltStorageThatTakesNothingNew()
    {
        var wagon = Db.Building("wagon");
        Assert.True(wagon.PrebuiltOnly);
        Assert.True(wagon.HasEntrance);
        Assert.NotNull(wagon.Storage);
        Assert.False(wagon.Storage!.Receives);
        Assert.True(wagon.RemovableWhenEmpty);
        Assert.True(wagon.StartStock!["stone"] >= 60);
        Assert.True(wagon.StartStock["log"] >= 40);
        Assert.True(wagon.Cost["log"] > 0);   // the teardown refund (BLD-09: half the cost)

        var hub = Db.Building("hub");
        Assert.Equal(40, hub.StartStock!["berries"]);
        Assert.Equal(30, hub.StartStock["water"]);
        Assert.True(hub.Storage!.Receives);
        Assert.False(hub.RemovableWhenEmpty);
        Assert.Null(Db.Building("warehouse").StartStock);

        var sim = new ScenarioBuilder().Ground(G - 1).Build();
        Assert.Equal(PlacementResult.PrebuiltOnly, sim.Buildings.CanPlace(wagon, new Int3(10, G, 10), 0));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void World_StartsWithWagonBesideTheHall_HoldingTheDataStock(ulong seed)
    {
        var sim = WorldFactory.Create(seed, Db);
        var hub = Hub(sim);
        var wagon = Wagon(sim);
        Assert.Equal(BuildingState.Complete, wagon.State);
        Assert.Equal(StockOf(hub.Def), hub.Stored.ToDictionary(kv => kv.Key, kv => kv.Value));
        Assert.Equal(StockOf(wagon.Def), wagon.Stored.ToDictionary(kv => kv.Key, kv => kv.Value));
        foreach (var c in wagon.FootprintCells()) Assert.Equal(BlockId.BuildingSolid, sim.World.GetBlock(c));

        // Next to the hall with a walkable gap: 2..4 cells apart (Chebyshev, x/z) between the footprints.
        int gap = int.MaxValue;
        foreach (var a in hub.FootprintCells())
            foreach (var b in wagon.FootprintCells())
                gap = Math.Min(gap, Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z)));
        Assert.InRange(gap, 2, 4);

        // Its entrance is dry, standable and in the hall's region (reachable).
        var e = wagon.EntranceCell;
        Assert.True(sim.PathGrid.IsStandable(e));
        Assert.False(sim.PathGrid.IsWet(e));
        sim.Tick();
        int region = sim.Regions.RegionOf(hub.EntranceCell);
        Assert.NotEqual(Paths.Regions.None, region);
        Assert.Equal(region, sim.Regions.RegionOf(e));
        Assert.All(sim.Agents.All, a => Assert.Equal(region, sim.Regions.RegionOf(a.Cell)));
    }

    [Fact]
    public void Seed1_Hud_ShowsTheWagonStock()
    {
        var sim = WorldFactory.Create(1, Db);
        var totals = TopBarModel.Totals(sim).ToDictionary(t => t.Name, t => t.Count);
        Assert.True(totals["Stone"] >= 60);
        Assert.True(totals["Log"] >= 40);
        Assert.Equal(40, totals["Berries"]);
        Assert.Equal(30, totals["Water"]);

        // Drawn with wheels in its own colour (VIEW-09); the hall has none.
        var visuals = BuildingVisuals.Build(sim, WorldFactory.SizeY - 1, new EntityColors(Db));
        var w = visuals.Single(v => v.Id == Wagon(sim).Id);
        Assert.True(w.Wheels);
        Assert.False(visuals.Single(v => v.Id == Hub(sim).Id).Wheels);
        Assert.NotEqual(visuals.Single(v => v.Id == Hub(sim).Id).Color, w.Color);
    }

    /// <summary>Hub at (20,5,20), wagon at (10,5,20) with stock, two agents between them.</summary>
    private static Simulation Yard(Action<ScenarioBuilder>? more = null)
    {
        var b = new ScenarioBuilder().Ground(G - 1)
            .Hub(new Int3(20, G, 20)).Stock("water", 30).Stock("berries", 30)
            .Storage("wagon", new Int3(10, G, 20)).Stock("log", 40).Stock("stone", 60)
            .Agent(new Int3(15, G, 15)).Agent(new Int3(16, G, 15));
        more?.Invoke(b);
        return b.Build();
    }

    [Fact]
    public void Wagon_TakesNoNewStock_HaulsGoToTheHall()
    {
        var sim = Yard(b => b.Pile(new Int3(9, G, 17), "log", 6));
        var wagon = Wagon(sim);
        var log = Db.Item("log");
        Assert.Equal(0, sim.Jobs.StorageRoom(wagon, log));
        Assert.Equal(0, Actions.WorldActions.FreeCapacity(wagon, log));
        Assert.NotSame(wagon, Jobs.HaulSystem.NearestStorage(sim, wagon.EntranceCell, log, 1));

        RunUntil(sim, () => sim.Piles.At(new Int3(9, G, 17)).IsEmpty && sim.Agents.All.All(a => a.Carried.IsEmpty), 3000);
        Assert.Equal(40, Stored(wagon, "log"));
        Assert.Equal(6, Stored(Hub(sim), "log"));
    }

    [Fact]
    public void Wagon_Stock_BuildsAWarehouse()
    {
        var sim = Yard();
        var wagon = Wagon(sim);
        sim.Enqueue(new PlaceBuilding("warehouse", new Int3(14, G, 26), 0));
        sim.Tick();
        var wh = sim.Buildings.All.Single(b => b.Def.Id == "warehouse");
        RunUntil(sim, () => wh.State == BuildingState.Complete, 4000);
        Assert.Equal(40 - wh.Def.Cost["log"], Stored(wagon, "log"));
        Assert.Equal(60, Stored(wagon, "stone"));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    [Fact]
    public void Wagon_TornDownOnlyWhenEmpty_ForLogs()
    {
        var sim = Yard();
        var wagon = Wagon(sim);
        Assert.NotNull(DeconstructTool.Refusal(sim, wagon));
        Assert.NotNull(DeconstructTool.Refusal(sim, Hub(sim)));
        sim.Enqueue(new Deconstruct(wagon.Id));
        sim.Enqueue(new Deconstruct(Hub(sim).Id));
        sim.Tick();
        var rejected = sim.Events.Drain().OfType<CommandRejected>().Select(r => r.Reason).ToList();
        Assert.Equal(new[] { "NotEmpty", "PrebuiltOnly" }, rejected);
        Assert.Equal(BuildingState.Complete, wagon.State);

        // Empty it (as builders would), then it may be torn down: half its cost comes back as logs.
        wagon.Stored.Clear();
        Assert.Null(DeconstructTool.Refusal(sim, wagon));
        sim.Enqueue(new Deconstruct(wagon.Id));
        sim.Tick();
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        Assert.Equal(BuildingState.Deconstructing, wagon.State);
        RunUntil(sim, () => sim.Buildings.Get(wagon.Id) is null, 2000);
        foreach (var c in wagon.FootprintCells()) Assert.Equal(BlockId.Air, sim.World.GetBlock(c));
        RunUntil(sim, () => Stored(Hub(sim), "log") == wagon.Def.Cost["log"] / 2, 3000);
    }

    [Fact]
    public void Content_StartStockMustBeStorable()
    {
        string Read(string f) => File.ReadAllText(Path.Combine(TestContent.RepoRoot, "data", f));
        var bad = Read("buildings.json").Replace("\"startStock\": { \"log\": 40", "\"startStock\": { \"potato\": 40");
        Assert.NotEqual(Read("buildings.json"), bad);
        var ex = Assert.Throws<InvalidDataException>(() => ContentDb.Load(Read("blocks.json"), Read("items.json"), bad, Read("palette.json")));
        Assert.Contains("wagon", ex.Message);
    }
}
