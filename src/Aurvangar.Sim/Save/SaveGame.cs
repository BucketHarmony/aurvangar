using System.Text;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Save;

/// <summary>Binary save/load of the full sim state (docs/specs/save-load.md, ADR-032). A save is taken between ticks.
/// Load builds a <see cref="Simulation"/> from the file and the <see cref="ContentDb"/> only (SAV-02) and reproduces
/// <see cref="Simulation.StateHash"/> exactly (SAV-03). Derived data (path flag cache, regions, reservation tables,
/// storage totals) is rebuilt, not saved. Entity sections live in SaveGame.Entities.cs.</summary>
public static partial class SaveGame
{
    public const string Magic = "CSAV";
    public const int FormatVersion = 4;   // 2: M5-T5 need retry ticks, ColonyLost; 3: M6-T1 moisture; 4: M6-T2 farms

    /// <summary>Largest world edge a save may declare (guards the allocation on corrupt input).</summary>
    private const int MaxWorldEdge = 1024;

    public static void Save(Simulation sim, Stream output)
    {
        using var w = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        w.Write(Encoding.ASCII.GetBytes(Magic));
        w.Write(FormatVersion);

        w.Section(SaveSection.Header);
        w.Write(sim.World.SizeX); w.Write(sim.World.SizeY); w.Write(sim.World.SizeZ);
        w.Write(sim.Seed);
        w.Write(sim.Clock.Tick);
        w.Write(sim.Rng.State);
        w.Write(!sim.Regions.IsDirty);   // regions built and current (false before the first tick)

        w.Section(SaveSection.Blocks);
        w.WriteRle(sim.World.Blocks);

        WriteWater(w, sim);
        WritePlants(w, sim);

        w.Section(SaveSection.Moisture);   // ADR-046: saved, not recomputed (it reflects the last recompute)
        w.WriteRle(sim.Moisture.Flags);
        WriteFarms(w, sim);
        WriteBuildings(w, sim);
        WritePiles(w, sim);
        WriteDesignations(w, sim);
        WriteAgents(w, sim);
        WriteJobs(w, sim);

        w.Section(SaveSection.Ids);
        w.Write(sim.Plants.Ids.Next); w.Write(sim.Buildings.Ids.Next); w.Write(sim.Agents.Ids.Next); w.Write(sim.Jobs.Ids.Next);

        w.Section(SaveSection.Commands);
        w.WriteCount(sim.Commands.Log.Count);
        foreach (var (tick, c) in sim.Commands.Log) { w.Write(tick); CommandCodec.Write(w, c); }
        w.WriteCount(sim.Commands.Pending.Count);
        foreach (var c in sim.Commands.Pending) CommandCodec.Write(w, c);

        w.Section(SaveSection.End);
    }

    /// <summary>SAV-02. Throws <see cref="InvalidDataException"/> with a clear message on a wrong magic, a version
    /// mismatch (SAV-04, no migration), a truncated or corrupt file, or content the file names but the ContentDb lacks.</summary>
    public static Simulation Load(Stream input, ContentDb content)
    {
        using var r = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        try
        {
            return Read(r, content);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException("Save file is truncated.", e);
        }
        catch (KeyNotFoundException e)
        {
            throw new InvalidDataException($"Save file does not match the game content: {e.Message}", e);
        }
        catch (Exception e) when (e is FormatException or IOException)
        {
            throw new InvalidDataException($"Save file is corrupt: {e.Message}", e);
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException($"Save file is corrupt: {e.Message}", e);
        }
    }

