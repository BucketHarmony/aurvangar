using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Blocks;

/// <summary>CON-04 upkeep and CON-12 posting (M8-T2, ADR-062), ARCH-01 step 8 after the dig and chop postings. Holds no
/// state: entries are <see cref="Simulation.Plans"/>, Build jobs are on the board, and the cells a job holds are its
/// Place steps not yet run.</summary>
public static partial class BlockBuildSystem
{
    /// <summary>CON-12: new Build jobs per tick.</summary>
    public const int MaxPostsPerTick = 8;

    /// <summary>CON-12: unclaimed Build jobs on the board at a time.</summary>
    public const int MaxUnclaimed = 32;

    /// <summary>CON-12: Chebyshev distance from a batch member within which entries join the batch.</summary>
    public const int BatchRadius = 4;

    public static void Tick(Simulation sim)
    {
        var plans = sim.Plans;
        List<Job>? jobs = null;
        foreach (var j in sim.Jobs.All)
            if (j.Kind == JobKind.Build) (jobs ??= new()).Add(j);
        if (plans.Count == 0 && jobs is null) return;

        DropFilled(sim);
        if (jobs is not null) Recheck(sim, jobs);
        if (plans.Count > 0) Post(sim);
    }

    /// <summary>CON-04: an entry whose cell is solid is gone, whatever filled it.</summary>
    private static void DropFilled(Simulation sim)
    {
        List<Int3>? filled = null;
        foreach (var (c, _) in sim.Plans.All)
            if (sim.World.IsSolid(c)) (filled ??= new()).Add(c);
        if (filled is not null)
            foreach (var c in filled) sim.Plans.Remove(c);
    }

    /// <summary>The cells Build jobs hold (CON-12): the Place steps a claimed job has not reached yet, or all of an
    /// unclaimed job's. Cell index -> job; lookups only.</summary>
    public static Dictionary<int, Job> HeldCells(Simulation sim)
    {
        var held = new Dictionary<int, Job>();
        foreach (var j in sim.Jobs.All)
        {
            if (j.Kind != JobKind.Build) continue;
            for (int k = HeldFrom(sim, j); k < j.Steps.Count; k++)
                if (j.Steps[k].Kind == StepKind.Place && sim.World.InBounds(j.Steps[k].Cell))
                    held[sim.World.Index(j.Steps[k].Cell)] = j;
        }
        return held;
    }

    /// <summary>The first step index whose Place counts as held.</summary>
    internal static int HeldFrom(Simulation sim, Job j) =>
        j.IsClaimed && sim.Agents.Get(j.ClaimedBy) is { } a && a.CurrentJob == j.Id ? a.StepIndex : 0;

    /// <summary>The block a Build job places (all its cells have the same one).</summary>
    public static BlockId BlockOf(Job job)
    {
        foreach (var s in job.Steps)
            if (s.Kind == StepKind.Place) return (BlockId)s.Target;
        return BlockId.Air;
    }

    /// <summary>CON-12 re-check of every unclaimed Build job, ascending id. Cells already placed (their entry gone)
    /// leave the job; the job is cancelled when its seed is gone or given up, or a remaining cell is not
    /// <c>Ready</c> (ignoring the job's own hold). The rest is re-planned against the current stock, so a job that
    /// failed after its pickup (JOB-08) keeps its failure count.</summary>
    private static void Recheck(Simulation sim, List<Job> jobs)
    {
        var promised = new SortedDictionary<(int B, int Item), int>();
        foreach (var j in jobs)
        {
            if (j.IsClaimed) continue;
            var scan = new BuildScan(sim, held: null, self: j);
            var block = BlockOf(j);
            var cells = new List<Int3>();
            bool bad = !sim.World.InBounds(j.Target) || sim.GiveUps.IsGivenUp(GiveUpSource.Build, sim.World.Index(j.Target));
            List<Int3>? seedStands = null;
            foreach (var s in j.Steps)
            {
                if (bad) break;
                if (s.Kind != StepKind.Place) continue;
                if (sim.Plans.Get(s.Cell) is not { } e || e.State != PlanState.Released || e.Block != block)
                {
                    if (s.Cell == j.Target) bad = true;
                    continue;   // placed, cancelled or repainted: not this job's any more
                }
                if (BlockPlans.StatusOf(scan, s.Cell, e, default, out var stands, ignoreAgents: true) != BuildStatus.Ready) bad = true;
                else
                {
                    if (s.Cell == j.Target) seedStands = stands;
                    cells.Add(s.Cell);
                }
            }
            if (bad || cells.Count == 0 || seedStands is null
                || !Source(sim, scan, promised, block, j.Target, seedStands, cells.Count, out var src, out int take))
            {
                JobRunner.Cancel(sim, j);
                continue;
            }
            cells.RemoveRange(take, cells.Count - take);   // the seed is first: it always stays
            var (item, cost) = sim.Content.CostOf(block);
            var steps = Steps(sim, src, item, cost, block, cells);
            var res = new[] { Reservation.OutOfStorage(src, item, take * cost) };
            if (!j.Steps.SequenceEqual(steps) || !j.Reservations.SequenceEqual(res))
            {
                j.Steps.Clear(); j.Steps.AddRange(steps);
                j.Reservations.Clear(); j.Reservations.AddRange(res);
            }
            Promise(promised, src, item, take * cost);
        }
    }

