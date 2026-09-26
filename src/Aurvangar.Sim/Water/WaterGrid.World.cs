using Aurvangar.Sim.Events;

namespace Aurvangar.Sim.Water;

/// <summary>World interaction (WAT-12, WAT-13) and WaterDirty throttling (WAT-15).
/// Block changes are read from <c>VoxelWorld.ChangedCells</c> through a cursor: <see cref="Tick"/> consumes the
/// changes made before the water step (commands, setup), and <see cref="EndTick"/> consumes the rest of the tick's
/// changes (buildings, agents) just before <c>Simulation</c> clears the log (ADR-013).</summary>
public sealed partial class WaterGrid
{
    /// <summary>WAT-15: a chunk's WaterDirty fires once any of its cells drifts this far from its level at the
    /// chunk's last event.</summary>
    public const int DirtyThreshold = 32;

    private int _changeCursor;

    // WAT-15 throttle. View-notification state only: not hashed or saved (ADR-013).
    private readonly ushort[] _emitted;          // level of each cell when its chunk last emitted
    private readonly bool[] _drift;              // cell changed since its chunk last emitted
    private readonly List<int>?[] _chunkDrift;   // per chunk: cells with _drift set
    private readonly bool[] _chunkEmit;
    private readonly List<int> _emitChunks = new();

    /// <summary>End-of-tick hook (ARCH-01, after agents): consume the remaining world changes and emit WaterDirty.
    /// The caller clears <c>VoxelWorld.ChangedCells</c> right after, so the cursor restarts at 0.</summary>
    public void EndTick(EventBus events)
    {
        ConsumeWorldChanges();
        FlushDirty(events);
        _changeCursor = 0;
    }

    /// <summary>Process block changes not yet seen. Each entry is judged by the cell's current block, so a cell
    /// changed twice is handled once per entry with the same (idempotent) result.</summary>
    private void ConsumeWorldChanges()
    {
        var changes = _world.ChangedCells;
        if (_changeCursor > changes.Count) _changeCursor = 0;   // log was cleared outside Simulation.Tick
        for (; _changeCursor < changes.Count; _changeCursor++)
        {
            int c = changes[_changeCursor];
            if (_world.IsSolidAt(c) && _level[c] > 0) PushOut(c);
            ActivateAround(c);                                // WAT-13 (and neighbors of a new solid)
        }
    }

    /// <summary>WAT-12: a cell that became solid gives its water away: equal shares to its open horizontal
    /// neighbors (each capped by its free room), the division remainder and whatever did not fit to the cell
    /// above if it is open, and the rest is counted as <see cref="WaterStats.Evaporated"/>.</summary>
    private void PushOut(int c)
    {
        int rest = _level[c];
        _level[c] = 0;
        NoteLevelChange(c);

        int n = HorizontalNeighbors(c);
        if (n > 0)
        {
            int share = rest / n;
            for (int k = 0; k < n; k++)
                rest -= Give(_nbr[k], share);
        }

        int above = c + _world.SizeX * _world.SizeZ;
        if (rest > 0 && above < _level.Length && !_world.IsSolidAt(above))
            rest -= Give(above, rest);

        Stats.Evaporated += rest;
    }

    /// <summary>Add up to <paramref name="amount"/> to an open cell without exceeding Full. Returns what was added.
    /// </summary>
    private int Give(int cell, int amount)
    {
        int give = Math.Min(amount, Full - _level[cell]);
        if (give <= 0) return 0;
        _level[cell] = (ushort)(_level[cell] + give);
        NoteLevelChange(cell);
        ActivateAround(cell);
        return give;
    }

    /// <summary>WAT-15: record that a cell's level changed. Its chunk is queued for a WaterDirty event if the cell
    /// is now at least <see cref="DirtyThreshold"/> away from its level at the chunk's last event, or crossed
    /// between 0 and wet.</summary>
    private void NoteLevelChange(int i)
    {
        int ci = ChunkOfCell(i);
        if (!_drift[i])
        {
            _drift[i] = true;
            (_chunkDrift[ci] ??= new List<int>()).Add(i);
        }
        if (_chunkEmit[ci]) return;
        int now = _level[i], was = _emitted[i];
        if (Math.Abs(now - was) >= DirtyThreshold || (now == 0) != (was == 0))
        {
            _chunkEmit[ci] = true;
            _emitChunks.Add(ci);
        }
    }

    /// <summary>Emit WaterDirty for queued chunks in ascending order and reset their cells' baselines.</summary>
    private void FlushDirty(EventBus events)
    {
        if (_emitChunks.Count == 0) return;
        _emitChunks.Sort();
        foreach (var ci in _emitChunks)
        {
            events.Emit(new WaterDirty(ci));
            _chunkEmit[ci] = false;
            var cells = _chunkDrift[ci];
            if (cells == null) continue;
            foreach (var i in cells)
            {
                _emitted[i] = _level[i];
                _drift[i] = false;
            }
            cells.Clear();
        }
        _emitChunks.Clear();
    }

    private int ChunkOfCell(int i)
    {
        var c = _world.CellOf(i);
        return _world.ChunkIndexOf(c);
    }
}
