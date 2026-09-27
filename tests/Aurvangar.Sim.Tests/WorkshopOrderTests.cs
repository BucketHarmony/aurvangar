using System.Text.Json.Nodes;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>M11-T4: workshop orders, colony stock and the active-order plan (crafting.md CRF-06..09, CRF-22). Worlds
/// are flat stone to y = 4. The sawmill at (10,5,10) covers x 10..11, z 10..11, y 5..6; its entrance is (10,5,9).</summary>
[Trait("Category", "Unit")]
public class WorkshopOrderTests
{
    private const int G = 5;
    private static readonly Int3 MillOrigin = new(10, G, 10);
    private static readonly Int3 HubOrigin = new(20, G, 20);

    private static ItemId Item(Simulation sim, string id) => sim.Content.Item(id);

    /// <summary>The shipped data with a second sawmill recipe: 1 stone -> 1 planks ("chips", index 1).</summary>
    private static readonly Lazy<ContentDb> TwoRecipes = new(() => CraftContentTests.LoadWith(root =>
        CraftContentTests.Building(root, "sawmill")["workshop"]!["recipes"]!.AsArray().Add(JsonNode.Parse(
            """{ "id": "chips", "name": "Split stone", "input": { "stone": 1 }, "output": { "planks": 1 }, "workTicks": 10 }"""))));

    private static Building Mill(Simulation sim) => sim.Buildings.All.Single(b => b.Def.Id == "sawmill");

    private static Simulation MillWorld(ContentDb? db = null, int logs = 0, int planks = 0)
    {
        var b = new ScenarioBuilder(content: db).Ground(G - 1).Hub(HubOrigin);
        if (logs > 0) b.Stock("log", logs);
        if (planks > 0) b.Stock("planks", planks);
        var sim = b.Build();
        sim.Buildings.PlacePrebuilt(sim.Content.Building("sawmill"), MillOrigin, 0);
        return sim;
    }

    private static List<CommandRejected> Order(Simulation sim, Building b, int recipe, OrderMode mode, int count)
    {
        sim.Events.Drain();
        sim.Enqueue(new SetWorkshopOrder(b.Id, recipe, mode, count));
        sim.Tick();
        return sim.Events.Drain().OfType<CommandRejected>().ToList();
    }

    private static List<(int Recipe, OrderMode Mode, int Count, int Done)> Orders(Building b) =>
        b.Orders.Select(o => (o.Recipe, o.Mode, o.Count, o.Done)).ToList();

    [Fact]
    public void SetWorkshopOrder_SetsReplacesRemoves_InRecipeOrder()
    {
        var sim = MillWorld(TwoRecipes.Value);
        var mill = Mill(sim);
        Assert.Equal(2, sim.Content.RecipesOf(mill.Def).Count);

        Assert.Empty(Order(sim, mill, 1, OrderMode.Keep, 5));
        Assert.Empty(Order(sim, mill, 0, OrderMode.Make, 20));
        Assert.Equal(new[] { (0, OrderMode.Make, 20, 0), (1, OrderMode.Keep, 5, 0) }, Orders(mill));

        mill.Orders[0].Done = 6;
        Assert.Empty(Order(sim, mill, 0, OrderMode.Make, 30));   // replaced: done goes back to 0
        Assert.Equal(new[] { (0, OrderMode.Make, 30, 0), (1, OrderMode.Keep, 5, 0) }, Orders(mill));
        Assert.Empty(Order(sim, mill, 1, OrderMode.Make, 999));   // the mode may change too
        Assert.Equal((1, OrderMode.Make, 999, 0), Orders(mill)[1]);

        Assert.Empty(Order(sim, mill, 0, OrderMode.Make, 0));   // count 0 removes
        Assert.Equal(new[] { (1, OrderMode.Make, 999, 0) }, Orders(mill));

        // CRF-07: orders may be set on a blueprint.
        sim.Enqueue(new PlaceBuilding("sawmill", new Int3(4, G, 4), 0));
        sim.Tick();
        var bp = sim.Buildings.All.Single(b => b.Def.Id == "sawmill" && b.State == BuildingState.Blueprint);
        Assert.Empty(Order(sim, bp, 0, OrderMode.Keep, 10));
        Assert.Equal(new[] { (0, OrderMode.Keep, 10, 0) }, Orders(bp));
    }

