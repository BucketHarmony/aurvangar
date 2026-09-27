using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Screenshots;
using Aurvangar.ViewCore.Scripts;
using Xunit;
using Xunit.Abstractions;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>One full <see cref="MonumentScript"/> session (seed 1, day 10), run once for the class, with what the
/// invariant checks saw every 100 ticks.</summary>
public sealed class MonumentRun
{
    public const long Ticks = 24000;

    public Simulation Sim { get; }
    public List<string> Rejected { get; } = new();
    public List<string> WalledIn { get; } = new();
    public List<string> Floating { get; } = new();
    /// <summary>The tick the last planned cell was built, or -1.</summary>
    public long CompletedAt { get; } = -1;
    public int MaxBuiltMidway { get; }
    /// <summary>Build trips: each time a dwarf is past the pickup of a Build job it claimed (a re-claim after a failure
    /// is a new trip).</summary>
    public int BuildTrips { get; }
    /// <summary>M10-T1: every entry's CON-05 status at the G5 screenshot ticks (11,000 and 14,000).</summary>
    public Dictionary<long, List<(Int3 Cell, PlanEntry Entry, BuildStatus Status)>> StatusesAt { get; } = new();

    public MonumentRun()
    {
        Sim = WorldFactory.Create(MonumentScript.Seed, TestContent.Db);
        var hub = Sim.Buildings.All.First();
        var tripOf = new Dictionary<int, int>();   // agent id -> the Build job it is counted on (lookups only)
        for (long i = 0; i < Ticks; i++)
        {
            MonumentScript.EnqueueDue(Sim);
            Sim.Tick();
            foreach (var a in Sim.Agents.All)
            {
                bool carrying = Sim.Jobs.Get(a.CurrentJob) is { Kind: JobKind.Build } && a.StepIndex >= 2;
                if (!carrying) { tripOf.Remove(a.Id.Value); continue; }
                if (tripOf.TryGetValue(a.Id.Value, out var j) && j == a.CurrentJob.Value) continue;
                tripOf[a.Id.Value] = a.CurrentJob.Value;
                BuildTrips++;
            }
            foreach (var e in Sim.Events.Drain())
                if (e is CommandRejected r) Rejected.Add($"tick {Sim.Clock.Tick}: {r.Command} {r.Reason}");
            if (Sim.Clock.Tick is 11000 or 14000) StatusesAt[Sim.Clock.Tick] = Sim.Plans.Statuses(Sim);
            if (Sim.Clock.Tick % 100 != 0) continue;
            int hall = Sim.Regions.RegionOf(hub.EntranceCell);
            foreach (var a in Sim.Agents.All)
            {
                if (!a.IsAlive || Sim.Jobs.Get(a.CurrentJob) is { Kind: JobKind.Flee }) continue;
                if (Sim.Regions.RegionOf(a.Cell) != hall) WalledIn.Add($"tick {Sim.Clock.Tick}: {a.Name} at {a.Cell}");
            }
            foreach (var c in Grounding.Floating(Sim)) Floating.Add($"tick {Sim.Clock.Tick}: {c}");
            if (CompletedAt < 0 && MonumentScript.PlannedCells.All(c => Sim.World.GetBlock(c) == BlockId.Masonry))
                CompletedAt = Sim.Clock.Tick;
        }
    }
}

/// <summary>M8-T6: the monument session (construction.md CON-09, CON-14, CON-P1; ADR-066). The dwarves quarry the
/// hill's stone, store it, and raise a hollow 7x7, 8-high stone tower with a door and an inner stair, and a walled
/// courtyard, by day 10 with everyone alive.</summary>
[Trait("Category", "Scenario")]
public class MonumentScenarioTests : IClassFixture<MonumentRun>
{
    private const long Day = 2400;
    private readonly MonumentRun _run;

    private readonly ITestOutputHelper _out;

    public MonumentScenarioTests(MonumentRun run, ITestOutputHelper output)
    {
        _run = run;
        _out = output;
    }

