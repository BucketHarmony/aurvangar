using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Events;

/// <summary>Base of all sim-to-view notifications (ARCH-03). Sim systems never read events.</summary>
public abstract record SimEvent;

public sealed record ChunkDirty(int ChunkIndex) : SimEvent;
/// <summary>WAT-15. View only: no sim system may react to it, because its throttle baselines are not hashed (ADR-013).</summary>
public sealed record WaterDirty(int ChunkIndex) : SimEvent;
public sealed record AgentSpawned(AgentId Agent) : SimEvent;
public sealed record AgentDied(AgentId Agent, string Cause) : SimEvent;
public sealed record BuildingPlaced(BuildingId Building) : SimEvent;
public sealed record BuildingCompleted(BuildingId Building) : SimEvent;
public sealed record BuildingRemoved(BuildingId Building) : SimEvent;
public sealed record ItemPileChanged(Int3 Cell) : SimEvent;
public sealed record CommandRejected(string Command, string Reason) : SimEvent;
public sealed record ColonyLost : SimEvent;
/// <summary>ECO-17: a season began this tick (the view refreshes its season readout, ECO-18).</summary>
public sealed record SeasonChanged(Aurvangar.Sim.Water.Season Season) : SimEvent;

/// <summary>CRF-11 (M11-T4): a Make order reached its count and was removed.</summary>
public sealed record WorkshopOrderDone(BuildingId Building, int Recipe) : SimEvent;

/// <summary>CRF-17 (M11-T5): the trade wagon arrived beside the Great Hall.</summary>
public sealed record TraderArrived(BuildingId Building) : SimEvent;

/// <summary>CRF-21 (M11-T5): the trade wagon left.</summary>
public sealed record TraderLeft(BuildingId Building) : SimEvent;

/// <summary>CRF-17 (M11-T5): a visit was skipped because no site beside the hall was free.</summary>
public sealed record TraderNoRoom : SimEvent;

/// <summary>Per-tick event buffer. The view drains it after each tick.</summary>
public sealed class EventBus
{
    private readonly List<SimEvent> _events = new();

    public void Emit(SimEvent e) => _events.Add(e);

    public IReadOnlyList<SimEvent> Pending => _events;

    /// <summary>Returns and clears all pending events.</summary>
    public List<SimEvent> Drain()
    {
        var copy = new List<SimEvent>(_events);
        _events.Clear();
        return copy;
    }
}
