using System.Diagnostics;
using Aurvangar.Sim;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Screenshots;
using Aurvangar.ViewCore.Scripts;

// Headless runner. Usage:
//   dotnet run --project tools/Aurvangar.Headless -c Release -- --seed 1 --ticks 24000 [--report-every 2400] [--script none|digchop|build|farm|survival|blocks|monument|workshop|economy]
// Prints world stats, per-interval sim stats and the final StateHash. Exit code 0 on success.
// Later milestones extend the per-interval report (agents alive, jobs, storage, water).

var opts = Options.Parse(args);
var content = ContentDb.LoadEmbedded();

var sw = Stopwatch.StartNew();
Simulation sim = WorldFactory.Create(opts.Seed, content);
Console.WriteLine($"world: seed={opts.Seed} size={sim.World.SizeX}x{sim.World.SizeY}x{sim.World.SizeZ} created in {sw.ElapsedMilliseconds} ms");
PrintWorldStats(sim);

// ADR-036: "none"/"digchop"/"build"/"farm" are the screenshot harness scripts (ViewCore), enqueued before tick 1 exactly as
// the harness does. ADR-045: "survival" is SurvivalScript and (M8-T6) "monument" is MonumentScript, whose commands are
// enqueued at their ticks (ScreenshotScripts.IsTimed).
Baseline? baseline = null;
bool timed = opts.Script is not null && ScreenshotScripts.IsTimed(opts.Script);
var knownScripts = ScreenshotScripts.Names;
if (opts.Script is not null && !knownScripts.Contains(opts.Script))
{
    Console.Error.WriteLine($"unknown script '{opts.Script}' (known: {string.Join(",", knownScripts)})");
    return 2;
}
if (timed && opts.Script == "survival")
{
    Console.WriteLine($"script: survival ({SurvivalScript.Commands.Count} commands at ticks 0..{SurvivalScript.LastTick})");
}
else if (timed && opts.Script == "economy")
{
    Console.WriteLine($"script: economy (timed; workshops and Keep orders at ticks 0-1, trades at {string.Join(",", EconomyScript.TradeTicks)}, the refined hall ({EconomyScript.HallCells.Count} blocks) released at tick {EconomyScript.HallTick})");
}
else if (timed && opts.Script == "monument")
{
    Console.WriteLine($"script: monument ({MonumentScript.Commands.Count} commands at ticks 0..{MonumentScript.LastTick}; the whole plan is released at tick {MonumentScript.ReleaseTick})");
}
else if (timed)
{
    Console.WriteLine($"script: {opts.Script} (timed)");
}
else if (opts.Script is not null)
{
    var commands = ScreenshotScripts.For(opts.Script, sim);
    foreach (var c in commands) sim.Enqueue(c);
    Console.WriteLine($"script: {opts.Script} ({commands.Count} commands enqueued before tick 1)");
}

var tickTimes = new List<double>(opts.Ticks);
long colonyLostTick = -1;
// M6-T8: cumulative dig/chop counts, so timed scripts (survival) count designations made after tick 1 too.
WorkTracker? tracker = opts.Script is not null ? new WorkTracker() : null;
var runClock = Stopwatch.StartNew();
for (int t = 0; t < opts.Ticks; t++)
{
    if (timed) ScreenshotScripts.EnqueueDue(opts.Script!, sim);
    long start = Stopwatch.GetTimestamp();
    sim.Tick();
    tickTimes.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    foreach (var e in sim.Events.Drain())
        if (e is Aurvangar.Sim.Events.ColonyLost) colonyLostTick = sim.Clock.Tick;   // ECO-07
    if (t == 0 && opts.Script is not null) baseline = Baseline.Capture(sim);
    if (tracker is not null)
    {
        runClock.Stop();
        tracker.Observe(sim);
        runClock.Start();
    }
    if (opts.ReportEvery > 0 && (t + 1) % opts.ReportEvery == 0) PrintInterval(sim, tickTimes, baseline);
}

runClock.Stop();
PrintInterval(sim, tickTimes, baseline);
if (baseline is not null && tracker is not null) PrintWorkSummary(sim, baseline, tracker);
if (opts.Script == "economy")
    Console.WriteLine($"economy: hall.built={EconomyScript.HallCells.Count(p => sim.World.GetBlock(p.Cell) == p.Block)}/{EconomyScript.HallCells.Count} " +
        $"planks={Aurvangar.Sim.Buildings.Economy.Stock(sim, content.Item("planks"))} cutstone={Aurvangar.Sim.Buildings.Economy.Stock(sim, content.Item("cutstone"))}");
if (colonyLostTick >= 0) Console.WriteLine($"colony.lost tick={colonyLostTick} day={colonyLostTick / 2400}");
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
    var built = sim.Buildings.All.Where(b => b.State == Aurvangar.Sim.Buildings.BuildingState.Complete)
        .GroupBy(b => b.Def.Id).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key}:{g.Count()}");
    var farms = sim.Farms.All.ToList();
    Console.WriteLine(
        $"  colony: season={Aurvangar.Sim.Water.WeatherSystem.SeasonAt(sim.Clock.Tick)} built={string.Join(",", built)} " +
        $"pump.nowater={sim.Buildings.All.Count(b => b.Def.Id == "pump" && b.NoWater)} farm.tiles={farms.Count} " +
        $"crops.growing={farms.Count(f => f.State == Aurvangar.Sim.Farming.CropState.Growing)} " +
        $"crops.mature={farms.Count(f => f.State == Aurvangar.Sim.Farming.CropState.Mature)} " +
        $"stored={string.Join(",", w.StoredByItem.Select(x => $"{x.Item}:{x.Count}"))}");
}

