using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;

namespace Aurvangar.Sim.Buildings;

/// <summary>M11-T5 trade wagon (docs/specs/crafting.md CRF-15..21, ADR-082, ADR-084), ARCH-01 step 7 after
/// <see cref="Workshops"/>: the schedule (a pure function of the tick), arrival and departure, the AcceptOffer handler,
/// and (Traders.Jobs.cs) the Trade jobs that pay deals and unload bought goods. Visit state:
/// <see cref="Simulation.Trader"/>.</summary>
public static partial class Traders
{
    // ---- schedule (CRF-16) ----

    /// <summary>The tick visit <paramref name="visit"/> (0, 1, ...) arrives.</summary>
    public static long ArrivesAt(TraderDef t, int visit) => t.FirstArrival + (long)visit * t.Interval;

    /// <summary>The tick visit <paramref name="visit"/> leaves.</summary>
    public static long LeavesAt(TraderDef t, int visit) => ArrivesAt(t, visit) + t.Stay;

    /// <summary>The first arrival tick at or after <paramref name="tick"/> (the HUD's "next trader").</summary>
    public static long NextArrival(TraderDef t, long tick)
    {
        if (tick <= t.FirstArrival) return t.FirstArrival;
        long v = (tick - t.FirstArrival + t.Interval - 1) / t.Interval;
        return t.FirstArrival + v * t.Interval;
    }

    /// <summary>The visit whose window [arrival, leave) holds the tick, or -1.</summary>
    public static int VisitAt(TraderDef t, long tick)
    {
        if (tick < t.FirstArrival) return -1;
        long v = (tick - t.FirstArrival) / t.Interval;
        return tick - ArrivesAt(t, (int)v) < t.Stay ? (int)v : -1;
    }

    private static bool IsArrival(TraderDef t, long tick) => tick >= t.FirstArrival && (tick - t.FirstArrival) % t.Interval == 0;

    // ---- tick (CRF-17, CRF-19..21) ----

    public static void Tick(Simulation sim)
    {
        var def = sim.Content.TraderBuilding;
        if (def is null) return;
        var t = def.Trader!;
        long now = sim.Clock.Tick;
        var visit = sim.Trader;
        if (visit.IsHere && VisitAt(t, now) < 0)   // the leave tick (or any tick outside a visit window)
        {
            sim.Actions.TraderDepart();   // CRF-21
            return;
        }
        if (!visit.IsHere && IsArrival(t, now))
        {
            var b = sim.Actions.TraderArrive(def);   // CRF-17
            if (b is null) { sim.Events.Emit(new TraderNoRoom()); return; }
            visit.Begin(b.Id, sim.Content.Offers);
            sim.Events.Emit(new TraderArrived(b.Id));
        }
        KeepJobs(sim);
    }

    // ---- command (CRF-18) ----

    public static void AcceptOffer(Simulation sim, string tag, int offer, int lots)
    {
        var visit = sim.Trader;
        var offers = sim.Content.Offers;
        string? reason = null;
        if (!visit.IsHere) reason = "NoTrader";
        else if (offer < 0 || offer >= offers.Count) reason = "BadOffer";
        else if (lots < 1) reason = "BadLots";
        else if (lots > visit.LotsLeft[offer]) reason = "OfferExhausted";
        else if ((long)lots * offers[offer].GiveCount > FreeStock(sim, offers[offer].Give)) reason = "NotEnough";
        if (reason is not null) { sim.Events.Emit(new CommandRejected(tag, reason)); return; }

        visit.LotsLeft[offer] -= lots;
        visit.Deals.Add(new TradeDeal { Offer = offer, Lots = lots });
    }

    /// <summary>CRF-18: the give stock a new deal may count on: unreserved stock (BLD-10) over complete storage buildings
    /// other than the trader, minus what earlier deals still have to fetch (<see cref="Unfetched"/>: owed units not paid
    /// and not yet taken by a claimed Trade job, whose stock is already reserved or carried).</summary>
    public static int FreeStock(Simulation sim, ItemId item)
    {
        int n = 0;
        foreach (var s in sim.Buildings.All)
            if (s.State == BuildingState.Complete && s.Def.Storage is not null && s.Id != sim.Trader.Building)
                n += sim.Jobs.StorageStock(s, item);
        var visit = sim.Trader;
        for (int i = 0; i < visit.Deals.Count; i++)
            if (sim.Content.Offers[visit.Deals[i].Offer].Give == item) n -= Unfetched(sim, i);
        return n;
    }

    /// <summary>CRF-19: give units of deal <paramref name="deal"/> not paid and not carried or reserved by a claimed Trade
    /// job of that deal.</summary>
    public static int Unfetched(Simulation sim, int deal)
    {
        var d = sim.Trader.Deals[deal];
        int n = d.Lots * sim.Content.Offers[d.Offer].GiveCount - d.Paid;
        foreach (var j in sim.Jobs.All)
            if (j.IsClaimed && IsPay(j) && DealOf(j) == deal) n -= PayCount(j);
        return Math.Max(n, 0);
    }

    /// <summary>Give units of the deal paid but not yet turned into a granted lot (refunded on departure, CRF-21).</summary>
    public static int PaidUngranted(Simulation sim, TradeDeal d) => d.Paid - d.Granted * sim.Content.Offers[d.Offer].GiveCount;

    private static int Manhattan(Int3 a, Int3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
}
