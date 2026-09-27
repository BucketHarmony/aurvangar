using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;

namespace Aurvangar.Sim.Buildings;

/// <summary>CRF-09: the job the active order of a workshop wants: <see cref="Cycles"/> cycles of
/// <see cref="Recipe"/>, with the input fetched from <see cref="Source"/>.</summary>
public readonly record struct CraftPlan(Recipe Recipe, Building Source, int Cycles);

/// <summary>M11-T4 workshops (docs/specs/crafting.md CRF-06..14, ADR-082), ARCH-01 step 7 after <see cref="Pumps"/>.
/// The SetWorkshopOrder handler, the active-order plan, and the upkeep of each complete workshop's one Craft job and
/// one Unload job. Stateless: orders and output are on the buildings, jobs on the board. Status: Workshops.Status.cs.</summary>
public static partial class Workshops
{
    // ---- command (CRF-07) ----

    public static void SetOrder(Simulation sim, string tag, BuildingId id, int recipe, OrderMode mode, int count)
    {
        var b = sim.Buildings.Get(id);
        string? reason = null;
        if (b is null) reason = "NoSuchBuilding";
        else if (b.Def.Workshop is null) reason = "NotAWorkshop";
        else if (recipe < 0 || recipe >= sim.Content.RecipesOf(b.Def).Count) reason = "BadRecipe";
        else if (!Enum.IsDefined(mode)) reason = "BadMode";
        else if (count < 0 || count > WorkshopOrder.MaxCount) reason = "BadCount";
        else if (count == 0 && b.OrderFor(recipe) is null) reason = "NothingToRemove";
        if (reason is not null) { sim.Events.Emit(new CommandRejected(tag, reason)); return; }

        int i = b!.Orders.FindIndex(o => o.Recipe == recipe);
        if (count == 0) { b.Orders.RemoveAt(i); return; }
        var order = new WorkshopOrder { Recipe = recipe, Mode = mode, Count = count };   // a replaced order starts at done 0
        if (i >= 0) { b.Orders[i] = order; return; }
        int at = 0;
        while (at < b.Orders.Count && b.Orders[at].Recipe < recipe) at++;
        b.Orders.Insert(at, order);   // CRF-06: recipe-index order
    }

    // ---- queries ----

    /// <summary>Items in the workshop's output buffer, all outputs together.</summary>
    public static int StoredTotal(Building b)
    {
        int n = 0;
        foreach (var v in b.Stored.Values) n += v;
        return n;
    }

    /// <summary>CRF-09: outputs the order still wants: count - done for Make, count - Stock(output) for Keep.</summary>
    public static int Wanted(Simulation sim, WorkshopOrder o, Recipe r) =>
        o.Mode == OrderMode.Make ? o.Count - o.Done : o.Count - Economy.Stock(sim, r.Output);

    /// <summary>CRF-09: the job for the active order of a complete workshop, or null (no order wants output and has
    /// input, or the cycle count is 0).</summary>
    public static CraftPlan? Plan(Simulation sim, Building b)
    {
        if (b.State != BuildingState.Complete || b.Def.Workshop is not { } w) return null;
        var recipes = sim.Content.RecipesOf(b.Def);
        foreach (var o in b.Orders)
        {
            var r = recipes[o.Recipe];
            int want = Wanted(sim, o, r);
            if (want <= 0) continue;
            var src = Source(sim, b, r);
            if (src is null) continue;
            int k = Math.Min((want + r.OutputCount - 1) / r.OutputCount, Agent.CarryCapacity / r.InputCount);
            k = Math.Min(k, (w.OutputBuffer - StoredTotal(b)) / r.OutputCount);
            k = Math.Min(k, sim.Jobs.StorageStock(src, r.Input) / r.InputCount);
            return k > 0 ? new CraftPlan(r, src, k) : null;
        }
        return null;
    }

