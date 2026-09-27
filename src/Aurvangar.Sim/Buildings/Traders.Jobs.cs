using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;

namespace Aurvangar.Sim.Buildings;

/// <summary>CRF-19/20 (M11-T5): upkeep of the Trade jobs. A pay job ends in a Pay step; an unload job carries the
/// trader's bought goods to storage. Unclaimed jobs are re-planned in place every tick, BLD-06 style, and surplus ones
/// withdrawn; claimed ones run to the end.</summary>
public static partial class Traders
{
    /// <summary>A Trade job that pays a deal (its last step is Pay).</summary>
    public static bool IsPay(Job j) => j.Kind == JobKind.Trade && j.Steps.Count > 0 && j.Steps[^1].Kind == StepKind.Pay;

    /// <summary>The deal a pay job pays (its Pay step's count).</summary>
    public static int DealOf(Job j) => j.Steps[^1].Count;

    /// <summary>The give units a pay job fetches (its pick-up count).</summary>
    public static int PayCount(Job j) => j.Steps[1].Count;

    /// <summary>The planned stock taken from (pay) or room taken at (unload) a storage by the unclaimed jobs planned so
    /// far this tick, by (building id, item). Lookups only.</summary>
    private sealed class Planned
    {
        private readonly Dictionary<(int, int), int> _n = new();
        public int this[Building b, ItemId item] => _n.TryGetValue((b.Id.Value, item.Value), out var n) ? n : 0;
        public void Add(Building b, ItemId item, int n) => _n[(b.Id.Value, item.Value)] = this[b, item] + n;
    }

    /// <summary>Every Trade job either belongs to the trader here (a pay job for one of its deals, an unload job from its
    /// storage) or is cancelled; then each deal's pay jobs and the unload jobs are kept in line.</summary>
    private static void KeepJobs(Simulation sim)
    {
        var visit = sim.Trader;
        if (!visit.IsHere && !sim.Jobs.All.Any(j => j.Kind == JobKind.Trade)) return;   // nothing to keep
        var trader = visit.IsHere ? sim.Buildings.Get(visit.Building) : null;
        var pays = new List<Job>[visit.Deals.Count];
        for (int i = 0; i < pays.Length; i++) pays[i] = new List<Job>();
        var unloads = new List<Job>();
        List<Job>? cancel = null;
        foreach (var j in sim.Jobs.All)
        {
            if (j.Kind != JobKind.Trade) continue;
            bool mine = trader is not null && (IsPay(j)
                ? j.Steps[^1].Target == trader.Id.Value && DealOf(j) >= 0 && DealOf(j) < pays.Length
                : j.Steps[1].Target == trader.Id.Value);
            if (!mine) { (cancel ??= new()).Add(j); continue; }
            if (IsPay(j)) pays[DealOf(j)].Add(j);
            else unloads.Add(j);
        }
        if (cancel is not null)
            foreach (var j in cancel) JobRunner.Cancel(sim, j);
        if (trader is null) return;

        var taken = new Planned();
        for (int i = 0; i < pays.Length; i++) KeepPays(sim, trader, i, pays[i], taken);
        KeepUnloads(sim, trader, unloads);
    }

    /// <summary>CRF-19: the unclaimed pay jobs of a deal fetch what is still owed and not taken by claimed ones, in loads of
    /// at most <see cref="Agent.CarryCapacity"/>, each from <see cref="PaySource"/>.</summary>
    private static void KeepPays(Simulation sim, Building trader, int deal, List<Job> jobs, Planned taken)
    {
        var d = sim.Trader.Deals[deal];
        var item = sim.Content.Offers[d.Offer].Give;
        int left = d.Lots * sim.Content.Offers[d.Offer].GiveCount - d.Paid;
        var open = new List<Job>();
        foreach (var j in jobs)
        {
            if (j.IsClaimed) left -= PayCount(j);
            else open.Add(j);
        }
        int k = 0;
        while (left > 0)
        {
            var src = PaySource(sim, trader, item, Math.Min(left, Agent.CarryCapacity), taken, out int count);
            if (src is null) break;
            taken.Add(src, item, count);
            left -= count;
            var steps = new[]
            {
                JobStep.GoToBuilding(src.Id), JobStep.PickUpFromStorage(src.Id, item, count),
                JobStep.GoTo(Construction.StandCell(sim, trader), GoalMode.Exact), JobStep.Pay(trader.Id, deal),
            };
            var res = new[] { Reservation.OutOfStorage(src.Id, item, count) };
            if (k < open.Count) Replan(open[k], steps, res);
            else sim.Jobs.Post(JobKind.Trade, trader.EntranceCell, steps, res);
            k++;
        }
        for (; k < open.Count; k++) JobRunner.Cancel(sim, open[k]);
    }

