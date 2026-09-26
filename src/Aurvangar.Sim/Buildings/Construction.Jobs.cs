using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;

namespace Aurvangar.Sim.Buildings;

/// <summary>ARCH-01 step 7 job upkeep for construction sites (BLD-06, BLD-08, BLD-09; ADR-041).</summary>
public static partial class Construction
{
    /// <summary>Keeps the construction jobs of every building in line with its state: stray jobs are cancelled, each
    /// site has Deliver jobs covering what is still to deliver (unclaimed ones re-planned every tick), a fully
    /// delivered site has one Construct job and a building being deconstructed has one Deconstruct job.</summary>
    public static void Tick(Simulation sim)
    {
        var bySite = new SortedDictionary<int, List<Job>>();   // building id -> its jobs (ascending job id)
        List<Job>? stray = null;
        foreach (var j in sim.Jobs.All)
        {
            if (!IsSiteJob(j)) continue;
            var b = sim.Buildings.Get(SiteOf(j));
            bool fits = b is not null && j.Kind switch
            {
                JobKind.Deliver => b.State is BuildingState.Blueprint or BuildingState.UnderConstruction,
                JobKind.Construct => b.State == BuildingState.UnderConstruction,
                _ => b.State == BuildingState.Deconstructing,
            };
            if (!fits) { (stray ??= new()).Add(j); continue; }
            if (!bySite.TryGetValue(b!.Id.Value, out var list)) bySite[b.Id.Value] = list = new();
            list.Add(j);
        }
        if (stray is not null)
            foreach (var j in stray) JobRunner.Cancel(sim, j);

        List<Building>? start = null;
        foreach (var b in sim.Buildings.All)
            if (b.State == BuildingState.Blueprint && FullyDelivered(sim, b)) (start ??= new()).Add(b);   // zero cost
        if (start is not null)
            foreach (var b in start) Start(sim, b);

        foreach (var b in sim.Buildings.All.ToList())
        {
            var jobs = bySite.TryGetValue(b.Id.Value, out var l) ? l : new List<Job>();
            switch (b.State)
            {
                case BuildingState.Blueprint:
                case BuildingState.UnderConstruction:
                    bool waits = WaitsForBelow(sim, b);
                    foreach (var (item, _) in Cost(sim, b.Def)) KeepDelivers(sim, b, item, jobs, waits);
                    if (b.State == BuildingState.UnderConstruction && FullyDelivered(sim, b))
                        KeepOne(sim, b, jobs, JobKind.Construct, b.Def.BuildTicks);
                    break;
                case BuildingState.Deconstructing:
                    KeepOne(sim, b, jobs, JobKind.Deconstruct, DeconstructTicks(b.Def));
                    break;
            }
        }
    }

    public static bool FullyDelivered(Simulation sim, Building b)
    {
        foreach (var (item, _) in Cost(sim, b.Def))
            if (Remaining(sim, b, item) > 0) return false;
        return true;
    }

    /// <summary>A Construct or Deconstruct job's Work step is over: the site is complete, or the building is gone.</summary>
    public static bool WorkDone(Simulation sim, Job job)
    {
        var b = sim.Buildings.Get(SiteOf(job));
        return b is null || (job.Kind == JobKind.Construct && b.State == BuildingState.Complete);
    }

    /// <summary>M4-T14 for deconstruction: removing any footprint cell would cut the worker's cell off from the Great
    /// Hall (each cell tested on its own, like a dig).</summary>
    public static bool WouldStrand(Simulation sim, Building b, Int3 stand)
    {
        if (!b.Def.SetsBlocks) return false;
        foreach (var c in b.FootprintCells())
            if (DigStrand.Strands(sim, c, stand)) return true;
        return false;
    }

    /// <summary>Items a Deliver job brings: the count of its PickUpFromStorage step.</summary>
    public static int DeliverCount(Job job) => job.Steps[1].Count;

