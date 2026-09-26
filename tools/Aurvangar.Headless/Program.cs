using System.Diagnostics;
using Aurvangar.Sim;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.World;

// Headless runner. Usage:
//   dotnet run --project tools/Aurvangar.Headless -c Release -- --seed 1 --ticks 24000 [--report-every 2400] [--script survival]
// Prints world stats, per-interval sim stats and the final StateHash. Exit code 0 on success.
// M1-T5 fills in world stats; later milestones extend the per-interval report (agents alive, jobs, storage, water).

var opts = Options.Parse(args);
var content = ContentDb.LoadEmbedded();

var sw = Stopwatch.StartNew();
Simulation sim = WorldFactory.Create(opts.Seed, content);
Console.WriteLine($"world: seed={opts.Seed} size={sim.World.SizeX}x{sim.World.SizeY}x{sim.World.SizeZ} created in {sw.ElapsedMilliseconds} ms");
PrintWorldStats(sim);

if (opts.Script is not null)
{
    // M5-T7 / M6-T6: load the named command script (e.g. "survival") and enqueue commands at their ticks.
    Console.Error.WriteLine($"script '{opts.Script}' requested but scripts are not implemented yet");
    return 2;
}

var tickTimes = new List<double>(opts.Ticks);
for (int t = 0; t < opts.Ticks; t++)
{
    long start = Stopwatch.GetTimestamp();
    sim.Tick();
    tickTimes.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    sim.Events.Drain();
    if (opts.ReportEvery > 0 && (t + 1) % opts.ReportEvery == 0) PrintInterval(sim, tickTimes);
}

PrintInterval(sim, tickTimes);
Console.WriteLine($"hash: {sim.StateHash():x16}");
return 0;

static void PrintWorldStats(Simulation sim)
{
    var counts = new long[256];
    foreach (var b in sim.World.Blocks) counts[b]++;
    foreach (BlockId id in Enum.GetValues<BlockId>())
        Console.WriteLine($"  blocks.{id,-14} {counts[(int)id],9}");
    Console.WriteLine($"  plants           {sim.Plants.Count,9}");
    Console.WriteLine($"  buildings        {sim.Buildings.All.Count(),9}");
    Console.WriteLine($"  agents           {sim.Agents.Count,9}");
    Console.WriteLine($"  water.volume     {sim.Water.TotalVolume(),9}");
}

static void PrintInterval(Simulation sim, List<double> tickTimes)
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