    /// <summary>CRF-19 source of a load of <paramref name="n"/>: complete storage buildings other than the trader, with
    /// their unreserved stock less what this tick's plans already take. The nearest (Manhattan from its entrance to the
    /// trader's, then lower id) with <paramref name="n"/>; else the one with the most, carrying only that; else null.</summary>
    private static Building? PaySource(Simulation sim, Building trader, ItemId item, int n, Planned taken, out int count)
    {
        Building? near = null, most = null;
        int dNear = int.MaxValue, mostStock = 0;
        foreach (var s in sim.Buildings.All)   // ascending id: strict comparisons keep the lower id on ties
        {
            if (s.State != BuildingState.Complete || s.Def.Storage is null || s.Id == trader.Id) continue;
            int stock = sim.Jobs.StorageStock(s, item) - taken[s, item];
            if (stock <= 0) continue;
            int dist = Manhattan(s.EntranceCell, trader.EntranceCell);
            if (stock >= n && dist < dNear) { near = s; dNear = dist; }
            if (stock > mostStock) { most = s; mostStock = stock; }
        }
        count = near is not null ? n : mostStock;
        return near ?? most;
    }

    /// <summary>CRF-20: the unclaimed unload jobs carry the trader's unreserved goods, lowest item id first, in loads of at
    /// most <see cref="Agent.CarryCapacity"/> and the destination's room (less this tick's plans), each to the JOB-10
    /// storage (complete, accepts the item, has room; nearest to the trader's entrance, then lower id).</summary>
    private static void KeepUnloads(Simulation sim, Building trader, List<Job> jobs)
    {
        var open = new List<Job>();
        foreach (var j in jobs) if (!j.IsClaimed) open.Add(j);
        var room = new Planned();
        int k = 0;
        foreach (var (id, _) in trader.Stored)
        {
            var item = new ItemId(id);
            int left = sim.Jobs.StorageStock(trader, item);
            while (left > 0)
            {
                var to = Destination(sim, trader, item, room, out int free);
                if (to is null) break;
                int n = Math.Min(Math.Min(left, Agent.CarryCapacity), free);
                room.Add(to, item, n);
                left -= n;
                var steps = new[]
                {
                    JobStep.GoToBuilding(trader.Id), JobStep.PickUpFromStorage(trader.Id, item, n),
                    JobStep.GoToBuilding(to.Id), JobStep.DeliverTo(to.Id),
                };
                var res = new[] { Reservation.OutOfStorage(trader.Id, item, n), Reservation.IntoStorage(to.Id, item, n) };
                if (k < open.Count) Replan(open[k], steps, res);
                else sim.Jobs.Post(JobKind.Trade, trader.EntranceCell, steps, res);
                k++;
            }
        }
        for (; k < open.Count; k++) JobRunner.Cancel(sim, open[k]);
    }

    private static Building? Destination(Simulation sim, Building trader, ItemId item, Planned room, out int free)
    {
        Building? best = null;
        int bestDist = int.MaxValue;
        free = 0;
        foreach (var s in sim.Buildings.All)
        {
            if (s.State != BuildingState.Complete || s.Def.Storage is null || !sim.Actions.Accepts(s, item)) continue;
            int r = sim.Jobs.StorageRoom(s, item) - room[s, item];
            if (r <= 0) continue;
            int dist = Manhattan(trader.EntranceCell, s.EntranceCell);
            if (dist < bestDist) { best = s; bestDist = dist; free = r; }
        }
        return best;
    }

    /// <summary>Re-plans an unclaimed job in place when its steps or reservations differ from the plan.</summary>
    private static void Replan(Job job, JobStep[] steps, Reservation[] res)
    {
        if (job.Steps.SequenceEqual(steps) && job.Reservations.SequenceEqual(res)) return;
        job.Steps.Clear();
        job.Steps.AddRange(steps);
        job.Reservations.Clear();
        job.Reservations.AddRange(res);
    }
}
