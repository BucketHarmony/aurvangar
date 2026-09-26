using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;

namespace Aurvangar.Sim.Agents;

/// <summary>ARCH-01 step 6 (ECO-02..06, JOB-07, ADR-043). For each living agent in ascending id: hunger −1 and thirst −2
/// per tick; health −1 per tick while either need is 0, else +1 on every tick divisible by 10 (not while trapped in deep water); death at health 0
/// (thirst wins ties). Below 4000 the agent posts its own Drink (first) or Eat job and claims it at once, preempting a
/// non-need job. A try that finds no reachable storage with stock, or a need job that fails, waits 100 ticks before
/// that need is tried again (ECO-04). Stateless apart from the agents' own fields.</summary>
public static class NeedsSystem
{
    public const int HungerDecay = 1, ThirstDecay = 2;   // ECO-03
    public const int Threshold = 4_000;                   // ECO-04
    public const int Sated = 9_000;                       // ECO-05
    public const int RetryInterval = 100;                 // ECO-04
    public const int RegenInterval = 10;                  // ECO-06

    public static void Tick(Simulation sim)
    {
        long now = sim.Clock.Tick;
        foreach (var a in sim.Agents.All)   // Kill does not change the agent list
        {
            if (!a.IsAlive) continue;
            a.Hunger = Math.Max(0, a.Hunger - HungerDecay);
            a.Thirst = Math.Max(0, a.Thirst - ThirstDecay);
            if (a.Hunger == 0 || a.Thirst == 0)
            {
                if (--a.Health <= 0)
                {
                    sim.Agents.Kill(sim, a, a.Thirst == 0 ? DeathCause.Dehydrated : DeathCause.Starved);
                    continue;
                }
            }
            else if (now % RegenInterval == 0 && a.Health < Agent.HealthMax && a.State != AgentState.Trapped)
                a.Health++;   // a drowning agent does not heal (ADR-043)
            PostNeedJobs(sim, a, now);
        }
    }

    /// <summary>The need a job kind restores is satisfied (ECO-05: at least 9000).</summary>
    public static bool IsSated(Agent a, JobKind kind) => (kind == JobKind.Drink ? a.Thirst : a.Hunger) >= Sated;

    /// <summary>HUD "No water" (ECO-04): some living agent is thirsty and no complete storage has unpromised water.</summary>
    public static bool NoWater(Simulation sim) => Short(sim, JobKind.Drink);

    /// <summary>HUD "No food" (ECO-04): some living agent is hungry and no complete storage has unpromised food.</summary>
    public static bool NoFood(Simulation sim) => Short(sim, JobKind.Eat);

    private static bool Short(Simulation sim, JobKind kind)
    {
        bool needy = false;
        foreach (var a in sim.Agents.All)
            if (a.IsAlive && (kind == JobKind.Drink ? a.Thirst : a.Hunger) < Threshold) { needy = true; break; }
        if (!needy) return false;
        foreach (var b in sim.Buildings.All)
            if (IsStorage(b) && PickItem(sim, b, kind, out _, out _)) return false;
        return true;
    }

    private static void PostNeedJobs(Simulation sim, Agent a, long now)
    {
        if (a.State == AgentState.Trapped) return;   // WAT-14 comes first; nothing to reach from deep water
        if (sim.Jobs.Get(a.CurrentJob) is { IsNeed: true } cur && cur.ClaimedBy == a.Id) return;
        if (a.Thirst < Threshold && now >= a.NextDrinkTick)
        {
            if (TryPost(sim, a, JobKind.Drink)) return;
            a.NextDrinkTick = now + RetryInterval;
        }
        if (a.Hunger < Threshold && now >= a.NextEatTick && !TryPost(sim, a, JobKind.Eat))
            a.NextEatTick = now + RetryInterval;
    }

    /// <summary>JOB-08 for need jobs (ADR-031: they are removed, not retried): the agent tries that need again after
    /// <see cref="RetryInterval"/> ticks, so an unreachable or emptied storage is not re-pathed every tick.</summary>
    public static void OnFailed(Simulation sim, Agent a, JobKind kind)
    {
        long next = sim.Clock.Tick + RetryInterval;
        if (kind == JobKind.Drink) a.NextDrinkTick = next;
        else if (kind == JobKind.Eat) a.NextEatTick = next;
    }

    /// <summary>JOB-05 Drink/Eat: <c>GoTo(storage) → Consume</c> at the nearest complete storage (Manhattan from the
    /// agent to its entrance, ties by lower building id) that is in the agent's region and has unpromised stock of a
    /// matching item (lowest item id first). Reserves the units that would bring the need to 9000, capped by the stock.</summary>
    private static bool TryPost(Simulation sim, Agent a, JobKind kind)
    {
        int region = sim.Regions.RegionOf(a.Cell);
        if (region == Paths.Regions.None) return false;
        Building? best = null;
        ItemId bestItem = default;
        int bestDist = int.MaxValue, bestStock = 0;
        foreach (var b in sim.Buildings.All)   // ascending id: strict < keeps the lower id on a tie
        {
            if (!IsStorage(b)) continue;
            int dist = Manhattan(a.Cell, b.EntranceCell);
            if (dist >= bestDist || !PickItem(sim, b, kind, out var item, out int stock)) continue;
            if (!InRegion(sim, b, region)) continue;
            best = b; bestItem = item; bestDist = dist; bestStock = stock;
        }
        if (best is null) return false;

        var def = sim.Content.ItemDef(bestItem);
        int value = kind == JobKind.Drink ? def.Drink : def.Food;
        int need = kind == JobKind.Drink ? a.Thirst : a.Hunger;
        int units = Math.Clamp((Sated - need + value - 1) / value, 1, bestStock);
        var steps = new[] { JobStep.GoToBuilding(best.Id), JobStep.Consume(best.Id, bestItem) };
        var res = new[] { Reservation.OutOfStorage(best.Id, bestItem, units) };
        return JobRunner.AssignNeed(sim, a, kind, best.EntranceCell, steps, res) is not null;
    }

    private static bool IsStorage(Building b) => b.State == BuildingState.Complete && b.Def.Storage is not null;

    /// <summary>The lowest-id item that restores the need and has unpromised stock in the building.</summary>
    private static bool PickItem(Simulation sim, Building b, JobKind kind, out ItemId item, out int stock)
    {
        foreach (var (id, _) in b.Stored)   // sorted by item id
        {
            var def = sim.Content.ItemDef(new ItemId(id));
            if ((kind == JobKind.Drink ? def.Drink : def.Food) <= 0) continue;
            int n = sim.Jobs.StorageStock(b, new ItemId(id));
            if (n <= 0) continue;
            item = new ItemId(id);
            stock = n;
            return true;
        }
        item = default;
        stock = 0;
        return false;
    }

    /// <summary>PTH-13: some cell from which the building is in reach lies in the agent's region.</summary>
    private static bool InRegion(Simulation sim, Building b, int region)
    {
        foreach (var g in JobGoals.For(sim, JobStep.GoToBuilding(b.Id)))
            if (sim.Regions.RegionOf(g) == region) return true;
        return false;
    }

    private static int Manhattan(Int3 a, Int3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
}
