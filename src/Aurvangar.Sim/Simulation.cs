using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Paths;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim;

/// <summary>Root of the simulation. Owns all state and runs systems in the fixed order of ARCH-01.
/// Add new systems as fields here, construct them in the constructor, tick them in Tick, hash them in StateHash,
/// and save them in SaveGame — all in the same task.</summary>
public sealed class Simulation
{
    public ulong Seed { get; }
    public ContentDb Content { get; }
    public VoxelWorld World { get; }
    public WaterGrid Water { get; }
    public PlantSystem Plants { get; }
    public PathGrid PathGrid { get; }
    public Pathfinder Pathfinder { get; }
    public Regions Regions { get; }
    /// <summary>M4-T14 what-if connectivity for digs. Derived: not hashed, not saved.</summary>
    public DigTrial DigTrial { get; }
    public AgentSystem Agents { get; }
    public BuildingSystem Buildings { get; }
    public ItemPiles Piles { get; }
    public JobBoard Jobs { get; }
    public DesignationMap Designations { get; }
    public WorldActions Actions { get; }
    public SimClock Clock { get; } = new();
    public Rng Rng { get; }
    public EventBus Events { get; } = new();
    public CommandQueue Commands { get; } = new();

    /// <summary>Wall-clock-free perf counters. Never read by gameplay code.</summary>
    public SimCounters Counters { get; } = new();

    /// <summary>Optional phase timing hook for the debug overlay (VIEW-17, ADR-019). Not state; never hashed or saved.</summary>
    public ITickProfiler? Profiler { get; set; }

    public Simulation(ContentDb content, int sizeX, int sizeY, int sizeZ, ulong seed)
    {
        Seed = seed;
        Content = content;
        Rng = Rng.Derive(seed, salt: 1);
        World = new VoxelWorld(sizeX, sizeY, sizeZ, content.SolidTable);
        Water = new WaterGrid(World);
        Plants = new PlantSystem(World);
        PathGrid = new PathGrid(World, Water, Plants);
        Pathfinder = new Pathfinder(PathGrid);
        Regions = new Regions(PathGrid);
        DigTrial = new DigTrial(PathGrid);
        Agents = new AgentSystem(Events);
        Buildings = new BuildingSystem(World, Plants, PathGrid);
        Piles = new ItemPiles(World, Events);
        Jobs = new JobBoard(World);
        Designations = new DesignationMap(World);
        Actions = new WorldActions(this);
    }

    public void Enqueue(ICommand command) => Commands.Enqueue(command);

    /// <summary>One fixed step. Order is ARCH-01 and must not change without an ADR.</summary>
    public void Tick()
    {
        Commands.ApplyAll(this);                 // 1
        // 2  WeatherSystem.Tick                  (M6-T4)
        Profiler?.Begin(TickPhase.Water);
        Water.Tick(Events);                      // 3
        Profiler?.End(TickPhase.Water);
        // 4  MoistureMap.Tick                    (M6-T1)
        Plants.Tick(Clock);                      // 5
        // 6  NeedsSystem.Tick                    (M5-T5)
        Buildings.Tick(this);                    // 7
        DesignationSystem.Tick(this);            // 8
        HaulSystem.Tick(this);                   // 9
        Agents.Tick(this);                       // 10
        Water.EndTick(Events);                   // WAT-12/13 for changes made after the water step (ADR-013)
        PathGrid.SyncWorldChanges();             // PTH-03: before the change log is cleared
        Profiler?.Begin(TickPhase.Regions);
        if (Regions.RebuildIfDirty()) Counters.RegionRebuilds++;   // 11
        Profiler?.End(TickPhase.Regions);
        Counters.PathSearches = Pathfinder.Searches;
        foreach (var ci in World.TakeDirtyChunks()) Events.Emit(new ChunkDirty(ci));
        World.ClearChangeLog();
        Clock.Tick++;                            // 12
    }

    public void RunTicks(int n)
    {
        for (int i = 0; i < n; i++) Tick();
    }

    /// <summary>ARCH-06. Every piece of state must be included. Dead agents are excluded (SAV-06).</summary>
    public ulong StateHash()
    {
        var h = StateHasher.Create();
        h.Add(Clock.Tick);
        h.Add(Rng.State);
        h.Add(World.SizeX); h.Add(World.SizeY); h.Add(World.SizeZ);
        h.Add(World.Blocks);
        Water.AddToHash(ref h);
        Plants.AddToHash(ref h);
        Buildings.AddToHash(ref h);
        Piles.AddToHash(ref h);
        Designations.AddToHash(ref h);
        Jobs.AddToHash(ref h);
        Agents.AddToHash(ref h);
        return h.Value;
    }
}

/// <summary>Diagnostics for the debug overlay and perf tests.</summary>
public sealed class SimCounters
{
    public long PathSearches { get; set; }
    public long RegionRebuilds { get; set; }
    public long JobsCompleted { get; set; }
    public long JobsFailed { get; set; }
}
