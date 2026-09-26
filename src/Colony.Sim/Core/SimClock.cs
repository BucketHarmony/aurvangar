namespace Colony.Sim.Core;

/// <summary>Sim time. 10 ticks = 1 game second at 1x (ECO-01).</summary>
public sealed class SimClock
{
    public const int TicksPerSecond = 10;
    public const int TicksPerDay = 2400;

    public long Tick { get; set; }
    public int Day => (int)(Tick / TicksPerDay);
    public int TickOfDay => (int)(Tick % TicksPerDay);
}