    [Fact]
    public void SetWorkshopOrder_Rejections_InOrder()
    {
        var sim = MillWorld();
        var mill = Mill(sim);
        var hub = sim.Buildings.All.Single(b => b.Def.Id == "hub");
        mill.Orders.Add(new WorkshopOrder { Recipe = 0, Mode = OrderMode.Make, Count = 4, Done = 1 });
        var before = sim.StateHash();

        void Rejected(string reason, BuildingId id, int recipe, OrderMode mode, int count)
        {
            sim.Events.Drain();
            sim.Enqueue(new SetWorkshopOrder(id, recipe, mode, count));
            sim.Tick();
            var r = Assert.Single(sim.Events.Drain().OfType<CommandRejected>());
            Assert.Equal(new CommandRejected("SetWorkshopOrder", reason), r);
            Assert.Equal(new[] { (0, OrderMode.Make, 4, 1) }, Orders(mill));
        }

        Rejected("NoSuchBuilding", new BuildingId(99), 0, OrderMode.Make, 5);
        Rejected("NotAWorkshop", hub.Id, 0, OrderMode.Make, 5);
        Rejected("BadRecipe", mill.Id, 1, OrderMode.Make, 5);
        Rejected("BadRecipe", mill.Id, -1, OrderMode.Make, 5);
        Rejected("BadMode", mill.Id, 0, (OrderMode)9, 5);
        Rejected("BadCount", mill.Id, 0, OrderMode.Make, -1);
        Rejected("BadCount", mill.Id, 0, OrderMode.Make, 1000);
        Rejected("NotAWorkshop", hub.Id, 1, (OrderMode)9, -1);   // first failing check wins

        var empty = MillWorld();
        empty.Events.Drain();
        empty.Enqueue(new SetWorkshopOrder(Mill(empty).Id, 0, OrderMode.Keep, 0));
        empty.Tick();
        Assert.Equal(new CommandRejected("SetWorkshopOrder", "NothingToRemove"), Assert.Single(empty.Events.Drain().OfType<CommandRejected>()));
        Assert.Empty(Mill(empty).Orders);

        // No order changed: the same world without the rejected commands hashes the same.
        var twin = MillWorld();
        Mill(twin).Orders.Add(new WorkshopOrder { Recipe = 0, Mode = OrderMode.Make, Count = 4, Done = 1 });
        Assert.Equal(before, twin.StateHash());
        for (int i = 0; i < 8; i++) twin.Tick();
        Assert.Equal(sim.StateHash(), twin.StateHash());
    }

    [Fact]
    public void ColonyStock_CountsStorageWorkshopOutputAndCarried_NotPiles()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(HubOrigin).Stock("planks", 7)
            .Pile(new Int3(3, G, 3), "planks", 9).Agent(new Int3(5, G, 5)).Agent(new Int3(6, G, 5)).Build();
        var planks = Item(sim, "planks");
        Assert.Equal(7, Economy.Stock(sim, planks));

        var mill = sim.Buildings.PlacePrebuilt(sim.Content.Building("sawmill"), MillOrigin, 0);
        mill.Stored[planks.Value] = 4;
        Assert.Equal(11, Economy.Stock(sim, planks));

        var agents = sim.Agents.All.ToList();
        agents[0].Carried = new ItemStack(planks, 3);
        agents[1].Carried = new ItemStack(Item(sim, "log"), 5);
        Assert.Equal(14, Economy.Stock(sim, planks));
        Assert.Equal(5, Economy.Stock(sim, Item(sim, "log")));   // carried logs count as logs, not planks
        agents[1].Carried = ItemStack.Empty;

