using Aurvangar.Sim;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;

namespace Aurvangar.ViewCore.Hud;

/// <summary>An accept button of an offer row: the lots it sends, and why the sim would refuse it (the CRF-18 reason
/// code and a sentence for the tooltip), or null reasons when it is enabled.</summary>
public sealed record AcceptButton(int Lots, string? Reason, string? Tooltip)
{
    public bool Enabled => Reason is null;

    public AcceptOffer? Command(int offer) => Enabled ? new AcceptOffer(offer, Lots) : null;
}

/// <summary>One offer row (VIEW-29): "10 log -> 10 stone", lots left, the colony's free stock of the give item
/// (CRF-18) and the two accept buttons.</summary>
public sealed record OfferRow(int Offer, string Text, int LotsLeft, string GiveName, int Have, AcceptButton AcceptOne,
    AcceptButton AcceptAll)
{
    public string LotsText => $"{LotsLeft} lot{(LotsLeft == 1 ? "" : "s")} left";
    public string HaveText => $"have {Have} {GiveName}";
}

/// <summary>One open deal: "2 x 10 log -> 10 stone: paid 12/20, granted 1/2".</summary>
public sealed record DealRow(int Deal, string Text);

/// <summary>The trade panel (VIEW-29) while a trader is here.</summary>
public sealed record TradePanel(string Title, IReadOnlyList<OfferRow> Offers, IReadOnlyList<DealRow> Deals);

/// <summary>VIEW-29 / VIEW-30 (M11-T6, ADR-085): the trade panel, the toolbar button's tooltip and the arrival toasts.
/// Times are game hours of <see cref="TicksPerHour"/> ticks, rounded up. "Accept all" takes as many lots as the colony
/// can pay for now (at most the lots left); when it cannot pay for one it asks for every lot left, so its tooltip gives
/// the sim's refusal.</summary>
public static class TradePanelModel
{
    public const int TicksPerHour = 100;
    public const int HoursPerDay = SimClock.TicksPerDay / TicksPerHour;

    public const string Name = "Trade wagon";
    public const string NoRoomToast = "A trade wagon came but found no room beside the Great Hall";
    public const string LeftToast = "The trade wagon has left";

    /// <summary>The panel, or null when no trader is here (or the data has none).</summary>
    public static TradePanel? Build(Simulation sim)
    {
        var visit = sim.Trader;
        if (!visit.IsHere || sim.Content.TraderBuilding?.Trader is not { } t) return null;
        var content = sim.Content;
        var offers = new List<OfferRow>(content.Offers.Count);
        foreach (var o in content.Offers)
        {
            int left = visit.LotsLeft[o.Index];
            int have = Traders.FreeStock(sim, o.Give);
            int afford = Math.Min(left, Math.Max(have, 0) / o.GiveCount);
            offers.Add(new OfferRow(o.Index, OfferText(content, o), left, ItemName(content, o.Give), have,
                Button(sim, o.Index, 1), Button(sim, o.Index, afford > 0 ? afford : Math.Max(left, 1))));
        }
        var deals = new List<DealRow>(visit.Deals.Count);
        for (int i = 0; i < visit.Deals.Count; i++)
        {
            var d = visit.Deals[i];
            var o = content.Offers[d.Offer];
            deals.Add(new DealRow(i, $"{d.Lots} x {OfferText(content, o)}: paid {d.Paid}/{d.Lots * o.GiveCount}, granted {d.Granted}/{d.Lots}"));
        }
        return new TradePanel($"{Name}: leaves in {Duration(LeavesIn(sim, t))}", offers, deals);
    }

    /// <summary>The toolbar button's tooltip: "Trade wagon: leaves in 3h" while one is here, else "Trade wagon in 1 day
    /// 4h". Null when the data has no trader.</summary>
    public static string? ButtonTooltip(Simulation sim)
    {
        if (sim.Content.TraderBuilding?.Trader is not { } t) return null;
        long now = sim.Clock.Tick;
        if (sim.Trader.IsHere) return $"{Name}: leaves in {Duration(LeavesIn(sim, t))}";
        long next = Traders.NextArrival(t, now);
        if (next == now) next = Traders.NextArrival(t, now + 1);   // this tick's arrival was skipped (no room)
        return $"{Name} in {Duration(next - now)}";
    }

    /// <summary>The toast on <c>TraderArrived</c>.</summary>
    public static string ArrivedToast(Simulation sim) =>
        sim.Content.TraderBuilding?.Trader is { } t
            ? $"A trade wagon has arrived: open Trade to see its offers (leaves in {Duration(LeavesIn(sim, t))})"
            : "A trade wagon has arrived";

    /// <summary>CRF-18 checks in the sim's order, for a button that would send <c>AcceptOffer(offer, lots)</c>: the
    /// reason code and a tooltip, or (null, null) when the sim would accept it.</summary>
    public static (string? Reason, string? Tooltip) Refusal(Simulation sim, int offer, int lots)
    {
        var visit = sim.Trader;
        var offers = sim.Content.Offers;
        if (!visit.IsHere) return ("NoTrader", "No trade wagon is here");
        if (offer < 0 || offer >= offers.Count) return ("BadOffer", "No such offer");
        if (lots < 1) return ("BadLots", "Pick at least one lot");
        if (lots > visit.LotsLeft[offer])
            return ("OfferExhausted", visit.LotsLeft[offer] == 0 ? "No lots left" : $"Only {visit.LotsLeft[offer]} lots left");
        var o = offers[offer];
        int need = lots * o.GiveCount, have = Traders.FreeStock(sim, o.Give);
        if (need > have) return ("NotEnough", $"Not enough {ItemName(sim.Content, o.Give)}: {need} needed, {Math.Max(have, 0)} free");
        return (null, null);
    }

    /// <summary>"10 log -> 10 stone".</summary>
    public static string OfferText(ContentDb content, TradeOffer o) =>
        $"{o.GiveCount} {ItemName(content, o.Give)} -> {o.GetCount} {ItemName(content, o.Get)}";

    /// <summary>Game time in whole hours, rounded up: "3h", "1 day 4h", "2 days".</summary>
    public static string Duration(long ticks)
    {
        long hours = Math.Max(0, (ticks + TicksPerHour - 1) / TicksPerHour);
        long days = hours / HoursPerDay, h = hours % HoursPerDay;
        if (days == 0) return $"{h}h";
        string d = $"{days} day{(days == 1 ? "" : "s")}";
        return h == 0 ? d : $"{d} {h}h";
    }

    private static long LeavesIn(Simulation sim, TraderDef t)
    {
        long now = sim.Clock.Tick;
        int v = Traders.VisitAt(t, now);
        return v < 0 ? 0 : Traders.LeavesAt(t, v) - now;
    }

    private static AcceptButton Button(Simulation sim, int offer, int lots)
    {
        var (reason, tooltip) = Refusal(sim, offer, lots);
        return new AcceptButton(lots, reason, tooltip);
    }

    private static string ItemName(ContentDb content, ItemId item) => content.ItemDef(item).Name.ToLowerInvariant();
}
