using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Items;

namespace Aurvangar.Sim.Jobs;

/// <summary>JOB-10 (ADR-030), ARCH-01 step 9: keeps one Haul job per loose item pile while some storage accepts the
/// item and has room. A pile haul is <c>GoTo(pile) → PickUp → GoTo(storage) → DeliverTo</c> reserving the items and
/// the storage room; it carries at most <see cref="Agent.CarryCapacity"/>, so a big pile takes several jobs in turn.
/// Unclaimed pile hauls are re-planned every tick (storage choice and count follow the current piles and room) and
/// withdrawn when their pile is gone or nothing has room; claimed ones are left alone. Stateless: the jobs are on the board.</summary>
public static class HaulSystem
{
    public static void Tick(Simulation sim)
    {
        var world = sim.World;
        var covered = new HashSet<int>();   // pile cells with a haul job; lookups only, never enumerated
        List<Job>? withdraw = null;
        foreach (var job in sim.Jobs.All)
        {
            if (!IsPileHaul(job)) continue;
            if (!job.IsClaimed && !Replan(sim, job)) { (withdraw ??= new()).Add(job); continue; }
            covered.Add(world.Index(job.Target));
        }
        if (withdraw is not null)
            foreach (var j in withdraw) JobRunner.Cancel(sim, j);

        if (sim.Piles.Count == 0) return;
        List<(Int3 Cell, ItemId Item, int Count, Building To)>? post = null;
        foreach (var (cell, stack) in sim.Piles.All)
        {
            if (covered.Contains(world.Index(cell))) continue;
            if (Plan(sim, cell, stack, out var to, out int n)) (post ??= new()).Add((cell, stack.Item, n, to));
        }
        if (post is null) return;
        foreach (var (cell, item, n, to) in post)
            sim.Jobs.Post(JobKind.Haul, cell, Steps(cell, item, n, to.Id), Reservations(cell, item, n, to.Id));
    }

    /// <summary>A Haul job posted by this system: its target is the pile cell and its steps have the fixed shape.</summary>
    public static bool IsPileHaul(Job job) =>
        job.Kind == JobKind.Haul && job.Steps.Count == 4 && job.Steps[1].Kind == StepKind.PickUp &&
        job.Steps[3].Kind == StepKind.DeliverTo && job.Steps[1].Cell == job.Target;

    /// <summary>JOB-10 storage choice: among complete storage buildings that accept the item and have unreserved room,
    /// the one nearest the pile (Manhattan to its entrance cell), ties by lower building id. The count is what is
    /// left of the pile after other jobs' reservations, capped by the carry capacity and that storage's room.</summary>
    public static bool Plan(Simulation sim, Int3 pile, ItemStack stack, out Building to, out int count)
    {
        to = null!;
        count = 0;
        int avail = stack.Count - sim.Jobs.ReservedFromPile(pile);
        if (stack.IsEmpty || avail <= 0) return false;
        int bestDist = int.MaxValue, bestRoom = 0;
        foreach (var b in sim.Buildings.All)   // ascending id: strict < keeps the lower id on a tie
        {
            if (b.State != BuildingState.Complete || b.Def.Storage is null || !sim.Actions.Accepts(b, stack.Item)) continue;
            int room = sim.Jobs.StorageRoom(b, stack.Item);
            if (room <= 0) continue;
            int dist = Manhattan(pile, b.EntranceCell);
            if (dist >= bestDist) continue;
            to = b;
            bestDist = dist;
            bestRoom = room;
        }
        if (bestDist == int.MaxValue) return false;
        count = Math.Min(Math.Min(avail, Agent.CarryCapacity), bestRoom);
        return true;
    }

    /// <summary>Brings an unclaimed pile haul up to date; false when it should be withdrawn.</summary>
    private static bool Replan(Simulation sim, Job job)
    {
        var stack = sim.Piles.At(job.Target);
        if (!Plan(sim, job.Target, stack, out var to, out int n)) return false;
        var pick = job.Steps[1];
        if (pick.Item == stack.Item && pick.Count == n && job.Steps[3].Target == to.Id.Value) return true;
        job.Steps.Clear();
        job.Steps.AddRange(Steps(job.Target, stack.Item, n, to.Id));
        job.Reservations.Clear();
        job.Reservations.AddRange(Reservations(job.Target, stack.Item, n, to.Id));
        return true;
    }

    private static JobStep[] Steps(Int3 pile, ItemId item, int n, BuildingId to) =>
        new[] { JobStep.GoTo(pile), JobStep.PickUp(pile, item, n), JobStep.GoToBuilding(to), JobStep.DeliverTo(to) };

    private static Reservation[] Reservations(Int3 pile, ItemId item, int n, BuildingId to) =>
        new[] { Reservation.FromPile(pile, item, n), Reservation.IntoStorage(to, item, n) };

    private static int Manhattan(Int3 a, Int3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
}