static void PrintWorkSummary(Simulation sim, Baseline b, WorkTracker tr)
{
    var w = WorkStats.Of(sim);
    Console.WriteLine("summary:");
    Console.WriteLine($"  jobs.completed   {sim.Counters.JobsCompleted,9}");
    Console.WriteLine($"  jobs.failed      {sim.Counters.JobsFailed,9}");
    Console.WriteLine($"  cells.dug        {tr.CellsDug,9}  (of {tr.CellsMarked} ever marked; {w.DigMarks} still marked, {w.DigUnreachable} unreachable)");
    Console.WriteLine($"  trees.felled     {tr.TreesFelled,9}  (of {tr.TreesMarked} ever marked; {w.MarkedTrees} still marked, {w.ChopUnreachable} unreachable)");
    Console.WriteLine($"  items.hauled     {w.Stored - b.Stored,9}  (net change in storage since tick 1)");
    Console.WriteLine($"  crops.harvested  {tr.CropsHarvested,9}");
    Console.WriteLine($"  crops.withered   {tr.CropsWithered,9}");
    Console.WriteLine($"  pump.dry.ticks   {tr.PumpDryTicksWet + tr.PumpDryTicksDrought,9}  ({tr.PumpDryTicksWet} wet season, {tr.PumpDryTicksDrought} drought)");
    Console.WriteLine($"  items.on.ground  {w.PileItems,9}  (in {w.Piles} piles; {w.Carried} carried)");
    foreach (var (item, count) in w.StoredByItem)
        Console.WriteLine($"  stored.{item,-10} {count,9}");
    Console.WriteLine($"  path.searches    {sim.Counters.PathSearches,9}");
    Console.WriteLine($"  region.rebuilds  {sim.Counters.RegionRebuilds,9}");
    Console.WriteLine($"  agents.trapped   {w.Trapped,9}");
}

/// <summary>Cumulative dig and chop results, observed after every tick (M6-T8). A dig mark counts as dug when it
/// disappears and its cell is no longer solid (a cancelled mark leaves the block in place); a marked tree counts as
/// felled when it is gone from the plant list. Unlike <see cref="Baseline"/>, this also sees designations that a timed
/// script adds after tick 1.</summary>
internal sealed class WorkTracker
{
    private readonly HashSet<Int3> _everDig = new();
    private readonly HashSet<Int3> _digNow = new();
    private readonly HashSet<int> _everTrees = new();
    private readonly HashSet<int> _treesNow = new();

    public int CellsMarked => _everDig.Count;
    public int CellsDug { get; private set; }
    public int TreesMarked => _everTrees.Count;
    public int TreesFelled { get; private set; }

    private readonly Dictionary<Int3, Aurvangar.Sim.Farming.CropState> _crops = new();   // lookups only

    public int CropsHarvested { get; private set; }
    public int CropsWithered { get; private set; }
    /// <summary>Ticks on which at least one complete pump flagged NoWater, by season.</summary>
    public int PumpDryTicksWet { get; private set; }
    public int PumpDryTicksDrought { get; private set; }

    public void Observe(Simulation sim)
    {
        foreach (var t in sim.Farms.All)
        {
            if (_crops.TryGetValue(t.Cell, out var before) && t.State == Aurvangar.Sim.Farming.CropState.Empty)
            {
                if (before == Aurvangar.Sim.Farming.CropState.Mature) CropsHarvested++;
                else if (before == Aurvangar.Sim.Farming.CropState.Growing) CropsWithered++;   // ECO-13
            }
            _crops[t.Cell] = t.State;
        }
        if (sim.Buildings.All.Any(b => b.Def.Id == "pump" && b.State == Aurvangar.Sim.Buildings.BuildingState.Complete && b.NoWater))
        {
            if (Aurvangar.Sim.Water.WeatherSystem.SeasonAt(sim.Clock.Tick) == Aurvangar.Sim.Water.Season.Wet) PumpDryTicksWet++;
            else PumpDryTicksDrought++;
        }

        var dig = new HashSet<Int3>();
        foreach (var (cell, mark) in sim.Designations.All)
            if (mark is Aurvangar.Sim.Designations.DesignationMark.Dig or Aurvangar.Sim.Designations.DesignationMark.DigUnreachable)
                dig.Add(cell);
        foreach (var cell in _digNow)
            if (!dig.Contains(cell) && !sim.World.IsSolid(cell)) CellsDug++;
        _digNow.Clear(); _digNow.UnionWith(dig); _everDig.UnionWith(dig);

        var alive = new HashSet<int>();
        var marked = new HashSet<int>();
        foreach (var p in sim.Plants.All)
        {
            if (p.Kind != PlantKind.Tree) continue;
            alive.Add(p.Id.Value);
            if (p.MarkedForChop) marked.Add(p.Id.Value);
        }
        foreach (var id in _treesNow)
            if (!alive.Contains(id)) TreesFelled++;
        _treesNow.Clear(); _treesNow.UnionWith(marked); _everTrees.UnionWith(marked);
    }
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
