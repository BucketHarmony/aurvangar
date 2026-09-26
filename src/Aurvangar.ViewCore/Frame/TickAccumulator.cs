namespace Aurvangar.ViewCore.Frame;

/// <summary>Fixed-step clock of the view's game loop (VIEW-01). Each frame the view calls <see cref="Advance"/> and
/// runs the returned number of sim ticks. Wall time is scaled by the speed multiplier (0 = pause, 1, 3, 6); at most
/// <see cref="MaxTicksPerFrame"/> ticks run per frame and any larger backlog is dropped (no spiral of death).
/// View-only: wall-clock doubles never reach the sim.</summary>
public sealed class TickAccumulator
{
    public const double TickSeconds = 0.1;
    public const int MaxTicksPerFrame = 4;

    /// <summary>Speed multipliers; the view's speed index points into this.</summary>
    public static readonly int[] Speeds = { 0, 1, 3, 6 };

    /// <summary>Unspent scaled seconds carried to the next frame.</summary>
    public double Accumulator { get; private set; }

    /// <summary>Adds this frame's time and returns how many ticks to run now (0..MaxTicksPerFrame).</summary>
    public int Advance(double deltaSeconds, int speedMultiplier)
    {
        Accumulator += deltaSeconds * speedMultiplier;
        int ticks = 0;
        while (Accumulator >= TickSeconds && ticks < MaxTicksPerFrame)
        {
            Accumulator -= TickSeconds;
            ticks++;
        }
        if (ticks == MaxTicksPerFrame) Accumulator = Math.Min(Accumulator, TickSeconds);
        return ticks;
    }
}
