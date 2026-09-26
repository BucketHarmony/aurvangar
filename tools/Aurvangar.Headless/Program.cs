using System.Diagnostics;
using Aurvangar.Sim;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Screenshots;

// Headless runner. Usage:
//   dotnet run --project tools/Aurvangar.Headless -c Release -- --seed 1 --ticks 24000 [--report-every 2400] [--script digchop]
// Prints world stats, per-interval sim stats and the final StateHash. Exit code 0 on success.
// Later milestones extend the per-interval report (agents alive, jobs, storage, water).

var opts = Options.Parse(args);
var content = ContentDb.LoadEmbedded();

var sw = Stopwatch.StartNew();
Simulation sim = WorldFactory.Create(opts.Seed, content);
Console.WriteLine($"world: seed={opts.Seed} size={sim.World.SizeX}x{sim.World.SizeY}x{sim.World.SizeZ} created in {sw.ElapsedMilliseconds} ms");
PrintWorldStats(sim);

// ADR-036: "none"/"digchop" are the screenshot harness scripts (ViewCore), enqueued before tick 1 exactly as the
// harness does. M5-T7 / M6-T6 add "survival" (commands at their ticks).
Baseline? baseline = null;
if (opts.Script is not null)
{
    if (!ScreenshotScripts.Names.Contains(opts.Script))
    {
        Console.Error.WriteLine($"unknown script '{opts.Script}' (known: {string.Join(",", ScreenshotScripts.Names)})");
        return 2;
    }
    var commands = ScreenshotScripts.For(opts.Script, sim);
    foreach (var c in commands) sim.Enqueue(c);
    Console.WriteLine($"script: {opts.Script} ({commands.Count} commands enqueued before tick 1)");
}

var tickTimes = new List<double>(opts.Ticks);
var runClock = Stopwatch.StartNew();
for (int t = 0; t < opts.Ticks; t++)
{
    long start = Stopwatch.GetTimestamp();
    sim.Tick();
    tickTimes.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    sim.Events.Drain();
    if (t == 0 && opts.Script is not null) baseline = Baseline.Capture(sim);
    if (opts.ReportEvery > 0 && (t + 1) % opts.ReportEvery == 0) PrintInterval(sim, tickTimes, baseline);
}

runClock.Stop();
PrintInterval(sim, tickTimes, baseline);
if (baseline is not null) PrintWorkSummary(sim, baseline);
double seconds = runClock.Elapsed.TotalSeconds;
double tps = seconds > 0 ? opts.Ticks / seconds : 0;
Console.WriteLine($"run: ticks={opts.Ticks} elapsed={seconds:F3} s ticks_per_sec={tps:F0}");
Console.WriteLine($"hash: {sim.StateHash():x16}");
return 0;

static void PrintWorldStats(Simulation sim)
{
    var counts = new long[256];
    foreach (var b in sim.World.Blocks) counts[b]++;
    foreach (BlockId id in Enum.GetValues<BlockId>())
        Console.WriteLine($"  blocks.{id,-14} {counts[(int)id],9}");
    int trees = sim.Plants.All.Count(p => p.Kind == PlantKind.Tree);
    int bushes = sim.Plants.All.Count(p => p.Kind == PlantKind.Bush);
    Console.WriteLine($"  plants.trees     {trees,9}");
    Console.WriteLine($"  plants.bushes    {bushes,9}");
    Console.WriteLine($"  buildings        {sim.Buildings.All.Count(),9}");
    Console.WriteLine($"  agents           {sim.Agents.Count,9}");
    Console.WriteLine($"  water.volume     {sim.Water.TotalVolume(),9}");
}

static void PrintInterval(Simulation sim, List<double> tickTimes, Baseline? baseline)
{
    if (tickTimes.Count == 0) return;
    var sorted = tickTimes.ToArray();
    Array.Sort(sorted);
    double median = sorted[sorted.Length / 2];
    double p95 = sorted[(int)(sorted.Length * 0.95)];
    Console.WriteLine(
        $"tick={sim.Clock.Tick} day={sim.Clock.Day} tick_ms median={median:F3} p95={p95:F3} " +
        $"water.active={sim.Water.ActiveCount} water.volume={sim.Water.TotalVolume()} " +
        $"agents.alive={sim.Agents.All.Count(a => a.IsAlive)} jobs.done={sim.Counters.JobsCompleted} jobs.failed={sim.Counters.JobsFailed}");
    if (baseline is null) return;
    var w = WorkStats.Of(sim);
    Console.WriteLine(
        $"  work: dig.marks={w.DigMarks} dig.unreachable={w.DigUnreachable} trees.marked={w.MarkedTrees} " +
        $"trees.unreachable={w.ChopUnreachable} piles={w.Piles} pile.items={w.PileItems} carried={w.Carried} " +
        $"stored={w.Stored - baseline.Stored} path.searches={sim.Counters.PathSearches} agents.trapped={w.Trapped} " +
        $"agents.idle={w.Idle} regions.of.agents={w.AgentRegions}");
}

