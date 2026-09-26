using Aurvangar.Sim;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.World;
using Godot;

namespace Aurvangar.Client;

/// <summary>Root node. Owns the Simulation and runs the fixed-tick loop (VIEW-01, VIEW-02).
/// M3-T4 adds ChunkRenderer and WaterRenderer children and routes events to them.</summary>
public partial class GameRoot : Node3D
{
    public const double TickSeconds = 0.1;
    public const int MaxTicksPerFrame = 4;
    public static readonly int[] Speeds = { 0, 1, 3, 6 };

    public Simulation Sim { get; private set; } = null!;
    public ContentDb Content { get; private set; } = null!;

    /// <summary>Index into Speeds. 1 = 1x.</summary>
    public int SpeedIndex { get; set; } = 1;

    private double _accumulator;
    private readonly List<SimEvent> _frameEvents = new();

    public override void _Ready()
    {
        Content = ContentDb.LoadEmbedded();
        ulong seed = ReadSeedFromCmdline() ?? 1UL;
        Sim = WorldFactory.Create(seed, Content);
        GD.Print($"Aurvangar: seed {seed}, world {Sim.World.SizeX}x{Sim.World.SizeY}x{Sim.World.SizeZ}");
        // M3-T4: create ChunkRenderer / WaterRenderer, enqueue all chunks for initial meshing.
    }

    public override void _Process(double delta)
    {
        _accumulator += delta * Speeds[SpeedIndex];
        int ticks = 0;
        while (_accumulator >= TickSeconds && ticks < MaxTicksPerFrame)
        {
            Sim.Tick();
            _frameEvents.AddRange(Sim.Events.Drain());
            _accumulator -= TickSeconds;
            ticks++;
        }
        if (ticks == MaxTicksPerFrame) _accumulator = Math.Min(_accumulator, TickSeconds); // no spiral of death

        if (_frameEvents.Count > 0)
        {
            // M3-T4: dispatch ChunkDirty / WaterDirty to renderers' remesh queues (budgeted per frame, VIEW-02).
            _frameEvents.Clear();
        }
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
