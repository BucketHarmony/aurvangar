using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Save;

public static partial class SaveGame
{
    /// <summary>CRF-22 (M11-T5, v9): the trader visit: the trader building id (0 when none); while a trader is here the
    /// lots left per offer and the deals (offer, lots, paid, granted) in order.</summary>
    private static void WriteTraders(BinaryWriter w, Simulation sim)
    {
        w.Section(SaveSection.Traders);
        var v = sim.Trader;
        w.Write(v.Building.Value);
        if (!v.IsHere) return;
        w.WriteCount(v.LotsLeft.Count);
        foreach (var n in v.LotsLeft) w.Write(n);
        w.WriteCount(v.Deals.Count);
        foreach (var d in v.Deals) { w.Write(d.Offer); w.Write(d.Lots); w.Write(d.Paid); w.Write(d.Granted); }
    }

    private static void ReadTraders(BinaryReader r, Simulation sim, ContentDb content)
    {
        r.ExpectSection(SaveSection.Traders);
        var id = new BuildingId(r.ReadInt32());
        if (id.Value == 0)
        {
            if (sim.Buildings.All.Any(x => x.Def.Trader is not null))
                throw new InvalidDataException("Save file is corrupt: a trade wagon stands but no trader visit is saved.");
            return;
        }
        var b = sim.Buildings.Get(id);
        if (b is null || content.TraderBuilding is null || b.Def != content.TraderBuilding)
            throw new InvalidDataException($"Save file is corrupt: trader building {id.Value} is not a trade wagon.");
        var offers = content.Offers;
        int n = r.ReadCount(offers.Count, "trader offer");
        if (n != offers.Count) throw new InvalidDataException($"Save file is corrupt: trader has {n} offers, content has {offers.Count}.");
        sim.Trader.Begin(id, offers);
        for (int i = 0; i < n; i++)
        {
            int left = r.ReadInt32();
            if (left < 0 || left > offers[i].Lots) throw new InvalidDataException($"Save file is corrupt: trader offer {i} has {left} lots left.");
            sim.Trader.LotsLeft[i] = left;
        }
        int deals = r.ReadCount(offers.Count * ContentDb.MaxOfferLots, "trade deal");
        for (int k = 0; k < deals; k++)
        {
            int offer = r.ReadIndex(offers.Count, "trade deal offer");
            int lots = r.ReadInt32(), paid = r.ReadInt32(), granted = r.ReadInt32();
            int give = offers[offer].GiveCount;
            if (lots < 1 || lots > offers[offer].Lots || paid < 0 || paid > lots * give || granted < 0 || granted > lots
                || granted != paid / give)
                throw new InvalidDataException($"Save file is corrupt: trade deal {k} (lots {lots}, paid {paid}, granted {granted}).");
            sim.Trader.Deals.Add(new TradeDeal { Offer = offer, Lots = lots, Paid = paid, Granted = granted });
        }
    }
}