static void PrintWorkSummary(Simulation sim, Baseline b)
{
    var w = WorkStats.Of(sim);
    Console.WriteLine("summary:");
    Console.WriteLine($"  jobs.completed   {sim.Counters.JobsCompleted,9}");
    Console.WriteLine($"  jobs.failed      {sim.Counters.JobsFailed,9}");
    Console.WriteLine($"  cells.dug        {b.DigMarks - w.DigMarks,9}  (of {b.DigMarks} marked; {w.DigUnreachable} left unreachable)");
    Console.WriteLine($"  trees.felled     {b.Trees - w.Trees,9}  (of {b.MarkedTrees} marked; {w.ChopUnreachable} left unreachable)");
    Console.WriteLine($"  items.hauled     {w.Stored - b.Stored,9}  (into hub storage)");
    Console.WriteLine($"  items.on.ground  {w.PileItems,9}  (in {w.Piles} piles; {w.Carried} carried)");
    foreach (var (item, count) in w.StoredByItem)
        Console.WriteLine($"  stored.{item,-10} {count,9}");
    Console.WriteLine($"  path.searches    {sim.Counters.PathSearches,9}");
    Console.WriteLine($"  region.rebuilds  {sim.Counters.RegionRebuilds,9}");
    Console.WriteLine($"  agents.trapped   {w.Trapped,9}");
}

/// <summary>Work counts after tick 1, when the script's commands have been applied. No dig or chop can finish in the
/// tick its designation is applied, so these are the full marked counts.</summary>
internal sealed record Baseline(int DigMarks, int MarkedTrees, int Trees, int Stored)
{
    public static Baseline Capture(Simulation sim)
    {
        var w = WorkStats.Of(sim);
        return new Baseline(w.DigMarks + w.DigUnreachable, w.MarkedTrees, w.Trees, w.Stored);
    }
}

internal sealed record WorkStats(int DigMarks, int DigUnreachable, int MarkedTrees, int ChopUnreachable, int Trees,
    int Piles, int PileItems, int Carried, int Stored, IReadOnlyList<(string Item, int Count)> StoredByItem,
    int Trapped, int Idle, int AgentRegions)
{
    public static WorkStats Of(Simulation sim)
    {
        int dig = 0, digUnreach = 0;
        foreach (var (_, mark) in sim.Designations.All)
        {
            if (mark == Aurvangar.Sim.Designations.DesignationMark.Dig) dig++;
            else if (mark == Aurvangar.Sim.Designations.DesignationMark.DigUnreachable) digUnreach++;
        }
        var trees = sim.Plants.All.Where(p => p.Kind == PlantKind.Tree).ToList();
        int pileItems = 0;
        foreach (var (_, stack) in sim.Piles.All) pileItems += stack.Count;
        var byItem = new SortedDictionary<int, int>();
        foreach (var b in sim.Buildings.All)
            foreach (var (item, count) in b.Stored)
                byItem[item] = byItem.GetValueOrDefault(item) + count;
        var alive = sim.Agents.All.Where(a => a.IsAlive).OrderBy(a => a.Id.Value).ToList();
        return new WorkStats(dig, digUnreach,
            trees.Count(t => t.MarkedForChop), trees.Count(t => t.MarkedForChop && t.ChopUnreachable), trees.Count,
            sim.Piles.Count, pileItems, alive.Sum(a => a.Carried.Count), byItem.Values.Sum(),
            byItem.Select(kv => (sim.Content.Items[kv.Key].Id, kv.Value)).ToList(),
            alive.Count(a => a.State == Aurvangar.Sim.Agents.AgentState.Trapped),
            alive.Count(a => a.State == Aurvangar.Sim.Agents.AgentState.Idle),
            alive.Select(a => sim.Regions.RegionOf(a.Cell)).Distinct().Count());
    }
}

internal sealed record Options(ulong Seed, int Ticks, int ReportEvery, string? Script)
{
    public static Options Parse(string[] args)
    {
        ulong seed = 1; int ticks = 1000; int every = 0; string? script = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed": seed = ulong.Parse(args[++i]); break;
                case "--ticks": ticks = int.Parse(args[++i]); break;
                case "--report-every": every = int.Parse(args[++i]); break;
                case "--script": script = args[++i]; break;
                default: throw new ArgumentException($"unknown argument '{args[i]}'");
            }
        }
        return new Options(seed, ticks, every, script);
    }
}
