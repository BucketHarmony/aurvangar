using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Buildings;

/// <summary>CRF-18/19 (M11-T5): one accepted deal: <see cref="Lots"/> lots of offer <see cref="Offer"/>, the give units
/// paid so far and the lots granted (their goods added to the trader's storage).</summary>
public sealed class TradeDeal
{
    public int Offer { get; init; }
    public int Lots { get; init; }
    public int Paid { get; set; }
    public int Granted { get; set; }
}

/// <summary>CRF-17..22 (M11-T5, ADR-082): the current trader visit. Only this is state; the schedule is a function of the
/// tick (CRF-16). Saved (v9) and hashed only while a trader is here, so a hash taken before the first arrival (or
/// between visits) does not move.</summary>
public sealed class TraderVisit
{
    /// <summary>The trader building while it is here; invalid (0) between visits.</summary>
    public BuildingId Building { get; internal set; }

    public bool IsHere => Building.IsValid;

    /// <summary>Lots still on offer this visit, by offer index.</summary>
    public List<int> LotsLeft { get; } = new();

    /// <summary>Accepted deals in the order they were accepted. The deal index is the Pay step's deal.</summary>
    public List<TradeDeal> Deals { get; } = new();

    internal void Begin(BuildingId trader, IReadOnlyList<Content.TradeOffer> offers)
    {
        Building = trader;
        LotsLeft.Clear();
        foreach (var o in offers) LotsLeft.Add(o.Lots);
        Deals.Clear();
    }

    internal void Clear()
    {
        Building = default;
        LotsLeft.Clear();
        Deals.Clear();
    }

    /// <summary>CRF-22: nothing while no trader is here.</summary>
    public void AddToHash(ref StateHasher h)
    {
        if (!IsHere) return;
        h.Add(Building.Value);
        h.Add(LotsLeft.Count); foreach (var n in LotsLeft) h.Add(n);
        h.Add(Deals.Count);
        foreach (var d in Deals) { h.Add(d.Offer); h.Add(d.Lots); h.Add(d.Paid); h.Add(d.Granted); }
    }
}
