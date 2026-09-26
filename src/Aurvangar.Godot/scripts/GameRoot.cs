using System.Diagnostics;
using Aurvangar.Sim;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Camera;
using Aurvangar.ViewCore.Diagnostics;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Frame;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Tools;
using Godot;

namespace Aurvangar.Client;

/// <summary>Root node. Owns the Simulation and runs the fixed-tick loop (VIEW-01): ticks come from a
/// <see cref="TickAccumulator"/>, events drained after each tick are routed by a <see cref="RemeshRouter"/> into the
/// terrain and water remesh queues, and up to 4 + 4 chunks are remeshed per frame (VIEW-02). Also drives the slice
/// level (VIEW-04), picking (VIEW-05), the camera (VIEW-06), agents, buildings, piles and designations (VIEW-08..11),
/// the HUD (VIEW-12, 15, 16) and the F3 overlay (VIEW-17). Input and drag tools live in GameRoot.Input.cs; the build
/// and deconstruct tools and the colony-lost modal (VIEW-14, 18) in GameRoot.Build.cs; F5/F9 and the renderer
/// rebuild on load in GameRoot.Save.cs.</summary>
public partial class GameRoot : Node3D
{
    public Simulation Sim { get; private set; } = null!;
    public ContentDb Content { get; private set; } = null!;
    public ChunkRenderer Terrain { get; private set; } = null!;
    public WaterRenderer WaterView { get; private set; } = null!;
    public PlantRenderer PlantView { get; private set; } = null!;
    public AgentRenderer AgentView { get; private set; } = null!;
    public PileRenderer PileView { get; private set; } = null!;
    public DesignationRenderer DesignationView { get; private set; } = null!;
    public BuildingRenderer BuildingView { get; private set; } = null!;
    public RemeshRouter Remesh { get; private set; } = null!;
    public SliceController Slice { get; private set; } = null!;

    /// <summary>Index into TickAccumulator.Speeds. 1 = 1x.</summary>
    public int SpeedIndex { get; set; } = 1;

    /// <summary>VIEW-04 view level; cells above it are hidden. Defaults to SizeY - 1 (no slicing).</summary>
    public int SliceY => Slice.SliceY;

    /// <summary>VIEW-05: the solid cell and face under the mouse, or null.</summary>
    public PickHit? Hover { get; private set; }

    /// <summary>Mouse picking and the hover marker; the screenshot harness turns it off.</summary>
    public bool PickingEnabled { get; set; } = true;

    /// <summary>With picking off, a fixed pick used as <see cref="Hover"/> (the screenshot harness shows the build
    /// ghost with it); the mouse label then sits at that cell on screen.</summary>
    public PickHit? PickOverride { get; set; }

    private readonly TickAccumulator _clock = new();
    private readonly List<SimEvent> _frameEvents = new();
    private readonly List<int> _batch = new();
    private readonly RollingAverage _tickMs = new(PhaseTimer.Window);
    private readonly PhaseTimer _phases = new();
    private readonly RateMeter _pathRate = new();
    private readonly ToolController _tool = new();
    private EntityColors _entityColors = null!;
    private DebugOverlay _overlay = null!;
    private HoverMarker _hoverMarker = null!;
    private ToolPreview _toolPreview = null!;
    private Hud _hud = null!;
    private bool _pilesDirty = true;
    private int _pilesBuiltSlice = int.MinValue;

    public override void _Ready()
    {
        Content = ContentDb.LoadEmbedded();
        _entityColors = new EntityColors(Content);
        ulong seed = ReadSeedFromCmdline() ?? 1UL;
        var sim = WorldFactory.Create(seed, Content);
        var w = sim.World;
        GD.Print($"Aurvangar: seed {seed}, world {w.SizeX}x{w.SizeY}x{w.SizeZ}");

        _hoverMarker = new HoverMarker { Name = "HoverMarker" };
        AddChild(_hoverMarker);
        _toolPreview = new ToolPreview { Name = "ToolPreview" };
        AddChild(_toolPreview);
        _overlay = new DebugOverlay { Name = "DebugOverlay" };
        AddChild(_overlay);
        ReadyBuildTools();
        _hud = new Hud { Name = "Hud", Buildable = _build.Buildable.Select(d => (d.Id, d.Name)).ToList() };
        AddChild(_hud);
        _hud.ToolChosen += SetTool;
        _hud.BuildChosen += ChooseBuilding;
        _hud.Colonists.Clicked += CenterOnAgent;

        AttachSimulation(sim);

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
        RouteEvents();

        Remesh.Terrain.TakeBatch(RemeshRouter.TerrainBudget, _batch);
        foreach (int ci in _batch) Terrain.Remesh(ci, SliceY);
        Remesh.Water.TakeBatch(RemeshRouter.WaterBudget, _batch);
        foreach (int ci in _batch) WaterView.Remesh(ci, SliceY);
        RefreshEntities((float)(_clock.Accumulator / TickAccumulator.TickSeconds));
        UpdateHud();

        _pathRate.Sample(Time.GetTicksMsec() / 1000.0, Sim.Counters.PathSearches);
        if (_overlay.Visible) _overlay.SetText(DebugOverlayText.Build(Snapshot()));
    }