    /// <summary>CON-12 posting: seeds in ascending (y, index) order, batches, limits and the ADR-041 source rule.</summary>
    private static void Post(Simulation sim)
    {
        var held = HeldCells(sim);
        var scan = new BuildScan(sim, held);
        var promised = new SortedDictionary<(int B, int Item), int>();
        int unclaimed = 0;
        foreach (var j in sim.Jobs.All)
        {
            if (j.Kind != JobKind.Build || j.IsClaimed) continue;
            unclaimed++;
            foreach (var r in j.Reservations)
                if (r.Kind == ReservationKind.StorageOut) Promise(promised, r.Building, r.Item, r.Count);
        }
        int posts = 0;
        var world = sim.World;
        foreach (var (cell, e) in sim.Plans.All)
        {
            if (posts >= MaxPostsPerTick || unclaimed >= MaxUnclaimed) return;
            if (e.State != PlanState.Released || held.ContainsKey(world.Index(cell))) continue;
            if (BlockPlans.StatusOf(scan, cell, e, default, out var stands) != BuildStatus.Ready) continue;
            var (item, cost) = sim.Content.CostOf(e.Block);
            var batch = new List<Int3> { cell };
            int max = Math.Max(ItemsCarried / cost, 1);
            Join(sim, scan, cell, e.Block, stands!, max, batch);
            SortByCourse(batch);
            if (!Source(sim, scan, promised, e.Block, cell, stands!, batch.Count, out var src, out int take)) continue;
            batch.RemoveRange(take, batch.Count - take);
            var job = sim.Jobs.Post(JobKind.Build, cell, Steps(sim, src, item, cost, e.Block, batch),
                new[] { Reservation.OutOfStorage(src, item, take * cost) });
            foreach (var c in batch) scan.AddHeld(c, job);
            Promise(promised, src, item, take * cost);
            unclaimed++;
            posts++;
        }
    }

    private const int ItemsCarried = Agents.Agent.CarryCapacity;

    /// <summary>CON-12 batch (M9-T2 chain, ADR-068): up to <paramref name="max"/> cells in all. Members are taken in
    /// order (the seed first); around each, the box within <see cref="BatchRadius"/> is scanned in ascending
    /// (dy, dz, dx) order for entries of the same block that are released, unheld, <c>Ready</c> and
    /// have a stand cell in a region shared with a seed stand cell; each joins at the end. So a batch follows a wall
    /// course instead of stopping at a box around the seed.</summary>
    private static void Join(Simulation sim, BuildScan scan, Int3 seed, BlockId block, List<Int3> seedStands, int max,
        List<Int3> batch)
    {
        if (batch.Count >= max) return;
        var regions = new SortedSet<int>();
        foreach (var c in seedStands)
        {
            int r = sim.Regions.RegionOf(c);
            if (r != Paths.Regions.None) regions.Add(r);
        }
        var world = sim.World;
        var tried = new HashSet<int>();   // lookups only
        foreach (var c in batch) tried.Add(world.Index(c));
        for (int m = 0; m < batch.Count; m++)
        {
            var from = batch[m];
            for (int dy = -BatchRadius; dy <= BatchRadius; dy++)
                for (int dz = -BatchRadius; dz <= BatchRadius; dz++)
                    for (int dx = -BatchRadius; dx <= BatchRadius; dx++)
                    {
                        var c = from + new Int3(dx, dy, dz);
                        if (!world.InBounds(c) || sim.Plans.Get(c) is not { } e || !tried.Add(world.Index(c))) continue;
                        if (e.Block != block || e.State != PlanState.Released || scan.Held!.ContainsKey(world.Index(c))) continue;
                        if (BlockPlans.StatusOf(scan, c, e, default, out var stands) != BuildStatus.Ready) continue;
                        bool shared = false;
                        foreach (var s in stands!)
                            if (regions.Contains(sim.Regions.RegionOf(s))) { shared = true; break; }
                        if (!shared) continue;
                        batch.Add(c);
                        if (batch.Count >= max) return;
                    }
        }
    }

