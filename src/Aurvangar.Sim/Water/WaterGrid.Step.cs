using Aurvangar.Sim.Events;

namespace Aurvangar.Sim.Water;

/// <summary>The CA step (WAT-02..04, WAT-16; spread WAT-05..08 lands in M2-T2). Double-buffered: flows are computed from the current levels into
/// <see cref="_delta"/>, then applied in ascending index order (= ascending y, then index) with overfill
/// resolution.</summary>
public sealed partial class WaterGrid
{
    private readonly int[] _delta;
    private readonly bool[] _touchedFlag;
    private readonly List<int> _touched = new();
    private readonly List<int> _stepCells = new();
    private readonly List<int> _changed = new();

    /// <summary>One CA step (WAT-02..04, WAT-16). Spread, sources/drains and world interaction: M2-T2..T4.</summary>
    public void Tick(EventBus events)
    {
        _active.TakeSorted(_stepCells);                    // WAT-03: sorted for stable iteration
        foreach (var c in _stepCells) ComputeFlows(c);
        ApplyDeltas();
        foreach (var c in _changed) ActivateAround(c);     // WAT-02: changed cells and their neighbors
        _changed.Clear();
    }

    private void ComputeFlows(int c)
    {
        int level = _level[c];
        if (level == 0 || _world.IsSolidAt(c)) return;

        // WAT-04 fall. Below y = 0 is bedrock (WLD-04).
        int layer = _world.SizeX * _world.SizeZ;
        int b = c - layer;
        if (b >= 0 && !_world.IsSolidAt(b))
        {
            int move = Math.Min(level, Full - _level[b]);
            if (move > 0)
            {
                AddDelta(c, -move);
                AddDelta(b, move);
            }
        }
    }

    private void AddDelta(int index, int amount)
    {
        if (!_touchedFlag[index])
        {
            _touchedFlag[index] = true;
            _touched.Add(index);
        }
        _delta[index] += amount;
    }

    /// <summary>Apply deltas in ascending index order. WAT-16: excess over Full is pushed into the cell above
    /// (which has a higher index, so it is resolved later in the same walk) or evaporated if that cell is solid
    /// or outside the world. Working values are ints so nothing wraps.</summary>
    private void ApplyDeltas()
    {
        _touched.Sort();
        int layer = _world.SizeX * _world.SizeZ;
        int count = _touched.Count;
        for (int k = 0; k < count; k++)
        {
            int i = _touched[k];
            while (true)
            {
                int v = _level[i] + _delta[i];
                _delta[i] = 0;
                int excess = 0;
                if (v > Full) { excess = v - Full; v = Full; }
                if (v != _level[i]) { _level[i] = (ushort)v; _changed.Add(i); }
                if (excess == 0) break;

                int above = i + layer;
                if (above >= _level.Length || _world.IsSolidAt(above))
                {
                    Stats.Evaporated += excess;
                    break;
                }
                if (_touchedFlag[above])
                {
                    _delta[above] += excess;               // not yet walked: above > i
                    break;
                }
                // Untouched cell: only this push reaches it, so resolve it now and continue upward.
                _touchedFlag[above] = true;
                _touched.Add(above);
                _delta[above] = excess;
                i = above;
            }
        }
        foreach (var i in _touched) _touchedFlag[i] = false;
        _touched.Clear();
    }

    /// <summary>Activate a cell and its 6 face neighbors. Only wet cells join: a dry cell has no step rule, and it
    /// is activated through its own level change when water reaches it (ADR-010).</summary>
    private void ActivateAround(int index)
    {
        int sx = _world.SizeX, sz = _world.SizeZ, layer = sx * sz;
        int y = index / layer;
        int rem = index - y * layer;
        int z = rem / sx;
        int x = rem - z * sx;
        Activate(index);
        if (y > 0) Activate(index - layer);
        if (y < _world.SizeY - 1) Activate(index + layer);
        if (z > 0) Activate(index - sx);
        if (x < sx - 1) Activate(index + 1);
        if (z < sz - 1) Activate(index + sx);
        if (x > 0) Activate(index - 1);
    }

    private void Activate(int index)
    {
        if (_level[index] > 0) _active.Add(index);
    }
}
