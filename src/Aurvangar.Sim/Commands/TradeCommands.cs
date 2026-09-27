using Aurvangar.Sim.Buildings;

namespace Aurvangar.Sim.Commands;

/// <summary>CRF-18 (M11-T5): accepts <paramref name="Lots"/> lots of offer <paramref name="Offer"/> from the trader
/// here. Rejected, in this order, with NoTrader, BadOffer, BadLots (under 1), OfferExhausted (more than the lots left)
/// or NotEnough (the give total exceeds <see cref="Traders.FreeStock"/>).</summary>
public sealed record AcceptOffer(int Offer, int Lots) : ICommand
{
    public string Tag => "AcceptOffer";

    public void Apply(Simulation sim) => Traders.AcceptOffer(sim, Tag, Offer, Lots);
}