    /// <summary>CRF-09: the nearest complete storage (Manhattan from the workshop entrance, then lower id) in the region of
    /// the workshop's stand cell that holds one cycle of input unreserved. Before regions are built, any region.</summary>
    public static Building? Source(Simulation sim, Building ws, Recipe r)
    {
        int region = sim.Regions.RegionOf(Construction.StandCell(sim, ws));
        Building? best = null;
        int bestDist = int.MaxValue;
        foreach (var s in sim.Buildings.All)   // ascending id: strict < keeps the lower id on a tie
        {
            if (s.State != BuildingState.Complete || s.Def.Storage is null) continue;
            if (sim.Jobs.StorageStock(s, r.Input) < r.InputCount) continue;
            if (region != Paths.Regions.None && sim.Regions.RegionOf(Construction.StandCell(sim, s)) != region) continue;
            int d = Manhattan(ws.EntranceCell, s.EntranceCell);
            if (d < bestDist) { best = s; bestDist = d; }
        }
        return best;
    }

    /// <summary>The workshop a Craft job (its last step, a Craft) or an Unload job (its pick-up) works for.</summary>
    public static BuildingId WorkshopOf(Job job) =>
        new(job.Kind == JobKind.Craft ? job.Steps[^1].Target : job.Steps[1].Target);

    // ---- tick (CRF-10, CRF-12, CRF-14) ----

    public static void Tick(Simulation sim)
    {
        var craft = new SortedDictionary<int, Job>();    // workshop id -> its Craft job (lowest job id)
        var unload = new SortedDictionary<int, Job>();   // workshop id -> its Unload job (lowest job id)
        List<Job>? cancel = null;
        foreach (var j in sim.Jobs.All)
        {
            if (j.Kind is not (JobKind.Craft or JobKind.Unload)) continue;
            var ws = sim.Buildings.Get(WorkshopOf(j));
            if (ws is not { State: BuildingState.Complete, Def.Workshop: not null })
            {
                // CRF-14: a workshop torn down or collapsing cancels its jobs (a crafter drops the input, which is
                // hauled back); an Unload already carrying the output finishes its delivery.
                if (j.Kind == JobKind.Craft || !j.IsClaimed || NotPickedUp(sim, j)) (cancel ??= new()).Add(j);
                continue;
            }
            var map = j.Kind == JobKind.Craft ? craft : unload;
            if (map.ContainsKey(ws.Id.Value)) { if (!j.IsClaimed) (cancel ??= new()).Add(j); continue; }
            map[ws.Id.Value] = j;
        }
        if (cancel is not null)
            foreach (var j in cancel) JobRunner.Cancel(sim, j);

        foreach (var b in sim.Buildings.All)
        {
            if (b.Def.Workshop is null || b.State != BuildingState.Complete) continue;
            var c = KeepCraft(sim, b, craft.TryGetValue(b.Id.Value, out var cj) ? cj : null);
            KeepUnload(sim, b, c, unload.TryGetValue(b.Id.Value, out var uj) ? uj : null);
        }
    }

    /// <summary>CRF-10: one Craft job for the active order. An unclaimed job that no longer matches the plan is withdrawn
    /// and a new one posted.</summary>
    private static Job? KeepCraft(Simulation sim, Building b, Job? job)
    {
        if (job is { IsClaimed: true }) return job;
        var plan = Plan(sim, b);
        JobStep[]? steps = null;
        Reservation[]? res = null;
        if (plan is { } p)
        {
            steps = CraftSteps(sim, b, p);
            res = new[] { Reservation.OutOfStorage(p.Source.Id, p.Recipe.Input, p.Cycles * p.Recipe.InputCount) };
        }
        if (job is not null)
        {
            if (steps is not null && job.Steps.SequenceEqual(steps) && job.Reservations.SequenceEqual(res!)) return job;
            JobRunner.Cancel(sim, job);
        }
        return steps is null ? null : sim.Jobs.Post(JobKind.Craft, b.EntranceCell, steps, res);
    }

