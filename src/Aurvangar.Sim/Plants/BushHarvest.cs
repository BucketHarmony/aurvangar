using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;

namespace Aurvangar.Sim.Plants;

/// <summary>ECO-10 bush harvest jobs (M6-T3, ADR-048). While the colony's food in storage is below
/// <see cref="FoodTarget"/>, every ripe bush has one Harvest job (35): <c>GoTo(reach) → Work(20) → HarvestBush</c>,
/// holding a cell reservation on the bush. The bush's berries then go straight to storage in the same job (JOB-11,
/// <see cref="Farming.FarmSystem.ChainDelivery"/>). Unclaimed jobs that are no longer wanted are withdrawn.
/// Bush jobs share <see cref="JobKind.Harvest"/> with farm crops; they are told apart by their HarvestBush step.</summary>
public static class BushHarvest
{
    /// <summary>ECO-10: harvests post only while total food in storage is below this many units.</summary>
    public const int FoodTarget = 60;

    /// <summary>JOB-05 work ticks (jobs-agents.md Harvest row).</summary>
    public const int WorkTicks = 20;

    private const int HarvestStep = 2;

    /// <summary>ECO-10 (ADR-048): units of food items (items with a food value: berries, potatoes) stored in complete
    /// storage buildings. Reservations and items being carried or lying in piles do not count.</summary>
    public static int FoodInStorage(Simulation sim)
    {
        int total = 0;
        foreach (var b in sim.Buildings.All)
        {
            if (b.State != BuildingState.Complete || b.Def.Storage is null) continue;
            foreach (var (id, n) in b.Stored)
                if (sim.Content.ItemDef(new ItemId(id)).Food > 0) total += n;
        }
        return total;
    }

    /// <summary>A bush harvest job (as posted: its third step is HarvestBush; delivery steps may follow).</summary>
    public static bool Is(Job job) =>
        job.Kind == JobKind.Harvest && job.Steps.Count > HarvestStep && job.Steps[HarvestStep].Kind == StepKind.HarvestBush;

    /// <summary>The bush a bush harvest job targets.</summary>
    public static PlantId BushOf(Job job) => new(job.Steps[HarvestStep].Target);

    /// <summary>False for a bush harvest job that should not be taken now: food in storage is at least
    /// <see cref="FoodTarget"/>, the bush is gone or not ripe, or the job already carries delivery steps (it was
    /// released mid-delivery; its berries were dropped for hauling). Such an unclaimed job is withdrawn at the next
    /// plant step. True for every other job.</summary>
    public static bool StillWanted(Simulation sim, Job job) =>
        !Is(job) || (Ripe(sim, BushOf(job)) && job.Steps.Count == HarvestStep + 1 && FoodInStorage(sim) < FoodTarget);

    private static bool Ripe(Simulation sim, PlantId id) => sim.Plants.Get(id) is { Kind: PlantKind.Bush, Berries: > 0 };

    /// <summary>Withdraws unwanted unclaimed bush jobs, then (while food is below target) posts one job per ripe bush
    /// that has none. Jobs are visited in ascending id order, bushes in ascending id order.</summary>
    internal static void Sync(Simulation sim)
    {
        bool want = FoodInStorage(sim) < FoodTarget;
        HashSet<int>? has = null;   // bush ids with a job; lookups only, never enumerated
        List<Job>? withdraw = null;
        foreach (var j in sim.Jobs.All)
        {
            if (!Is(j)) continue;
            if (!j.IsClaimed && !(want && Ripe(sim, BushOf(j)) && j.Steps.Count == HarvestStep + 1))
                (withdraw ??= new()).Add(j);
            else (has ??= new()).Add(BushOf(j).Value);
        }
        if (withdraw is not null)
            foreach (var j in withdraw) JobRunner.Cancel(sim, j);
        if (!want) return;

        foreach (var p in sim.Plants.All)
        {
            if (p.Kind != PlantKind.Bush || p.Berries <= 0 || (has is not null && has.Contains(p.Id.Value))) continue;
            sim.Jobs.Post(JobKind.Harvest, p.Base,
                new[] { JobStep.GoTo(p.Base), JobStep.Work(p.Base, WorkTicks), JobStep.HarvestBush(p.Id) },
                new[] { Reservation.OnCell(p.Base) });
        }
    }
}
