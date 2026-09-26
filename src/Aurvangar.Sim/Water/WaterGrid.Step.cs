using System.Runtime.CompilerServices;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;

namespace Aurvangar.Sim.Water;

/// <summary>The CA step (WAT-02..09, WAT-16). Double-buffered: flows are computed from the current levels into
/// <see cref="_delta"/>, then applied in ascending index order (= ascending y, then index) with overfill
/// resolution.</summary>
public sealed partial class WaterGrid
{
    private readonly int[] _delta;
    private readonly bool[] _touchedFlag;
    private readonly List<int> _touched = new();
    private readonly IndexSort _sorter = new();
    private readonly List<int> _stepCells = new();
    private readonly List<int> _changed = new();
    private readonly int[] _nbr = new int[4];

    /// <summary>WAT-07: below this level a film on a floor evaporates.</summary>
    public const int FilmLevel = 16;

    /// <summary>One CA step (WAT-02..16). World changes made before the step are applied first (WAT-12, WAT-13);
    /// WaterDirty events (WAT-15) are emitted at the end.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]   // hot path: skip tier-0 JIT (WAT-P1)
    public void Tick(EventBus events)
    {
        ConsumeWorldChanges();
        ApplySources();
        _active.TakeSorted(_stepCells);                    // WAT-03: sorted for stable iteration
        foreach (var c in _stepCells) ComputeFlows(c);
        ApplyDeltas();
        ApplyDrains();
        foreach (var c in _changed)
        {
            ActivateAround(c);                             // WAT-02: changed cells and their neighbors
            NoteLevelChange(c);                            // WAT-15
        }
        _changed.Clear();
        FlushDirty(events);
    }

    /// <summary>WAT-09: raise each source cell to <c>Full * strength / 100</c> before the step. A source never
    /// lowers a cell (ADR-011); the added volume is counted in <see cref="WaterStats.SourceAdded"/>.</summary>
    private void ApplySources()
    {
        int target = Full * SourceStrength / 100;
        foreach (var s in _sources)
        {
            if (_world.IsSolidAt(s) || _level[s] >= target) continue;
            Stats.SourceAdded += target - _level[s];
            _level[s] = (ushort)target;
            NoteLevelChange(s);
            ActivateAround(s);
        }
    }

    /// <summary>WAT-10: empty each drain cell at the end of the step; the removed volume is counted in
    /// <see cref="WaterStats.Drained"/>. The change activates the drain's wet neighbors so water keeps flowing in.
    /// </summary>
    private void ApplyDrains()
    {
        foreach (var d in _drains)
        {
            int v = _level[d];
            if (v == 0) continue;
            Stats.Drained += v;
            _level[d] = 0;
            _changed.Add(d);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]   // hot path: skip tier-0 JIT (WAT-P1)
    private void ComputeFlows(int c)
    {
        int level = _level[c];
        if (level == 0 || _world.IsSolidAt(c)) return;

        int layer = _world.SizeX * _world.SizeZ;
        int b = c - layer;
        bool floorSolid = b < 0 || _world.IsSolidAt(b);      // below y = 0 is bedrock (WLD-04)
        int n = HorizontalNeighbors(c);

        // WAT-07: a thin film on a solid floor with no neighbor holding >= FilmLevel evaporates.
        if (floorSolid && level < FilmLevel && AllBelowFilm(n))
        {
            AddDelta(c, -level);
            Stats.Evaporated += level;
            return;
        }

        // WAT-04 fall.
        if (!floorSolid)
        {
            int move = Math.Min(level, Full - _level[b]);
            if (move > 0)
            {
                AddDelta(c, -move);
                AddDelta(b, move);
                level -= move;
            }
        }

        // WAT-05: spread only when the cell below is solid or (after fall) full. If b is open and water remains
        // after the fall, the fall filled b to Full, so "level > 0" is the whole condition. WAT-08: no upward flow.
        if (level == 0) return;
        int minTarget = -1;
        for (int k = 0; k < n; k++)
        {
            int nb = _nbr[k];
            int diff = level - _level[nb];
            if (diff <= 0) continue;
            int f = diff / 5;
            if (f > 0)
            {
                AddDelta(c, -f);
                AddDelta(nb, f);
            }
            else if (diff >= 2 && (minTarget < 0 || _level[nb] < _level[minTarget]))
            {
                minTarget = nb;                               // _nbr is ascending, so ties keep the lowest index
            }
        }

        // WAT-06 minimum flow: 1 unit to the single lowest neighbor whose computed flow rounded to 0.
        if (minTarget >= 0)
        {
            AddDelta(c, -1);
            AddDelta(minTarget, 1);
        }
    }

    /// <summary>Fill <see cref="_nbr"/> with the non-solid, in-bounds horizontal neighbors of <paramref name="c"/>
    /// in ascending index order (z-1, x-1, x+1, z+1). Returns the count.</summary>
    private int HorizontalNeighbors(int c)
    {
        int sx = _world.SizeX, sz = _world.SizeZ;
        int rem = c % (sx * sz);
        int z = rem / sx;
        int x = rem - z * sx;
        int n = 0;
        if (z > 0 && !_world.IsSolidAt(c - sx)) _nbr[n++] = c - sx;
        if (x > 0 && !_world.IsSolidAt(c - 1)) _nbr[n++] = c - 1;
        if (x < sx - 1 && !_world.IsSolidAt(c + 1)) _nbr[n++] = c + 1;
        if (z < sz - 1 && !_world.IsSolidAt(c + sx)) _nbr[n++] = c + sx;
        return n;
    }

    /// <summary>WAT-07: solid and out-of-world neighbors hold 0, so only the open ones need checking.</summary>
    private bool AllBelowFilm(int count)
    {
        for (int k = 0; k < count; k++)
            if (_level[_nbr[k]] >= FilmLevel) return false;
        return true;
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
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]   // hot path: skip tier-0 JIT (WAT-P1)
    private void ApplyDeltas()
    {
        _sorter.Sort(_touched);
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
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]   // hot path: skip tier-0 JIT (WAT-P1)
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
