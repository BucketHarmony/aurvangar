using Aurvangar.Sim.Core;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Tests.Support;

/// <summary>Builds small test worlds from boxes and ASCII layers (docs/testing.md).
/// Layer rows are Z (first row z = originZ), characters are X (first char x = originX). One char per cell.</summary>
public sealed class ScenarioBuilder
{
    /// <summary>Legend for Layer(). Add new characters here only.</summary>
    public static readonly IReadOnlyDictionary<char, string> Legend = new Dictionary<char, string>
    {
        ['.'] = "air",
        ['S'] = "stone",
        ['D'] = "dirt",
        ['G'] = "grass",
        ['B'] = "bedrock",
        ['#'] = "BuildingSolid (levee-like wall)",
        ['W'] = "air + full water",
        ['w'] = "air + half water",
        ['T'] = "air + tree base (trunk height 4)",
        ['b'] = "air + berry bush",
        [' '] = "leave unchanged",
    };

    private readonly Simulation _sim;
    private readonly List<(Int3 Cell, int Level)> _water = new();

    public ScenarioBuilder(int sizeX = 32, int sizeY = 32, int sizeZ = 32, ulong seed = 1)
    {
        _sim = new Simulation(TestContent.Db, sizeX, sizeY, sizeZ, seed);
    }

    public ScenarioBuilder FillBox(Int3 min, Int3 maxInclusive, BlockId block)
    {
        for (int y = min.Y; y <= maxInclusive.Y; y++)
            for (int z = min.Z; z <= maxInclusive.Z; z++)
                for (int x = min.X; x <= maxInclusive.X; x++)
                    Set(new Int3(x, y, z), block);
        return this;
    }

    /// <summary>Solid ground: Bedrock at y=0, Stone from y=1 to topY inclusive, across the whole world.</summary>
    public ScenarioBuilder Ground(int topY)
    {
        var w = _sim.World;
        FillBox(new Int3(0, 0, 0), new Int3(w.SizeX - 1, 0, w.SizeZ - 1), BlockId.Bedrock);
        if (topY >= 1) FillBox(new Int3(0, 1, 0), new Int3(w.SizeX - 1, topY, w.SizeZ - 1), BlockId.Stone);
        return this;
    }

    public ScenarioBuilder Layer(int y, params string[] rows) => Layer(new Int3(0, y, 0), rows);

    public ScenarioBuilder Layer(Int3 origin, params string[] rows)
    {
        for (int z = 0; z < rows.Length; z++)
        {
            for (int x = 0; x < rows[z].Length; x++)
            {
                var c = origin + new Int3(x, 0, z);
                char ch = rows[z][x];
                switch (ch)
                {
                    case ' ': break;
                    case '.': Set(c, BlockId.Air); break;
                    case 'S': Set(c, BlockId.Stone); break;
                    case 'D': Set(c, BlockId.Dirt); break;
                    case 'G': Set(c, BlockId.Grass); break;
                    case 'B': Set(c, BlockId.Bedrock); break;
                    case '#': Set(c, BlockId.BuildingSolid); break;
                    case 'W': Set(c, BlockId.Air); _water.Add((c, WaterGrid.Full)); break;
                    case 'w': Set(c, BlockId.Air); _water.Add((c, WaterGrid.Full / 2)); break;
                    case 'T': Set(c, BlockId.Air); _sim.Plants.AddTree(c); break;
                    case 'b': Set(c, BlockId.Air); _sim.Plants.AddBush(c); break;
                    default: throw new ArgumentException($"ScenarioBuilder: unknown layer char '{ch}' at {c}");
                }
            }
        }
        return this;
    }

    public ScenarioBuilder Water(Int3 cell, int level) { _water.Add((cell, level)); return this; }

    public ScenarioBuilder Source(Int3 cell) { _sim.Water.AddSource(cell); return this; }

    public ScenarioBuilder Drain(Int3 cell) { _sim.Water.AddDrain(cell); return this; }

    /// <summary>M4-T4: spawn an agent standing at the cell.</summary>
    public ScenarioBuilder Agent(Int3 at) => throw new NotImplementedException("M4-T4: ScenarioBuilder.Agent");

    /// <summary>M4-T8: place a pre-built hub (storage) with its origin at the cell.</summary>
    public ScenarioBuilder Hub(Int3 origin) => throw new NotImplementedException("M4-T8: ScenarioBuilder.Hub");

    /// <summary>M4-T8: add items to the hub placed with Hub().</summary>
    public ScenarioBuilder Stock(string item, int count) => throw new NotImplementedException("M4-T8: ScenarioBuilder.Stock");

    public Simulation Build()
    {
        foreach (var (cell, level) in _water) _sim.Water.SetLevel(cell, level);
        _sim.World.ClearChangeLog();
        _sim.World.TakeDirtyChunks();
        _sim.PathGrid.InvalidateAll();   // Set() writes raw, bypassing the change log (PTH-03)
        return _sim;
    }

    private void Set(Int3 c, BlockId b)
    {
        if (!_sim.World.InBounds(c)) throw new ArgumentException($"ScenarioBuilder: {c} out of bounds");
        _sim.World.SetBlockRaw(_sim.World.Index(c), b);
    }
}
