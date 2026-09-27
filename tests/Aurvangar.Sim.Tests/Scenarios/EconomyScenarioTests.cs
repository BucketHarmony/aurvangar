using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Screenshots;
using Aurvangar.ViewCore.Scripts;
using Xunit;
using Xunit.Abstractions;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>One full <see cref="EconomyScript"/> session (seed 1, day 10), run once for the class, with what the checks
/// saw along the way.</summary>
public sealed class EconomyRun
{
    public const long Ticks = 24000;

    public Simulation Sim { get; }
    public List<string> Rejected { get; } = new();
    public List<string> WalledIn { get; } = new();
    public List<string> Floating { get; } = new();
    /// <summary>The tick the last hall cell was built, or -1.</summary>
    public long HallDoneAt { get; } = -1;
    /// <summary>Per workshop def id: the first tick its Keep order was met (colony stock at least the count), or -1.</summary>
    public Dictionary<string, long> KeepMetAt { get; } = new() { ["sawmill"] = -1, ["stonecutter"] = -1 };
    /// <summary>Per workshop def id: ticks it read <see cref="WorkshopStatus.Working"/>.</summary>
    public Dictionary<string, int> WorkingTicks { get; } = new() { ["sawmill"] = 0, ["stonecutter"] = 0 };
    public int AcceptedOffers { get; }
    public int DealsFullyGranted { get; }
    public int TraderVisits { get; }

    public EconomyRun()
    {
        Sim = WorldFactory.Create(EconomyScript.Seed, TestContent.Db);
        var hub = Sim.Buildings.All.First();
        var granted = new HashSet<(int Visit, int Deal)>();
        for (long i = 0; i < Ticks; i++)
        {
            EconomyScript.EnqueueDue(Sim);
            Sim.Tick();
            foreach (var e in Sim.Events.Drain())
            {
                if (e is CommandRejected r) Rejected.Add($"tick {Sim.Clock.Tick}: {r.Command} {r.Reason}");
                if (e is TraderArrived) TraderVisits++;
            }
            for (int d = 0; d < Sim.Trader.Deals.Count; d++)
                if (Sim.Trader.Deals[d].Granted == Sim.Trader.Deals[d].Lots && granted.Add((TraderVisits, d))) DealsFullyGranted++;
            foreach (var b in Sim.Buildings.All)
            {
                if (b.Def.Workshop is null || !KeepMetAt.ContainsKey(b.Def.Id)) continue;
                if (Workshops.StatusOf(Sim, b).Status == WorkshopStatus.Working) WorkingTicks[b.Def.Id]++;
                if (KeepMetAt[b.Def.Id] < 0 && b.OrderFor(0) is { Mode: OrderMode.Keep } o
                    && Economy.Stock(Sim, Sim.Content.RecipesOf(b.Def)[0].Output) >= o.Count)
                    KeepMetAt[b.Def.Id] = Sim.Clock.Tick;
            }
            if (HallDoneAt < 0 && EconomyScript.HallBuilt(Sim)) HallDoneAt = Sim.Clock.Tick;
            if (Sim.Clock.Tick % 100 != 0) continue;
            int hall = Sim.Regions.RegionOf(hub.EntranceCell);
            foreach (var a in Sim.Agents.All)
            {
                if (!a.IsAlive || Sim.Jobs.Get(a.CurrentJob) is { Kind: JobKind.Flee }) continue;
                if (Sim.Regions.RegionOf(a.Cell) != hall) WalledIn.Add($"tick {Sim.Clock.Tick}: {a.Name} at {a.Cell}");
            }
            foreach (var c in Grounding.Floating(Sim)) Floating.Add($"tick {Sim.Clock.Tick}: {c}");
            foreach (var c in Grounding.FloatingPiles(Sim)) Floating.Add($"tick {Sim.Clock.Tick}: pile {c}");
            foreach (var b in Grounding.UnsupportedBuildings(Sim)) Floating.Add($"tick {Sim.Clock.Tick}: {b.Def.Id} {b.Origin}");
        }
        AcceptedOffers = Sim.Commands.Log.Count(c => c.Command is Commands.AcceptOffer);
    }

}

/// <summary>M11-T7: the economy session (crafting.md scenario 10, CRF-P1, ADR-086). The colony builds a Sawmill and a
/// Stonecutter, keeps planks and cut stone stocked, pays the trade wagon in logs for stone, and raises a small hall of
/// Slate, Polished stone and Wood planks by day 10 with everyone alive.</summary>
[Trait("Category", "Scenario")]
public class EconomyScenarioTests : IClassFixture<EconomyRun>
{
    private const long Day = 2400;
    private readonly EconomyRun _run;
    private readonly ITestOutputHelper _out;

    public EconomyScenarioTests(EconomyRun run, ITestOutputHelper output)
    {
        _run = run;
        _out = output;
    }

