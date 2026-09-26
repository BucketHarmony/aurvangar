using System.Diagnostics;

namespace Aurvangar.Sim.Tests.Perf;

public static class PerfHelpers
{
    /// <summary>Budget multiplier for slow machines (PERF_SCALE env var, default 1.0).</summary>
    public static double Scale =>
        double.TryParse(Environment.GetEnvironmentVariable("PERF_SCALE"), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1.0;

    /// <summary>Runs action warmup + iterations times, returns per-iteration milliseconds (measured part only).</summary>
    public static double[] Measure(Action action, int iterations, int warmup = 5)
    {
        for (int i = 0; i < warmup; i++) action();
        var times = new double[iterations];
        for (int i = 0; i < iterations; i++)
        {
            long t = Stopwatch.GetTimestamp();
            action();
            times[i] = Stopwatch.GetElapsedTime(t).TotalMilliseconds;
        }
        Array.Sort(times);
        return times;
    }

    public static double Median(double[] sorted) => sorted[sorted.Length / 2];
    public static double P95(double[] sorted) => sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.95))];
}
