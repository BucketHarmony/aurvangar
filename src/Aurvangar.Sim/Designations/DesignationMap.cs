using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Designations;

public enum DesignationMark : byte { None, Dig, DigUnreachable }

/// <summary>Per-cell dig marks (DSG-01). M4-T6: the store and the JOB-08 give-up mark. M4-T7: the commands and
/// <c>DesignationSystem.Tick</c> that posts dig jobs.</summary>
public sealed class DesignationMap
{
    private readonly VoxelWorld _world;
    private readonly byte[] _marks;

    public DesignationMap(VoxelWorld world)
    {
        _world = world;
        _marks = new byte[world.CellCount];
    }

    /// <summary>Number of cells with a mark.</summary>
    public int Count { get; private set; }

    public DesignationMark Get(Int3 c) => _world.InBounds(c) ? (DesignationMark)_marks[_world.Index(c)] : DesignationMark.None;

    public void Set(Int3 c, DesignationMark mark)
    {
        if (!_world.InBounds(c)) return;
        int i = _world.Index(c);
        if (_marks[i] != 0) Count--;
        _marks[i] = (byte)mark;
        if (mark != DesignationMark.None) Count++;
    }

    /// <summary>JOB-08: a dig job gave up after its fifth failure. Only a live Dig mark changes.</summary>
    public void MarkUnreachable(Int3 c)
    {
        if (Get(c) == DesignationMark.Dig) Set(c, DesignationMark.DigUnreachable);
    }

    /// <summary>Marked cells in ascending index order.</summary>
    public IEnumerable<(Int3 Cell, DesignationMark Mark)> All
    {
        get
        {
            if (Count == 0) yield break;
            for (int i = 0; i < _marks.Length; i++)
                if (_marks[i] != 0) yield return (_world.CellOf(i), (DesignationMark)_marks[i]);
        }
    }

    public void AddToHash(ref StateHasher h)
    {
        h.Add(Count);
        if (Count == 0) return;
        for (int i = 0; i < _marks.Length; i++)
            if (_marks[i] != 0) { h.Add(i); h.Add(_marks[i]); }
    }
}