    [Fact]
    public void Seed1_Monument_CompleteByDay10_AllAlive()
    {
        var sim = _run.Sim;
        Assert.Empty(_run.Rejected);
        Assert.True(_run.CompletedAt > 0 && _run.CompletedAt <= 10 * Day, $"completed at {_run.CompletedAt}");
        Assert.Equal(0, sim.Plans.Count);
        Assert.Equal(5, sim.Agents.All.Count(a => a.IsAlive));
        Assert.False(sim.Agents.ColonyLost);

        // The monument: a hollow tower at least 7x7 and 8 high, with a two-high door and an inner stair, and a
        // courtyard wall; all of it Masonry from the quarry.
        Assert.True(MonumentScript.TowerSize >= 7 && MonumentScript.TowerHeight >= 8);
        Assert.Equal(221, MonumentScript.PlannedCells.Count);
        Assert.All(MonumentScript.PlannedCells, c => Assert.Equal(BlockId.Masonry, sim.World.GetBlock(c)));
        var a = MonumentScript.TowerA;
        var b = MonumentScript.TowerB;
        for (int y = a.Y; y < a.Y + MonumentScript.TowerHeight; y++)
            foreach (var c in new[] { new Int3(a.X, y, a.Z), new Int3(b.X, y, a.Z), new Int3(a.X, y, b.Z), new Int3(b.X, y, b.Z) })
                Assert.Equal(BlockId.Masonry, sim.World.GetBlock(c));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(MonumentScript.DoorA));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(MonumentScript.DoorB));
        Assert.Equal(BlockId.Air, sim.World.GetBlock(new Int3(a.X + 3, a.Y + 4, a.Z + 3)));   // hollow
        Assert.Equal(BlockId.Masonry, sim.World.GetBlock(new Int3(b.X - 1, a.Y + 6, a.Z + 3)));   // the stair's top step
        Assert.Equal(BlockId.Air, sim.World.GetBlock(new Int3(MonumentScript.DoorA.X, a.Y, MonumentScript.CourtyardFrontZ)));   // gate
        Assert.All(sim.Buildings.All, bl => Assert.Equal(Buildings.BuildingState.Complete, bl.State));
        Assert.True(sim.Water.Stats.Pumped > 0, "the pump never ran");
    }

    /// <summary>M9-T2 (CON-12, ADR-068): the script releases the whole plan at once, and Build trips still carry full
    /// batches: on average at least 4 blocks per trip.</summary>
    [Fact]
    public void Seed1_Monument_WholePlanRelease_FullTrips()
    {
        Assert.Contains(MonumentScript.Commands, c => c.Command is ReleasePlan);
        Assert.Single(MonumentScript.Commands, c => c.Command is ReleasePlan);
        double perTrip = (double)MonumentScript.PlannedCells.Count / Math.Max(_run.BuildTrips, 1);
        _out.WriteLine($"monument: {_run.BuildTrips} build trips, {perTrip:F2} blocks per trip, complete at tick {_run.CompletedAt}");
        Assert.True(perTrip >= 4.0, $"{perTrip:F2} blocks per trip ({_run.BuildTrips} trips)");
    }

    /// <summary>M10-T1 (G5 issue 5; CON-05, VIEW-22, ADR-071): after the whole-plan release, the inner stair steps
    /// that lean on a wall not yet built wait for support; none of them, and no other entry, is red.</summary>
    [Fact]
    public void Seed1_Monument_StairSteps_WaitForSupport_NotRed()
    {
        var stairs = new HashSet<Int3>();
        foreach (var (_, c) in MonumentScript.Commands)
            if (c is DesignateBuild { Shape: BuildShape.Stair } d)
                foreach (var cell in BuildShapes.Cells(d.Shape, d.A, d.B, d.Height)) stairs.Add(cell);
        Assert.NotEmpty(stairs);
        int waiting = 0;
        foreach (long tick in new long[] { 11000, 14000 })
        {
            var statuses = _run.StatusesAt[tick];
            Assert.NotEmpty(statuses);
            _out.WriteLine($"tick {tick}: " + string.Join(", ", statuses.GroupBy(s => s.Status).OrderBy(g => g.Key)
                .Select(g => $"{g.Key} {g.Count()}")));
            foreach (var (cell, _, status) in statuses)
            {
                Assert.False(PlanGhostMesher.IsStuck(status), $"tick {tick}: {cell} is {status} (red)");
                if (stairs.Contains(cell) && status == BuildStatus.WaitSupport) waiting++;
            }
        }
        Assert.True(waiting > 0, "no stair step was waiting for support; the case is not exercised");
    }

    [Fact]
    public void Seed1_Monument_NoDwarfWalledIn_NoFloatingBlock()
    {
        Assert.True(_run.WalledIn.Count == 0, string.Join("; ", _run.WalledIn.Take(10)));
        Assert.True(_run.Floating.Count == 0, string.Join("; ", _run.Floating.Take(10)));
        Assert.Empty(Grounding.Floating(_run.Sim));
    }

    /// <summary>SAV-03 mid-build: saved at tick 12,000 (lower courses up, the next ones released course by course by
    /// the script, many Build jobs open), the loaded game and the original, both run on with the script, hash equal
    /// every 100 ticks for 2,000 ticks.</summary>
    [Fact]
    public void Seed1_Monument_SaveLoadMidBuild_Identical()
    {
        const long saveAt = 12000, span = 2000;
        var sim = WorldFactory.Create(MonumentScript.Seed, TestContent.Db);
        MonumentScript.Run(sim, saveAt);
        Assert.True(sim.Plans.Count > 0 && sim.Jobs.All.Any(j => j.Kind == JobKind.Build), "not mid-build");
        byte[] save;
        using (var ms = new MemoryStream()) { SaveGame.Save(sim, ms); save = ms.ToArray(); }
        Simulation loaded;
        using (var ms = new MemoryStream(save)) loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        for (long t = 0; t < span; t += 100)
        {
            MonumentScript.Run(sim, 100);
            MonumentScript.Run(loaded, 100);
            Assert.True(sim.StateHash() == loaded.StateHash(), $"hash differs at tick {sim.Clock.Tick}");
        }
    }

    [Fact]
    public void MonumentScript_AvailableToHeadlessAndScreenshots()
    {
        Assert.Contains("monument", ScreenshotScripts.Names);
        Assert.True(ScreenshotScripts.IsTimed("monument"));
        Assert.Contains("monument", ScreenshotPresets.Names);

        var ticks = MonumentScript.Commands.Select(c => c.Tick).ToList();
        Assert.Equal(ticks.OrderBy(t => t), ticks);

        const int n = 3000;
        var harness = WorldFactory.Create(MonumentScript.Seed, TestContent.Db);
        ScreenshotScripts.Run("monument", harness, n, () => harness.Events.Drain());
        var direct = WorldFactory.Create(MonumentScript.Seed, TestContent.Db);
        MonumentScript.Run(direct, n);
        Assert.Equal(n, harness.Clock.Tick);
        Assert.Equal(direct.StateHash(), harness.StateHash());
        Assert.Equal(MonumentScript.Commands.Where(c => c.Tick < n), harness.Commands.Log);
    }
}
