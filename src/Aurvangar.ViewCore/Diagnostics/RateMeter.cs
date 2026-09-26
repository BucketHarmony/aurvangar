namespace Aurvangar.ViewCore.Diagnostics;

/// <summary>Turns a monotonically growing counter into a per-second rate, refreshed once a window of at least one
/// second has elapsed (e.g. path searches/s from <c>SimCounters.PathSearches</c>, VIEW-17).</summary>
public sealed class RateMeter
{
    public const double WindowSeconds = 1.0;

    private bool _started;
    private double _t0;
    private long _c0;

    public double PerSecond { get; private set; }

    /// <param name="timeSeconds">View time (monotonic).</param>
    /// <param name="count">Current counter value.</param>
    public void Sample(double timeSeconds, long count)
    {
        if (!_started)
        {
            _started = true;
            _t0 = timeSeconds;
            _c0 = count;
            return;
        }
        double dt = timeSeconds - _t0;
        if (dt < WindowSeconds) return;
        PerSecond = (count - _c0) / dt;
        _t0 = timeSeconds;
        _c0 = count;
    }
}