    public override void _PhysicsProcess(double delta)
    {
        Hover = PickingEnabled ? PickUnderMouse() : PickOverride;
        _hoverMarker.SetHit(_tool.Tool == ToolKind.Select || !_tool.Dragging ? Hover : null);
        _tool.Move(Hover);
        if (!ToolController.IsDragTool(_tool.Tool) && _tool.Tool != ToolKind.Select) UpdateClickToolPreview();
        else if (_tool.PreviewBox(SliceY) is var (min, max))
            _toolPreview.Show(min, max, _tool.Tool == ToolKind.Cancel ? new Color(1f, 0.25f, 0.2f, 0.3f) : new Color(1f, 0.6f, 0.24f, 0.3f));
        else _toolPreview.Visible = false;
    }

    /// <summary>Runs ticks immediately (no real-time pacing) and routes their events (screenshot harness, VIEW-20).</summary>
    public void RunTicksNow(int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            Sim.Tick();
            _frameEvents.AddRange(Sim.Events.Drain());
        }
        RouteEvents();
    }

    /// <summary>Remeshes every queued terrain and water chunk now, ignoring the per-frame budget, and refreshes
    /// plants, agents, piles, designations and the colonist panel.</summary>
    public void FlushRemesh()
    {
        Remesh.Terrain.TakeBatch(int.MaxValue, _batch);
        foreach (int ci in _batch) Terrain.Remesh(ci, SliceY);
        Remesh.Water.TakeBatch(int.MaxValue, _batch);
        foreach (int ci in _batch) WaterView.Remesh(ci, SliceY);
        RefreshEntities(0f);
        UpdateHud();
    }

    /// <summary>Screenshot preset (VIEW-20): slice level, full remesh, camera view.</summary>
    public void ApplyShot(Aurvangar.ViewCore.Screenshots.CameraShot shot)
    {
        Slice.Set(shot.SliceY, Remesh);
        FlushRemesh();
        var camera = GetNode<CameraRig>("Camera");
        if (camera.Rig != null)
        {
            shot.ApplyTo(camera.Rig);
            camera.Rig.Update(0f, SliceY);
            camera.ApplyNow();
        }
        UpdatePileLabels();
    }

    private void RouteEvents()
    {
        if (_frameEvents.Count == 0) return;
        Remesh.Route(_frameEvents);
        foreach (var e in _frameEvents)
        {
            if (e is ItemPileChanged) _pilesDirty = true;
            else HandleHudEvent(e);
        }
        _frameEvents.Clear();
    }

    /// <summary>Agents every frame (they move); piles on a pile event or a slice change; designations and plants
    /// when their own change checks say so.</summary>
    private void RefreshEntities(float tickFraction)
    {
        PlantView.Refresh(SliceY);
        AgentView.Refresh(AgentVisuals.Build(Sim, SliceY, _entityColors, tickFraction));
        if (_pilesDirty || _pilesBuiltSlice != SliceY)
        {
            PileView.Rebuild(SliceY);
            _pilesDirty = false;
            _pilesBuiltSlice = SliceY;
        }
        DesignationView.Refresh(SliceY);
        BuildingView.Refresh(BuildingVisuals.Build(Sim, SliceY, _entityColors));
        UpdatePileLabels();
    }

    /// <summary>Pile count labels show at the default and closer zooms, not at the overview (VIEW-10).</summary>
    private void UpdatePileLabels()
    {
        if (GetNode<CameraRig>("Camera").Rig is { } rig) PileView.SetLabelsVisible(PileMesher.LabelsVisible(rig.Distance));
    }

    private void UpdateHud()
    {
        _hud.Colonists.SetRows(ColonistPanelModel.Build(Sim));
        _hud.TopBar.Show(TopBarModel.Build(Sim, TickAccumulator.Speeds[SpeedIndex]));
        string? label = ClickToolTooltip();
        if (label == null && Hover is { } h && PileMesher.AtPick(Sim, h, SliceY) is { } pile)
            label = PileMesher.Label(Content, pile.Stack);
        _hud.SetHoverLabel(label, LabelPoint());
    }

    /// <summary>Where the mouse label goes: the mouse, or the override pick's cell on screen.</summary>
    private Vector2 LabelPoint()
    {
        var viewport = GetViewport();
        if (PickingEnabled || PickOverride is not { } p || viewport.GetCamera3D() is not { } camera) return viewport.GetMousePosition();
        return camera.UnprojectPosition(new Vector3(p.Cell.X + 0.5f, p.Cell.Y + 1f, p.Cell.Z + 0.5f));
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
        OpenJobsByKind = Sim.Jobs.OpenCountsByKind(),
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
