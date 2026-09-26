using System.Diagnostics;
using Aurvangar.Sim.Core;

namespace Aurvangar.ViewCore.Diagnostics;

/// <summary>View-side <see cref="ITickProfiler"/>: wall-clock milliseconds per tick phase, averaged over the last
/// 60 ticks (VIEW-17, ADR-019). Lives outside the sim so the sim never reads time.</summary>
public sealed class PhaseTimer : ITickProfiler
{
    public const int Window = 60;

    private readonly RollingAverage[] _averages;
    private readonly long[] _started;

    public PhaseTimer()
    {
        int n = Enum.GetValues<TickPhase>().Length;
        _averages = new RollingAverage[n];
        for (int i = 0; i < n; i++) _averages[i] = new RollingAverage(Window);
        _started = new long[n];
    }

    public RollingAverage For(TickPhase phase) => _averages[(int)phase];

    public void Begin(TickPhase phase) => _started[(int)phase] = Stopwatch.GetTimestamp();

    public void End(TickPhase phase)
    {
        long start = _started[(int)phase];
        if (start == 0) return;
        _started[(int)phase] = 0;
        _averages[(int)phase].Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
}
