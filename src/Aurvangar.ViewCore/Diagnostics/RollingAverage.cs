namespace Aurvangar.ViewCore.Diagnostics;

/// <summary>Mean of the last N samples (ring buffer). Used for "sim ms per tick (avg over 60)" (VIEW-17).</summary>
public sealed class RollingAverage
{
    private readonly double[] _samples;
    private int _next;
    private double _sum;

    public RollingAverage(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _samples = new double[capacity];
    }

    public int Count { get; private set; }

    public double Average => Count == 0 ? 0.0 : _sum / Count;

    public void Add(double value)
    {
        if (Count == _samples.Length) _sum -= _samples[_next];
        else Count++;
        _samples[_next] = value;
        _sum += value;
        _next = (_next + 1) % _samples.Length;
    }
}