    /// <summary>CRF-10: GoTo(source) → PickUpFromStorage(k × in) → GoTo(stand, Exact) → k × (WorkOn → Craft).</summary>
    private static JobStep[] CraftSteps(Simulation sim, Building b, CraftPlan p)
    {
        var r = p.Recipe;
        var steps = new JobStep[3 + 2 * p.Cycles];
        steps[0] = JobStep.GoToBuilding(p.Source.Id);
        steps[1] = JobStep.PickUpFromStorage(p.Source.Id, r.Input, p.Cycles * r.InputCount);
        steps[2] = JobStep.GoTo(Construction.StandCell(sim, b), GoalMode.Exact);
        for (int i = 0; i < p.Cycles; i++)
        {
            steps[3 + 2 * i] = JobStep.WorkOn(b.Id, r.WorkTicks);
            steps[4 + 2 * i] = JobStep.Craft(b.Id, r.Index);
        }
        return steps;
    }

    /// <summary>CRF-12: one Unload job while the unpromised output is at least haulAt, or is any at all and the
    /// workshop has no Craft job. It carries the lowest item id held (at most the carry capacity and the destination's
    /// room) to the nearest complete storage that accepts it and has room. An unclaimed one is re-planned every tick and
    /// withdrawn when it is no longer wanted or possible.</summary>
    private static void KeepUnload(Simulation sim, Building b, Job? craft, Job? job)
    {
        if (job is { IsClaimed: true }) return;
        int total = 0;
        ItemId item = default;
        foreach (var (id, _) in b.Stored)
        {
            int n = sim.Jobs.StorageStock(b, new ItemId(id));
            if (n > 0 && !item.IsValid) item = new ItemId(id);
            total += n;
        }
        bool want = total >= b.Def.Workshop!.HaulAt || (total > 0 && craft is null);
        var to = want ? Destination(sim, b, item) : null;
        if (to is null)
        {
            if (job is not null) JobRunner.Cancel(sim, job);
            return;
        }
        int count = Math.Min(Math.Min(sim.Jobs.StorageStock(b, item), Agent.CarryCapacity), sim.Jobs.StorageRoom(to, item));
        var steps = new[]
        {
            JobStep.GoToBuilding(b.Id), JobStep.PickUpFromStorage(b.Id, item, count), JobStep.GoToBuilding(to.Id), JobStep.DeliverTo(to.Id),
        };
        var res = new[] { Reservation.OutOfStorage(b.Id, item, count), Reservation.IntoStorage(to.Id, item, count) };
        if (job is null) { sim.Jobs.Post(JobKind.Unload, b.EntranceCell, steps, res); return; }
        if (job.Steps.SequenceEqual(steps) && job.Reservations.SequenceEqual(res)) return;
        job.Steps.Clear();
        job.Steps.AddRange(steps);
        job.Reservations.Clear();
        job.Reservations.AddRange(res);
    }

    /// <summary>JOB-10 storage choice from the workshop entrance: complete, accepts the item, has room; nearest, then
    /// lower id.</summary>
    public static Building? Destination(Simulation sim, Building ws, ItemId item)
    {
        Building? best = null;
        int bestDist = int.MaxValue;
        foreach (var s in sim.Buildings.All)
        {
            if (s.State != BuildingState.Complete || s.Def.Storage is null || !sim.Actions.Accepts(s, item)) continue;
            if (sim.Jobs.StorageRoom(s, item) <= 0) continue;
            int d = Manhattan(ws.EntranceCell, s.EntranceCell);
            if (d < bestDist) { best = s; bestDist = d; }
        }
        return best;
    }

    /// <summary>A claimed job whose agent has not done its pick-up step (step 1) yet.</summary>
    private static bool NotPickedUp(Simulation sim, Job job) =>
        sim.Agents.Get(job.ClaimedBy) is not { } a || a.CurrentJob != job.Id || a.StepIndex <= 1;

    private static int Manhattan(Int3 a, Int3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
}