    private static Simulation Read(BinaryReader r, ContentDb content)
    {
        var magic = r.ReadBytes(4);
        if (magic.Length < 4 || Encoding.ASCII.GetString(magic) != Magic)
            throw new InvalidDataException($"Not an Aurvangar save file: magic is not '{Magic}'.");
        int version = r.ReadInt32();
        if (version != FormatVersion)
            throw new InvalidDataException(
                $"Save file format version {version} is not supported; this build reads version {FormatVersion} only (SAV-04).");

        r.ExpectSection(SaveSection.Header);
        int sx = r.ReadInt32(), sy = r.ReadInt32(), sz = r.ReadInt32();
        if (sx <= 0 || sy <= 0 || sz <= 0 || sx > MaxWorldEdge || sy > MaxWorldEdge || sz > MaxWorldEdge
            || sx % VoxelWorld.ChunkSize != 0 || sy % VoxelWorld.ChunkSize != 0 || sz % VoxelWorld.ChunkSize != 0)
            throw new InvalidDataException($"Save file is corrupt: world size {sx}x{sy}x{sz}.");
        ulong seed = r.ReadUInt64();
        var sim = new Simulation(content, sx, sy, sz, seed);
        sim.Clock.Tick = r.ReadInt64();
        sim.Rng.State = r.ReadUInt64();
        bool regionsBuilt = r.ReadBoolean();

        r.ExpectSection(SaveSection.Blocks);
        r.ReadRle(sim.World.BlocksMutable, "blocks");
        foreach (var b in sim.World.Blocks)
            if (b >= content.Blocks.Count) throw new InvalidDataException($"Save file is corrupt: block id {b} is not defined.");

        ReadWater(r, sim);
        ReadPlants(r, sim);

        r.ExpectSection(SaveSection.Moisture);
        r.ReadRle(sim.Moisture.FlagsMutable, "moisture");
        foreach (var f in sim.Moisture.Flags)
            if (f > 1) throw new InvalidDataException($"Save file is corrupt: moisture flag {f}.");
        ReadFarms(r, sim);
        ReadBuildings(r, sim, content);
        ReadPiles(r, sim, content);
        ReadDesignations(r, sim);
        ReadAgents(r, sim, content);
        ReadJobs(r, sim, content);

        r.ExpectSection(SaveSection.Ids);
        sim.Plants.Ids.Next = r.ReadInt32(); sim.Buildings.Ids.Next = r.ReadInt32();
        sim.Agents.Ids.Next = r.ReadInt32(); sim.Jobs.Ids.Next = r.ReadInt32();

        r.ExpectSection(SaveSection.Commands);
        int logCount = r.ReadCount(int.MaxValue, "command log");
        for (int i = 0; i < logCount; i++)
        {
            long tick = r.ReadInt64();
            sim.Commands.Log.Add((tick, CommandCodec.Read(r)));
        }
        int pending = r.ReadCount(int.MaxValue, "pending commands");
        for (int i = 0; i < pending; i++) sim.Commands.Enqueue(CommandCodec.Read(r));

        r.ExpectSection(SaveSection.End);
        AfterLoad(sim, regionsBuilt);
        return sim;
    }

    /// <summary>Rebuild derived data. Regions are rebuilt now, not at the end of the first tick, because job selection
    /// reads them during the tick, when the saved game had them built; a game saved before its first tick had none,
    /// and neither does the load (ADR-032).</summary>
    private static void AfterLoad(Simulation sim, bool regionsBuilt)
    {
        sim.World.ClearChangeLog();
        sim.World.MarkAllDirty();
        sim.PathGrid.InvalidateAll();
        sim.Buildings.AfterLoad();   // M5-T2: construction sites block paths (PTH-02)
        if (regionsBuilt) sim.Regions.RebuildIfDirty();
        sim.Jobs.RebuildReservations();
        sim.Events.Drain();   // the loaded world is read by the view directly, like a new world (ADR-026)
    }

    private static void WriteWater(BinaryWriter w, Simulation sim)
    {
        var water = sim.Water;
        w.Section(SaveSection.Water);
        w.WriteRle(water.Levels);
        var active = water.ActiveSorted();
        w.WriteCount(active.Count); foreach (var i in active) w.Write(i);
        w.Write(water.SourceStrength);
        w.WriteCount(water.Sources.Count); foreach (var i in water.Sources) w.Write(i);
        w.WriteCount(water.Drains.Count); foreach (var i in water.Drains) w.Write(i);

        w.Section(SaveSection.WaterStats);
        w.Write(water.Stats.SourceAdded); w.Write(water.Stats.Drained); w.Write(water.Stats.Evaporated); w.Write(water.Stats.Pumped);
    }

    private static void ReadWater(BinaryReader r, Simulation sim)
    {
        var water = sim.Water;
        int cells = sim.World.CellCount;
        r.ExpectSection(SaveSection.Water);
        r.ReadRle(water.LevelsMutable, Water.WaterGrid.Full, "water levels");
        var active = ReadIndices(r, cells, "water active cell");
        water.SourceStrength = r.ReadInt32();
        var sources = ReadIndices(r, cells, "water source");
        var drains = ReadIndices(r, cells, "water drain");
        water.RestoreAfterLoad(active, sources, drains);

        r.ExpectSection(SaveSection.WaterStats);
        water.Stats.SourceAdded = r.ReadInt64(); water.Stats.Drained = r.ReadInt64();
        water.Stats.Evaporated = r.ReadInt64(); water.Stats.Pumped = r.ReadInt64();
    }

    private static List<int> ReadIndices(BinaryReader r, int cellCount, string what)
    {
        int n = r.ReadCount(cellCount, what);
        var list = new List<int>(n);
        for (int k = 0; k < n; k++) list.Add(r.ReadIndex(cellCount, what));
        return list;
    }
}
