namespace Aurvangar.Sim.Buildings;

/// <summary>CRF-06: Make ("make N": craft until <see cref="WorkshopOrder.Done"/> reaches the count, then the order is
/// removed) or Keep ("keep at least N in stock": craft while the colony stock of the output is below the count).</summary>
public enum OrderMode : byte { Make, Keep }

/// <summary>CRF-06 (M11-T4): one workshop order. <see cref="Count"/> is 1..999 output items (not cycles);
/// <see cref="Done"/> counts outputs made for a Make order and stays 0 for Keep. Saved and hashed with its building.</summary>
public sealed class WorkshopOrder
{
    /// <summary>Largest order count (CRF-07).</summary>
    public const int MaxCount = 999;

    public int Recipe { get; init; }
    public OrderMode Mode { get; init; }
    public int Count { get; init; }
    public int Done { get; set; }
}