        // A blueprint warehouse's delivered or stored items do not count; the loose pile never does.
        Assert.Equal(PlacementResult.Ok, sim.Buildings.TryPlaceBlueprint(sim.Content.Building("warehouse"), new Int3(4, G, 12), 0, out var bp));
        bp!.Stored[planks.Value] = 50;
        Assert.Equal(14, Economy.Stock(sim, planks));
        Assert.Equal(9, sim.Piles.All.Single().Stack.Count);
    }

    [Fact]
    public void ActiveOrder_AndCycleCount()
    {
        var planks = TestContent.Db.Item("planks");

        // Make 20 planks with 30 logs stored: k = min(ceil(20/2), 10/1, 20/2, 30) = 10.
        var sim = MillWorld(logs: 30);
        var mill = Mill(sim);
        var hub = sim.Buildings.All.Single(b => b.Def.Id == "hub");
        mill.Orders.Add(new WorkshopOrder { Recipe = 0, Mode = OrderMode.Make, Count = 20 });
        var plan = Workshops.Plan(sim, mill)!.Value;
        Assert.Equal((sim.Content.Recipe("planks"), hub, 10), (plan.Recipe, plan.Source, plan.Cycles));

        // Keep 10 planks with 6 in stock: 4 wanted, ceil(4/2) = 2.
        sim = MillWorld(logs: 30, planks: 6);
        mill = Mill(sim);
        mill.Orders.Add(new WorkshopOrder { Recipe = 0, Mode = OrderMode.Keep, Count = 10 });
        Assert.Equal(2, Workshops.Plan(sim, mill)!.Value.Cycles);

        // With 3 logs unreserved, k = 3 (capped by input).
        sim = MillWorld(logs: 3);
        mill = Mill(sim);
        mill.Orders.Add(new WorkshopOrder { Recipe = 0, Mode = OrderMode.Make, Count = 20 });
        Assert.Equal(3, Workshops.Plan(sim, mill)!.Value.Cycles);

        // With 18 planks in the output buffer (20), k = 1.
        sim = MillWorld(logs: 30);
        mill = Mill(sim);
        mill.Orders.Add(new WorkshopOrder { Recipe = 0, Mode = OrderMode.Make, Count = 20 });
        mill.Stored[planks.Value] = 18;
        Assert.Equal(1, Workshops.Plan(sim, mill)!.Value.Cycles);
        mill.Stored[planks.Value] = 19;   // no room for one more cycle of 2
        Assert.Null(Workshops.Plan(sim, mill));

        // An order with no input anywhere is skipped for the next one in recipe order.
        sim = MillWorld(TwoRecipes.Value);
        mill = Mill(sim);
        hub = sim.Buildings.All.Single(b => b.Def.Id == "hub");
        hub.Stored[sim.Content.Item("stone").Value] = 4;
        mill.Orders.Add(new WorkshopOrder { Recipe = 0, Mode = OrderMode.Make, Count = 20 });
        mill.Orders.Add(new WorkshopOrder { Recipe = 1, Mode = OrderMode.Make, Count = 6 });
        plan = Workshops.Plan(sim, mill)!.Value;
        Assert.Equal((1, 4), (plan.Recipe.Index, plan.Cycles));

        // A satisfied order is skipped too; with nothing wanted there is no plan.
        mill.Orders[1].Done = 6;
        Assert.Null(Workshops.Plan(sim, mill));
    }

    [Fact]
    public void WorkshopOrders_SavedAndHashed_FormatVersion8()
    {
        Assert.Equal(9, SaveGame.FormatVersion);   // 8 in M11-T4; 9 since M11-T5 (trader), orders unchanged
        var sim = MillWorld(logs: 5);
        var mill = Mill(sim);
        var hub = sim.Buildings.All.Single(b => b.Def.Id == "hub");
        var h0 = sim.StateHash();

        var o = new WorkshopOrder { Recipe = 0, Mode = OrderMode.Make, Count = 20 };
        mill.Orders.Add(o);
        var h1 = sim.StateHash();
        Assert.NotEqual(h0, h1);
        mill.Orders[0] = new WorkshopOrder { Recipe = 0, Mode = OrderMode.Keep, Count = 20 };
        Assert.NotEqual(h1, sim.StateHash());
        mill.Orders[0] = new WorkshopOrder { Recipe = 0, Mode = OrderMode.Make, Count = 21 };
        Assert.NotEqual(h1, sim.StateHash());
        mill.Orders[0] = o;
        o.Done = 2;
        Assert.NotEqual(h1, sim.StateHash());
        o.Done = 0;
        Assert.Equal(h1, sim.StateHash());

        // A building without a workshop block hashes as before, whatever its (unused) order list holds.
        hub.Orders.Add(new WorkshopOrder { Recipe = 0, Mode = OrderMode.Keep, Count = 3 });
        Assert.Equal(h1, sim.StateHash());
        hub.Orders.Clear();

        // Commands: one accepted, one rejected (BadMode) and one pending at save time.
        sim.Enqueue(new SetWorkshopOrder(mill.Id, 0, OrderMode.Keep, 7));
        sim.Enqueue(new SetWorkshopOrder(mill.Id, 0, (OrderMode)9, 3));
        sim.Tick();
        mill.Orders[0].Done = 0;
        sim.Enqueue(new SetWorkshopOrder(mill.Id, 0, OrderMode.Make, 12));
        byte[] bytes;
        using (var ms = new MemoryStream()) { SaveGame.Save(sim, ms); bytes = ms.ToArray(); }
        var loaded = SaveGame.Load(new MemoryStream(bytes), TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.Equal(Orders(mill), Orders(Mill(loaded)));
        Assert.Equal(sim.Commands.Log, loaded.Commands.Log);
        Assert.Equal((OrderMode)9, ((SetWorkshopOrder)loaded.Commands.Log[1].Item2).Mode);
        Assert.Equal(1, loaded.Commands.PendingCount);
        for (int t = 0; t < 50; t++) { sim.Tick(); loaded.Tick(); }
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.Equal(Orders(mill), Orders(Mill(loaded)));

        // The decoded log replays with the rejected command still rejected.
        var replay = MillWorld(logs: 5);
        replay.Events.Drain();
        foreach (var (_, cmd) in loaded.Commands.Log.Take(2)) replay.Enqueue(cmd);
        replay.Tick();
        Assert.Equal(new CommandRejected("SetWorkshopOrder", "BadMode"), Assert.Single(replay.Events.Drain().OfType<CommandRejected>()));
        Assert.Equal(new[] { (0, OrderMode.Keep, 7, 0) }, Orders(Mill(replay)));

        // SAV-04: a version 7 file is refused.
        var v7 = (byte[])bytes.Clone();
        BitConverter.GetBytes(7).CopyTo(v7, 4);
        var ex = Assert.Throws<InvalidDataException>(() => SaveGame.Load(new MemoryStream(v7), TestContent.Db));
        Assert.Contains("version 7", ex.Message);
    }
}
