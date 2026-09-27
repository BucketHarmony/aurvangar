using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Content;

/// <summary>CRF-15 (M11-T5): an offer resolved against the items. For each lot the colony gives <see cref="GiveCount"/>
/// of <see cref="Give"/> and gets <see cref="GetCount"/> of <see cref="Get"/>, up to <see cref="Lots"/> lots per visit.
/// <see cref="Index"/> is its position in the trader's offer list (the AcceptOffer index).</summary>
public sealed record TradeOffer(int Index, ItemId Give, int GiveCount, ItemId Get, int GetCount, int Lots);

/// <summary>CRF-15 (M11-T5, ADR-082): trader validation and the resolved offers.</summary>
public sealed partial class ContentDb
{
    /// <summary>Largest give or get count of an offer (CRF-15).</summary>
    public const int MaxOfferCount = 100;

    /// <summary>Largest lot count of an offer (CRF-15).</summary>
    public const int MaxOfferLots = 20;

    /// <summary>The one building with a <c>trader</c> block (the trade wagon), or null when the data has none.</summary>
    public BuildingDef? TraderBuilding { get; private set; }

    /// <summary>The trader's offers in offer-index order (empty without a trader).</summary>
    public IReadOnlyList<TradeOffer> Offers { get; private set; } = Array.Empty<TradeOffer>();

    /// <summary>CRF-15. Each error names the file and the building, and for an offer error the offer index.</summary>
    private void ValidateTraders()
    {
        foreach (var b in Buildings)
        {
            if (b.Trader is not { } t) continue;
            string who = $"buildings.json: trader '{b.Id}'";
            if (TraderBuilding is not null) throw new InvalidDataException($"{who}: there is already a trader ('{TraderBuilding.Id}'); at most one is allowed");
            if (!b.PrebuiltOnly) throw new InvalidDataException($"{who} must be prebuiltOnly");
            if (b.Entrance is null) throw new InvalidDataException($"{who} needs an entrance");
            if (b.Storage is null) throw new InvalidDataException($"{who} needs a storage");
            if (b.Storage.Receives) throw new InvalidDataException($"{who} storage must have receives: false");
            if (b.RemovableWhenEmpty) throw new InvalidDataException($"{who} must not be removableWhenEmpty (it is anchored)");
            if (t.FirstArrival < 1) throw new InvalidDataException($"{who} firstArrival must be at least 1");
            if (t.Stay < 1 || t.Stay > t.Interval - 1)
                throw new InvalidDataException($"{who} stay {t.Stay} must be 1..interval - 1 ({t.Interval - 1})");
            if (t.Offers is null || t.Offers.Length == 0) throw new InvalidDataException($"{who} has no offers");
            var offers = new TradeOffer[t.Offers.Length];
            for (int i = 0; i < offers.Length; i++)
            {
                var o = t.Offers[i];
                string owho = $"{who} offer {i}";
                var (give, giveN) = OneItem(owho, "give", o.Give);
                var (get, getN) = OneItem(owho, "get", o.Get);
                if (give == get) throw new InvalidDataException($"{owho} gives and gets the same item '{Items[give.Value].Id}'");
                if (o.Lots < 1 || o.Lots > MaxOfferLots) throw new InvalidDataException($"{owho} lots {o.Lots} must be 1..{MaxOfferLots}");
                if (!b.Storage.Accepts.Contains(Items[get.Value].Id, StringComparer.Ordinal))
                    throw new InvalidDataException($"{owho} get item '{Items[get.Value].Id}' is not in the trader's accepts");
                offers[i] = new TradeOffer(i, give, giveN, get, getN, o.Lots);
            }
            TraderBuilding = b;
            Offers = offers;
        }
    }

    private (ItemId Item, int Count) OneItem(string who, string what, Dictionary<string, int>? items)
    {
        if (items is null || items.Count != 1) throw new InvalidDataException($"{who} must have exactly one {what} item");
        var (key, n) = items.Single();
        if (!_itemsByKey.TryGetValue(key, out var id)) throw new InvalidDataException($"{who} has unknown {what} item '{key}'");
        if (n < 1 || n > MaxOfferCount) throw new InvalidDataException($"{who} {what} count {n} must be 1..{MaxOfferCount}");
        return (id, n);
    }
}