    /// <summary>BLD-06: the unclaimed Deliver jobs for one item carry what claimed ones do not, in loads of at most
    /// <see cref="Agent.CarryCapacity"/> (10, 10, ..., rest). None while the site waits for the building below it.</summary>
    private static void KeepDelivers(Simulation sim, Building b, ItemId item, List<Job> jobs, bool waits)
    {
        int claimed = 0;
        var open = new List<Job>();
        foreach (var j in jobs)
        {
            if (j.Kind != JobKind.Deliver || j.Steps[1].Item != item) continue;
            if (j.IsClaimed) claimed += DeliverCount(j);
            else open.Add(j);
        }
        int left = waits ? 0 : Math.Max(Remaining(sim, b, item) - claimed, 0);
        int k = 0;
        while (left > 0)
        {
            int n = Math.Min(left, Agent.CarryCapacity);
            left -= n;
            if (!Source(sim, b, item, n, out var src, out int count)) break;
            if (k < open.Count) Replan(open[k], b, item, src, count);
            else sim.Jobs.Post(JobKind.Deliver, b.EntranceCell, DeliverSteps(b.Id, item, src, count), DeliverRes(item, src, count));
            k++;
        }
        for (; k < open.Count; k++) JobRunner.Cancel(sim, open[k]);
    }

    /// <summary>Source of a Deliver job (ADR-041): complete storage buildings that accept the item. The nearest (Manhattan
    /// from entrance to the site's entrance, ties by lower id) with n unpromised in stock; else the one with the most
    /// stock, carrying only that much; else the nearest, carrying n (the job waits for stock, JOB-06 CanReserve).
    /// False when there is no such storage.</summary>
    private static bool Source(Simulation sim, Building site, ItemId item, int n, out BuildingId src, out int count)
    {
        src = default;
        count = n;
        Building? nearest = null, nearFull = null, most = null;
        int dNear = int.MaxValue, dFull = int.MaxValue, stockMost = 0;
        foreach (var s in sim.Buildings.All)   // ascending id: strict comparisons keep the lower id on ties
        {
            if (s.State != BuildingState.Complete || s.Def.Storage is null || !sim.Actions.Accepts(s, item)) continue;
            int d = Manhattan(s.EntranceCell, site.EntranceCell);
            int stock = sim.Jobs.StorageStock(s, item);
            if (d < dNear) { nearest = s; dNear = d; }
            if (stock >= n && d < dFull) { nearFull = s; dFull = d; }
            if (stock > stockMost) { most = s; stockMost = stock; }
        }
        if (nearFull is not null) src = nearFull.Id;
        else if (most is not null) { src = most.Id; count = stockMost; }
        else if (nearest is not null) src = nearest.Id;
        else return false;
        return true;
    }

    private static void Replan(Job job, Building site, ItemId item, BuildingId src, int count)
    {
        // The reservation is compared too: a job released after its pickup has used up its StorageOut (ADR-048).
        if (job.Steps[0].Target == src.Value && job.Steps[1].Count == count
            && job.Reservations.SequenceEqual(DeliverRes(item, src, count))) return;
        job.Steps.Clear();
        job.Steps.AddRange(DeliverSteps(site.Id, item, src, count));
        job.Reservations.Clear();
        job.Reservations.AddRange(DeliverRes(item, src, count));
    }

    private static JobStep[] DeliverSteps(BuildingId site, ItemId item, BuildingId src, int n) => new[]
    {
        JobStep.GoToBuilding(src), JobStep.PickUpFromStorage(src, item, n), JobStep.GoToBuilding(site), JobStep.DeliverTo(site),
    };

    private static Reservation[] DeliverRes(ItemId item, BuildingId src, int n) => new[] { Reservation.OutOfStorage(src, item, n) };

    /// <summary>Exactly one job of the kind for the building: <c>GoTo(building) → Work(building)</c>.</summary>
    private static void KeepOne(Simulation sim, Building b, List<Job> jobs, JobKind kind, int ticks)
    {
        foreach (var j in jobs)
            if (j.Kind == kind) return;
        sim.Jobs.Post(kind, b.EntranceCell, new[] { JobStep.GoToBuilding(b.Id), JobStep.WorkOn(b.Id, ticks) });
    }

    /// <summary>BLD-08: the site's Construct job, posted by the delivery that completes its materials (so it exists
    /// in the same tick); a no-op when the site already has one.</summary>
    public static void PostConstruct(Simulation sim, Building b)
    {
        var jobs = new List<Job>();
        foreach (var j in sim.Jobs.All)
            if (j.Kind == JobKind.Construct && SiteOf(j) == b.Id) jobs.Add(j);
        KeepOne(sim, b, jobs, JobKind.Construct, b.Def.BuildTicks);
    }

    private static int Manhattan(Int3 a, Int3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
}
