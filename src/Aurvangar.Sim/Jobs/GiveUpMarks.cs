using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Jobs;

/// <summary>What posts a recurring job that can be given up (JOB-12): a construction site's deliveries (id = building
/// id), a pump's OperatePump job (pump id), a pump's buffer haul (pump id), or a loose pile's haul (id = flat cell
/// index). Never renumber: saved.</summary>
public enum GiveUpSource : byte { Site = 1, Pump, PumpHaul, Pile }

/// <summary>Strikes against one job source (JOB-12). <see cref="Cell"/> is the job target at the last strike (site
/// entrance, pump stand cell, pump entrance, pile cell): the reset radius is measured from it.</summary>
public sealed class GiveUpMark
{
    public GiveUpSource Source { get; init; }
    public int Id { get; init; }
    public Int3 Cell { get; set; }
    public int Strikes { get; set; }
    /// <summary>The last strike came from the region check (not from failures): the mark also goes when the cell is
    /// back in a living agent's region (ADR-058).</summary>
    public bool Unreachable { get; set; }

    public bool GivenUp => Strikes >= GiveUpMarks.StrikeLimit;
}

/// <summary>JOB-12 give-up marks (M7-T5, ADR-058), by source in ascending (source, id) order. Sim state: saved and
/// hashed. Written by <see cref="JobGiveUp"/> (strikes, success, resets); read by the job posters, which post nothing
/// for a given-up source, and by the HUD.</summary>
public sealed class GiveUpMarks
{
    /// <summary>Strikes at which a source is given up.</summary>
    public const int StrikeLimit = 3;

    private readonly SortedDictionary<long, GiveUpMark> _marks = new();

    private static long Key(GiveUpSource s, int id) => ((long)s << 32) | (uint)id;

    /// <summary>All marks (struck or given up), ascending (source, id).</summary>
    public IEnumerable<GiveUpMark> All => _marks.Values;

    public int Count => _marks.Count;

    public GiveUpMark? Get(GiveUpSource s, int id) => _marks.TryGetValue(Key(s, id), out var m) ? m : null;

    public bool IsGivenUp(GiveUpSource s, int id) => _marks.TryGetValue(Key(s, id), out var m) && m.GivenUp;

    public int StrikesOf(GiveUpSource s, int id) => _marks.TryGetValue(Key(s, id), out var m) ? m.Strikes : 0;

    /// <summary>One more strike against the source; true when this strike gives it up.</summary>
    public bool Strike(GiveUpSource s, int id, Int3 cell, bool unreachable = false)
    {
        long k = Key(s, id);
        if (!_marks.TryGetValue(k, out var m)) _marks[k] = m = new GiveUpMark { Source = s, Id = id };
        m.Cell = cell;
        m.Unreachable = unreachable;
        if (m.Strikes < StrikeLimit) m.Strikes++;   // a job already claimed may still fail after the give-up
        return m.Strikes == StrikeLimit;
    }

    /// <summary>Forgets the source's mark (a job from it succeeded, the world changed near it, or it is gone).</summary>
    public void Remove(GiveUpSource s, int id) => _marks.Remove(Key(s, id));

    public void Clear() => _marks.Clear();

    /// <summary>SaveGame load.</summary>
    internal void Restore(GiveUpMark m) => _marks[Key(m.Source, m.Id)] = m;

    /// <summary>Hashed only when there is a mark, so a game that never strikes a job hashes as before M7-T5.</summary>
    public void AddToHash(ref StateHasher h)
    {
        if (_marks.Count == 0) return;
        h.Add(_marks.Count);
        foreach (var m in _marks.Values)
        {
            h.Add((byte)m.Source); h.Add(m.Id); h.Add(m.Cell); h.Add(m.Strikes); h.Add(m.Unreachable);
        }
    }
}
