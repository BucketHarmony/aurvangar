using System.Diagnostics;
using Aurvangar.Sim;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Camera;
using Aurvangar.ViewCore.Diagnostics;
using Aurvangar.ViewCore.Frame;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;
using Godot;

namespace Aurvangar.Client;

/// <summary>Root node. Owns the Simulation and runs the fixed-tick loop (VIEW-01): ticks come from a
/// <see cref="TickAccumulator"/>, events drained after each tick are routed by a <see cref="RemeshRouter"/> into the
/// terrain and water remesh queues, and up to 4 + 4 chunks are remeshed per frame (VIEW-02). Also drives the slice
/// level (VIEW-04), mouse picking (VIEW-05), the orbit camera (VIEW-06) and the F3 overlay (VIEW-17).</summary>
public partial class GameRoot : Node3D
{
    public Simulation Sim { get; private set; } = null!;
    public ContentDb Content { get; private set; } = null!;
    public ChunkRenderer Terrain { get; private set; } = null!;
    public WaterRenderer WaterView { get; private set; } = null!;
    public RemeshRouter Remesh { get; private set; } = null!;
    public SliceController Slice { get; private set; } = null!;

    /// <summary>Index into TickAccumulator.Speeds. 1 = 1x.</summary>
    public int SpeedIndex { get; set; } = 1;

    /// <summary>VIEW-04 view level; cells above it are hidden. Defaults to SizeY - 1 (no slicing).</summary>
    public int SliceY => Slice.SliceY;

    /// <summary>VIEW-05: the solid cell and face under the mouse, or null.</summary>
    public PickHit? Hover { get; private set; }

    private readonly TickAccumulator _clock = new();
    private readonly List<SimEvent> _frameEvents = new();
    private readonly List<int> _batch = new();
    private readonly RollingAverage _tickMs = new(PhaseTimer.Window);
    private readonly PhaseTimer _phases = new();
    private readonly RateMeter _pathRate = new();
    private DebugOverlay _overlay = null!;
    private HoverMarker _hoverMarker = null!;

    public override void _Ready()
    {
        Content = ContentDb.LoadEmbedded();
        ulong seed = ReadSeedFromCmdline() ?? 1UL;
        Sim = WorldFactory.Create(seed, Content);
        Sim.Profiler = _phases;
        var w = Sim.World;
        GD.Print($"Aurvangar: seed {seed}, world {w.SizeX}x{w.SizeY}x{w.SizeZ}");

        Terrain = new ChunkRenderer { Name = "Terrain" };
        Terrain.Init(Sim, new BlockColors(Content));
        AddChild(Terrain);
        WaterView = new WaterRenderer { Name = "Water" };
        WaterView.Init(Sim, new WaterColors(Content));
        AddChild(WaterView);
        _hoverMarker = new HoverMarker { Name = "HoverMarker" };
        AddChild(_hoverMarker);
        _overlay = new DebugOverlay { Name = "DebugOverlay" };
        AddChild(_overlay);

        Remesh = new RemeshRouter(w.ChunksX, w.ChunksY, w.ChunksZ);
        Slice = new SliceController(w.SizeY);
        Remesh.EnqueueAll();
        Sim.Events.Drain(); // everything is queued; world-creation events carry nothing new

        var hub = Sim.Buildings.All.FirstOrDefault();
        var focus = hub != null
            ? new System.Numerics.Vector3(hub.Origin.X + 0.5f, hub.Origin.Y, hub.Origin.Z + 0.5f)
            : new System.Numerics.Vector3(w.SizeX / 2f, w.SizeY / 2f, w.SizeZ / 2f);
        GetNode<CameraRig>("Camera").Init(new OrbitRig(w.SizeX, w.SizeZ, focus, focus.Y), () => SliceY);
    }

    public override void _Process(double delta)
    {
        int ticks = _clock.Advance(delta, TickAccumulator.Speeds[SpeedIndex]);
        for (int i = 0; i < ticks; i++)
        {
            long start = Stopwatch.GetTimestamp();
            Sim.Tick();
            _tickMs.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
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

        _pathRate.Sample(Time.GetTicksMsec() / 1000.0, Sim.Counters.PathSearches);
        if (_overlay.Visible) _overlay.SetText(DebugOverlayText.Build(Snapshot()));
    }

    public override void _PhysicsProcess(double delta)
    {
        Hover = PickUnderMouse();
        _hoverMarker.SetHit(Hover);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } key)
        {
            switch (key.Keycode)
            {
                // VIEW-04: slice keys repeat while held.
                case Key.Pageup or Key.Bracketright: Slice.Step(+1, Remesh); return;
                case Key.Pagedown or Key.Bracketleft: Slice.Step(-1, Remesh); return;
            }
            if (key.Echo) return;
            switch (key.Keycode)
            {
                case Key.Space: SpeedIndex = SpeedIndex == 0 ? 1 : 0; break;
                case Key.Key1: SpeedIndex = 1; break;
                case Key.Key2: SpeedIndex = 2; break;
                case Key.Key3: SpeedIndex = 3; break;
                case Key.F3: _overlay.Toggle(); break;
            }
        }
    }

    /// <summary>VIEW-05: ray from the camera through the mouse against terrain collision (physics layer 1).</summary>
    private PickHit? PickUnderMouse()
    {
        var viewport = GetViewport();
        var camera = viewport.GetCamera3D();
        if (camera == null) return null;
        var mouse = viewport.GetMousePosition();
        var from = camera.ProjectRayOrigin(mouse);
        var to = from + camera.ProjectRayNormal(mouse) * camera.Far;
        var query = PhysicsRayQueryParameters3D.Create(from, to, ChunkRenderer.PickLayer);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return null;
        return PickResolver.Resolve(Sim.World, CameraRig.ToNumerics(hit["position"].AsVector3()),
            CameraRig.ToNumerics(hit["normal"].AsVector3()), SliceY);
    }

    private DebugSnapshot Snapshot() => new()
    {
        Fps = Engine.GetFramesPerSecond(),
        SimMsPerTick = _tickMs.Average,
        WaterActiveCells = Sim.Water.ActiveCount,
        WaterStepMs = _phases.For(TickPhase.Water).Average,
        PathSearchesPerSecond = _pathRate.PerSecond,
        RegionRebuildMs = _phases.For(TickPhase.Regions).Average,
        OpenJobsByKind = null, // M4-T6: job board
        Tick = Sim.Clock.Tick,
        SliceY = SliceY,
        MaxSliceY = Slice.MaxY,
        SpeedMultiplier = TickAccumulator.Speeds[SpeedIndex],
        Hover = Hover,
        HoverBlock = Hover is { } h ? Sim.World.GetBlock(h.Cell).ToString() : null,
    };

    private static ulong? ReadSeedFromCmdline()
    {
        var args = OS.GetCmdlineUserArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--seed" && ulong.TryParse(args[i + 1], out var s)) return s;
        return null;
    }
}
