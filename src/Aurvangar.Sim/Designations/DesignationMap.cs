using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Designations;

public enum DesignationMark : byte { None, Dig, DigUnreachable }

/// <summary>Per-cell dig marks (DSG-01): a byte per cell plus a sorted index of the marked cells, so iteration costs
/// the number of marks, not the world size. Commands and job posting live in <see cref="DesignationSystem"/>.</summary>
public sealed class DesignationMap
{
    private readonly VoxelWorld _world;
    private readonly byte[] _marks;
    private readonly SortedSet<int> _marked = new();

    public DesignationMap(VoxelWorld world)
    {
        _world = world;
        _marks = new byte[world.CellCount];
    }

    /// <summary>Number of cells with a mark.</summary>
    public int Count => _marked.Count;

    public DesignationMark Get(Int3 c) => _world.InBounds(c) ? (DesignationMark)_marks[_world.Index(c)] : DesignationMark.None;

    public void Set(Int3 c, DesignationMark mark)
    {
        if (!_world.InBounds(c)) return;
        int i = _world.Index(c);
        _marks[i] = (byte)mark;
        if (mark == DesignationMark.None) _marked.Remove(i);
        else _marked.Add(i);
    }

    /// <summary>JOB-08: a dig job gave up after its fifth failure. Only a live Dig mark changes.</summary>
    public void MarkUnreachable(Int3 c)
    {
        if (Get(c) == DesignationMark.Dig) Set(c, DesignationMark.DigUnreachable);
    }

    /// <summary>Marked cells in ascending index order.</summary>
    public IEnumerable<(Int3 Cell, DesignationMark Mark)> All
    {
        get { foreach (var i in _marked) yield return (_world.CellOf(i), (DesignationMark)_marks[i]); }
    }

    public void AddToHash(ref StateHasher h)
    {
        h.Add(Count);
        foreach (var i in _marked) { h.Add(i); h.Add(_marks[i]); }
    }
}
