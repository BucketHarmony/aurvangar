using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;

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
        SettleGc();
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

    /// <summary>Full blocking GC before a timed section, so garbage left by setup (seed-1 worlds, earlier tests in
    /// the process) is not collected inside the measurement.</summary>
    public static void SettleGc()
    {
        EnsureHighQos();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerThrottlingState { public uint Version, ControlMask, StateMask; }

    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")]
    private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PowerThrottlingState info, int size);

    private static bool _highQos;

    /// <summary>ADR-052: on Windows, opt the test process out of EcoQoS power throttling (ProcessPowerThrottling,
    /// execution-speed control on, state off), so a windowless testhost is not parked on efficiency cores during a
    /// timed section. The game runs as a foreground window and is not throttled, so this removes a harness artifact;
    /// it does not change any budget.</summary>
    public static void EnsureHighQos()
    {
        if (_highQos || !OperatingSystem.IsWindows()) return;
        _highQos = true;
        var state = new PowerThrottlingState { Version = 1, ControlMask = 1, StateMask = 0 };
        SetProcessInformation(GetCurrentProcess(), 4, ref state, Marshal.SizeOf<PowerThrottlingState>());
    }

    public static double Median(double[] sorted) => sorted[sorted.Length / 2];
    public static double P95(double[] sorted) => sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.95))];
}

/// <summary>All perf test classes share this collection, so they run one at a time and never beside another test
/// (ADR-052): parallel classes skewed timings through CPU contention and GC pauses from the other tests' allocations.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerfCollection
{
    public const string Name = "Perf";
}