    [Fact]
    public void Seed1_Economy_HallOfRefinedBlocksByDay10_AllAlive()
    {
        var sim = _run.Sim;
        _out.WriteLine($"hall done at {_run.HallDoneAt}; keep met: sawmill {_run.KeepMetAt["sawmill"]}, stonecutter "
            + $"{_run.KeepMetAt["stonecutter"]}; working ticks: sawmill {_run.WorkingTicks["sawmill"]}, stonecutter "
            + $"{_run.WorkingTicks["stonecutter"]}; {_run.AcceptedOffers} accepts, {_run.DealsFullyGranted} deals granted "
            + $"in {_run.TraderVisits} visits; jobs {sim.Counters.JobsCompleted} done, {sim.Counters.JobsFailed} failed");
        Assert.True(_run.Rejected.Count == 0, string.Join("; ", _run.Rejected.Take(10)));

        // Both workshops stand complete with their Keep orders, and each order was met at least once.
        foreach (var id in new[] { "sawmill", "stonecutter" })
        {
            var ws = Assert.Single(sim.Buildings.All, b => b.Def.Id == id);
            Assert.Equal(BuildingState.Complete, ws.State);
            Assert.Equal(OrderMode.Keep, ws.OrderFor(0)!.Mode);
            Assert.True(_run.KeepMetAt[id] > 0, $"{id} Keep order never met");
            Assert.True(_run.WorkingTicks[id] > 0, $"{id} never worked");
        }
        Assert.Equal(EconomyScript.KeepPlanks, WorkshopScript.First(sim, "sawmill")!.OrderFor(0)!.Count);
        Assert.Equal(EconomyScript.KeepCutStone, WorkshopScript.First(sim, "stonecutter")!.OrderFor(0)!.Count);

        // Trade: at least one offer accepted and one deal fully granted.
        Assert.True(_run.TraderVisits >= 1);
        Assert.True(_run.AcceptedOffers >= 1, "no offer accepted");
        Assert.True(_run.DealsFullyGranted >= 1, "no deal fully granted");

        // The hall: every cell holds its refined block, and all three refined blocks are used.
        Assert.True(_run.HallDoneAt > 0 && _run.HallDoneAt <= 10 * Day, $"hall done at {_run.HallDoneAt}");
        Assert.All(EconomyScript.HallCells, p => Assert.Equal(p.Block, sim.World.GetBlock(p.Cell)));
        var used = EconomyScript.HallCells.Select(p => p.Block).Distinct().ToList();
        Assert.Contains(BlockId.Planks, used);
        Assert.Contains(BlockId.PolishedStone, used);
        Assert.Contains(BlockId.Slate, used);
        Assert.Equal(BlockId.Air, sim.World.GetBlock(EconomyScript.DoorA));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(EconomyScript.DoorB));
        Assert.Equal(0, sim.Plans.Count);

        Assert.Equal(5, sim.Agents.All.Count(a => a.IsAlive));
        Assert.False(sim.Agents.ColonyLost);
    }

    [Fact]
    public void Seed1_Economy_NoDwarfWalledIn_NothingFloats()
    {
        Assert.True(_run.WalledIn.Count == 0, string.Join("; ", _run.WalledIn.Take(10)));
        Assert.True(_run.Floating.Count == 0, string.Join("; ", _run.Floating.Take(10)));
        Assert.Empty(Grounding.Floating(_run.Sim));
        Assert.Empty(Grounding.FloatingPiles(_run.Sim));
        Assert.Empty(Grounding.UnsupportedBuildings(_run.Sim));
    }

    [Fact]
    public void EconomyScript_AvailableToHeadlessAndScreenshots()
    {
        Assert.Contains("economy", ScreenshotScripts.Names);
        Assert.True(ScreenshotScripts.IsTimed("economy"));
        Assert.True(ScreenshotScripts.IsTimed("survival"));
        Assert.Empty(ScreenshotScripts.For("economy", WorldFactory.Create(EconomyScript.Seed, TestContent.Db)));
        Assert.Contains("economy", ScreenshotPresets.Names);

        const int n = 6000;
        var harness = WorldFactory.Create(EconomyScript.Seed, TestContent.Db);
        ScreenshotScripts.Run("economy", harness, n, () => harness.Events.Drain());
        var direct = WorldFactory.Create(EconomyScript.Seed, TestContent.Db);
        EconomyScript.Run(direct, n);
        Assert.Equal(n, harness.Clock.Tick);
        Assert.Equal(direct.StateHash(), harness.StateHash());
        Assert.Equal(direct.Commands.Log, harness.Commands.Log);

        // The headless runner accepts --script economy: it takes any name in ScreenshotScripts.Names and runs timed
        // scripts through ScreenshotScripts.EnqueueDue, as checked here.
        Assert.Contains(harness.Commands.Log, c => c.Command is Commands.AcceptOffer);
        Assert.Contains(harness.Commands.Log, c => c.Command is Commands.ReleasePlan);
    }
}
