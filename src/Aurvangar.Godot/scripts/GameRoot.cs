using Aurvangar.Sim;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Frame;
using Aurvangar.ViewCore.Meshing;
using Godot;

namespace Aurvangar.Client;

/// <summary>Root node. Owns the Simulation and runs the fixed-tick loop (VIEW-01): ticks come from a
/// <see cref="TickAccumulator"/>, events drained after each tick are routed by a <see cref="RemeshRouter"/> into the
/// terrain and water remesh queues, and up to 4 + 4 chunks are remeshed per frame (VIEW-02).</summary>
public partial class GameRoot : Node3D
{
    public Simulation Sim { get; private set; } = null!;
    public ContentDb Content { get; private set; } = null!;
    public ChunkRenderer Terrain { get; private set; } = null!;
    public WaterRenderer WaterView { get; private set; } = null!;
    public RemeshRouter Remesh { get; private set; } = null!;

    /// <summary>Index into TickAccumulator.Speeds. 1 = 1x.</summary>
    public int SpeedIndex { get; set; } = 1;

    /// <summary>VIEW-04 view level; cells above it are hidden. Defaults to SizeY - 1 (no slicing). M3-T5 drives it.</summary>
    public int SliceY { get; private set; }

    private readonly TickAccumulator _clock = new();
    private readonly List<SimEvent> _frameEvents = new();
    private readonly List<int> _batch = new();

    public override void _Ready()
    {
        Content = ContentDb.LoadEmbedded();
        ulong seed = ReadSeedFromCmdline() ?? 1UL;
        Sim = WorldFactory.Create(seed, Content);
        var w = Sim.World;
        GD.Print($"Aurvangar: seed {seed}, world {w.SizeX}x{w.SizeY}x{w.SizeZ}");
        SliceY = w.SizeY - 1;

        Terrain = new ChunkRenderer { Name = "Terrain" };
        Terrain.Init(Sim, new BlockColors(Content));
        AddChild(Terrain);
        WaterView = new WaterRenderer { Name = "Water" };
        WaterView.Init(Sim, new WaterColors(Content));
        AddChild(WaterView);

        Remesh = new RemeshRouter(w.ChunksX, w.ChunksY, w.ChunksZ);
        Remesh.EnqueueAll();
        Sim.Events.Drain(); // everything is queued; world-creation events carry nothing new
    }

    public override void _Process(double delta)
    {
        int ticks = _clock.Advance(delta, TickAccumulator.Speeds[SpeedIndex]);
        for (int i = 0; i < ticks; i++)
        {
            Sim.Tick();
            _frameEvents.AddRange(Sim.Events.Drain());
        }

        if (_frameEvents.Count > 0)
        {
            Remesh.Route(_frameEvents);
            _frameEvents.Clear();
        }

        Remesh.Terrain.TakeBatch(RemeshRouter.TerrainBudget, _batch);
        foreach (int ci in _batch) Terrain.Remesh(ci, SliceY);
        Remesh.Water.TakeBatch(RemeshRouter.WaterBudget, _batch);
        foreach (int ci in _batch) WaterView.Remesh(ci, SliceY);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            switch (key.Keycode)
            {
                case Key.Space: SpeedIndex = SpeedIndex == 0 ? 1 : 0; break;
                case Key.Key1: SpeedIndex = 1; break;
                case Key.Key2: SpeedIndex = 2; break;
                case Key.Key3: SpeedIndex = 3; break;
            }
        }
    }

    private static ulong? ReadSeedFromCmdline()
    {
        var args = OS.GetCmdlineUserArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--seed" && ulong.TryParse(args[i + 1], out var s)) return s;
        return null;
    }
}
