using Colony.Sim.Core;

namespace Colony.Sim.Events;

/// <summary>Base of all sim-to-view notifications (ARCH-03). Sim systems never read events.</summary>
public abstract record SimEvent;

public sealed record ChunkDirty(int ChunkIndex) : SimEvent;
public sealed record WaterDirty(int ChunkIndex) : SimEvent;
public sealed record AgentSpawned(AgentId Agent) : SimEvent;
public sealed record AgentDied(AgentId Agent, string Cause) : SimEvent;
public sealed record BuildingPlaced(BuildingId Building) : SimEvent;
public sealed record BuildingCompleted(BuildingId Building) : SimEvent;
public sealed record BuildingRemoved(BuildingId Building) : SimEvent;
public sealed record ItemPileChanged(Int3 Cell) : SimEvent;
public sealed record CommandRejected(string Command, string Reason) : SimEvent;
public sealed record ColonyLost : SimEvent;

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
