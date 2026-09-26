namespace Colony.Sim.Commands;

/// <summary>Player intent. The only way the view changes sim state (ARCH-02).
/// Implementations validate in Apply and emit CommandRejected on failure instead of throwing.</summary>
public interface ICommand
{
    /// <summary>Stable type tag for the command log and save file. Never reuse a tag.</summary>
    string Tag { get; }

    void Apply(Simulation sim);
}

/// <summary>Commands queued between ticks, applied in enqueue order at the start of the next tick.</summary>
public sealed class CommandQueue
{
    private readonly List<ICommand> _pending = new();

    /// <summary>Every applied command with the tick it was applied on. Saved for replay (SAV-01).</summary>
    public List<(long Tick, ICommand Command)> Log { get; } = new();

    public void Enqueue(ICommand command) => _pending.Add(command);

    public int PendingCount => _pending.Count;

    internal void ApplyAll(Simulation sim)
    {
        if (_pending.Count == 0) return;
        var batch = _pending.ToArray();
        _pending.Clear();
        foreach (var c in batch)
        {
            Log.Add((sim.Clock.Tick, c));
            c.Apply(sim);
        }
    }
}
