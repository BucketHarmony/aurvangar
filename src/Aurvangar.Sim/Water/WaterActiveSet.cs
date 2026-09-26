namespace Aurvangar.Sim.Water;

/// <summary>WAT-02 active set: a flag per cell plus a list of flagged indices. The list order carries no meaning;
/// <see cref="Sorted"/> sorts it in place (ascending) for the step and for hashing.</summary>
internal sealed class WaterActiveSet
{
    private readonly bool[] _flag;
    private readonly List<int> _list = new();

    public WaterActiveSet(int cellCount) => _flag = new bool[cellCount];

    public int Count => _list.Count;

    public void Add(int index)
    {
        if (_flag[index]) return;
        _flag[index] = true;
        _list.Add(index);
    }

    /// <summary>The active indices, sorted ascending.</summary>
    public List<int> Sorted()
    {
        _list.Sort();
        return _list;
    }

    /// <summary>Copy the sorted indices into <paramref name="into"/> and empty the set.</summary>
    public void TakeSorted(List<int> into)
    {
        into.Clear();
        into.AddRange(Sorted());
        foreach (var i in _list) _flag[i] = false;
        _list.Clear();
    }
}