    /// <summary>Stable sort by y of the members after the seed (M9-T2): lower cells are built first, and trimming the
    /// batch keeps them. The seed stays first (the re-check and the source rule key on it).</summary>
    private static void SortByCourse(List<Int3> batch)
    {
        var rest = batch.Skip(1).Select((c, k) => (c, k)).OrderBy(x => x.c.Y).ThenBy(x => x.k).Select(x => x.c).ToList();
        batch.RemoveRange(1, batch.Count - 1);
        batch.AddRange(rest);
    }

    /// <summary>ADR-041 source for <paramref name="cells"/> blocks: among complete storages that accept the cost item
    /// and serve a seed stand cell's region, the nearest (Manhattan from its entrance to the seed, ties by lower id)
    /// with the whole amount available; else the one with the most, for the whole cells it covers; else none.
    /// Available = unpromised stock minus what other unclaimed Build jobs will take.</summary>
    private static bool Source(Simulation sim, BuildScan scan, SortedDictionary<(int B, int Item), int> promised,
        BlockId block, Int3 seed, List<Int3> seedStands, int cells, out BuildingId src, out int take)
    {
        var (item, cost) = sim.Content.CostOf(block);
        src = default;
        take = 0;
        Building? nearFull = null, most = null;
        int dFull = int.MaxValue, availMost = 0;
        foreach (var s in sim.Buildings.All)   // ascending id: strict comparisons keep the lower id on ties
        {
            if (!scan.Serves(s, item, seedStands)) continue;
            int avail = sim.Jobs.StorageStock(s, item)
                - (promised.TryGetValue((s.Id.Value, item.Value), out var p) ? p : 0);
            int d = Math.Abs(s.EntranceCell.X - seed.X) + Math.Abs(s.EntranceCell.Y - seed.Y) + Math.Abs(s.EntranceCell.Z - seed.Z);
            if (avail >= cells * cost && d < dFull) { nearFull = s; dFull = d; }
            if (avail > availMost) { most = s; availMost = avail; }
        }
        if (nearFull is not null) { src = nearFull.Id; take = cells; return true; }
        if (most is not null && availMost >= cost) { src = most.Id; take = Math.Min(availMost / cost, cells); return true; }
        return false;
    }

    private static void Promise(SortedDictionary<(int B, int Item), int> promised, BuildingId b, ItemId item, int n) =>
        promised[(b.Value, item.Value)] = (promised.TryGetValue((b.Value, item.Value), out var p) ? p : 0) + n;

    /// <summary>CON-12 steps: fetch, then per cell GoTo(Build) -> Work(buildTicks) -> Place.</summary>
    private static List<JobStep> Steps(Simulation sim, BuildingId src, ItemId item, int cost, BlockId block, List<Int3> cells)
    {
        int ticks = sim.Content.Block(block).BuildTicks;
        var steps = new List<JobStep>(2 + 3 * cells.Count)
        {
            JobStep.GoToBuilding(src), JobStep.PickUpFromStorage(src, item, cells.Count * cost),
        };
        foreach (var c in cells)
        {
            steps.Add(JobStep.GoTo(c, GoalMode.Build));
            steps.Add(JobStep.Work(c, ticks));
            steps.Add(JobStep.Place(c, block));
        }
        return steps;
    }

    /// <summary>DSG-06 extension (CON-07): removes every entry in the box, either state, and cancels the Build jobs that
    /// hold any of them (a claimed one is released: the dwarf drops what it carries). Placed blocks stay.</summary>
    public static void CancelIn(Simulation sim, Int3 min, Int3 max)
    {
        if (sim.Plans.Count == 0) return;
        var held = HeldCells(sim);
        var removed = new List<Int3>();
        foreach (var (c, _) in sim.Plans.All)
            if (c.X >= min.X && c.X <= max.X && c.Y >= min.Y && c.Y <= max.Y && c.Z >= min.Z && c.Z <= max.Z) removed.Add(c);
        var cancel = new SortedDictionary<int, Job>();
        foreach (var c in removed)
        {
            sim.Plans.Remove(c);
            if (held.TryGetValue(sim.World.Index(c), out var j)) cancel[j.Id.Value] = j;
        }
        foreach (var j in cancel.Values) JobRunner.Cancel(sim, j);
    }

    /// <summary>Cancels the Build job holding <paramref name="c"/>, if any (a repaint that changes the block).</summary>
    internal static void CancelHolder(Simulation sim, Int3 c)
    {
        if (HeldCells(sim).TryGetValue(sim.World.Index(c), out var j)) JobRunner.Cancel(sim, j);
    }
}
