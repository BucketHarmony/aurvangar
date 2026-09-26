namespace Colony.Sim.Core;

/// <summary>Deterministic PRNG (SplitMix64). The only randomness allowed in the sim.</summary>
public sealed class Rng
{
    public ulong State { get; set; }

    public Rng(ulong seed) { State = seed; }

    public ulong NextU64()
    {
        ulong z = State += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform int in [0, maxExclusive).</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return (int)(NextU64() % (ulong)maxExclusive);
    }

    /// <summary>Uniform int in [min, maxExclusive).</summary>
    public int NextInt(int min, int maxExclusive) => min + NextInt(maxExclusive - min);

    /// <summary>True with probability num/den.</summary>
    public bool Chance(int num, int den) => NextInt(den) < num;

    /// <summary>Derive an independent stream (e.g. per subsystem) without disturbing this one.</summary>
    public static Rng Derive(ulong seed, ulong salt) => new(Hash64(seed ^ (salt * 0xD6E8FEB86659FD93UL)));

    /// <summary>Stateless 64-bit hash; use for coordinate-based noise.</summary>
    public static ulong Hash64(ulong x)
    {
        x ^= x >> 33; x *= 0xFF51AFD7ED558CCDUL;
        x ^= x >> 33; x *= 0xC4CEB9FE1A85EC53UL;
        x ^= x >> 33;
        return x;
    }
}
