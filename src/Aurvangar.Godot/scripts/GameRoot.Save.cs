using Aurvangar.Sim;
using Aurvangar.ViewCore.Frame;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Persistence;
using Godot;

namespace Aurvangar.Client;

/// <summary>F5 quick save / F9 quick load (VIEW-19) and swapping in a simulation. Input runs between ticks, so a
/// save always sees a whole tick. A load replaces <see cref="GameRoot.Sim"/> and rebuilds every sim-bound renderer
/// from scratch; the camera, slice level and speed are kept.</summary>
public partial class GameRoot
{
    public const string QuickSaveFile = "user://quick.save";

    public static string QuickSavePath => ProjectSettings.GlobalizePath(QuickSaveFile);

    public void QuickSaveNow()
    {
        string? error = QuickSave.Save(Sim, QuickSavePath);
        _hud.Toast(error ?? $"Saved (tick {Sim.Clock.Tick}).");
        if (error != null) GD.PrintErr(error);
    }

    public void QuickLoadNow()
    {
        var (sim, error) = QuickSave.Load(QuickSavePath, Content);
        if (sim == null)
        {
            _hud.Toast(error ?? "Load failed.");
            GD.PrintErr(error);
            return;
        }
        AttachSimulation(sim);
        FlushRemesh();
        _hud.Toast($"Loaded (tick {Sim.Clock.Tick}).");
    }

    /// <summary>Makes <paramref name="sim"/> the running simulation: frees the old terrain, water, plant, agent, pile,
    /// designation and building renderers, creates new ones, queues every chunk, and drops pending events and any tool
    /// drag. The slice level carries over; the colony-lost modal follows the new colony.</summary>
    public void AttachSimulation(Simulation sim)
    {
        foreach (Node? old in new Node?[] { Terrain, WaterView, PlantView, AgentView, PileView, DesignationView, BuildingView })
        {
            if (old == null) continue;
            RemoveChild(old);
            old.QueueFree();
        }

        Sim = sim;
        Sim.Profiler = _phases;
        Terrain = Add(new ChunkRenderer { Name = "Terrain" });
        Terrain.Init(Sim, new BlockColors(Content));
        WaterView = Add(new WaterRenderer { Name = "Water" });
        WaterView.Init(Sim, new WaterColors(Content));
        PlantView = Add(new PlantRenderer { Name = "Plants" });
        PlantView.Init(Sim, new PlantColors(Content));
        AgentView = Add(new AgentRenderer { Name = "Agents" });
        PileView = Add(new PileRenderer { Name = "Piles" });
        PileView.Init(Sim, _entityColors);
        DesignationView = Add(new DesignationRenderer { Name = "Designations" });
        DesignationView.Init(Sim, _entityColors);
        BuildingView = Add(new BuildingRenderer { Name = "Buildings" });

        var w = Sim.World;
        int slice = Slice?.SliceY ?? w.SizeY - 1;
        Remesh = new RemeshRouter(w.ChunksX, w.ChunksY, w.ChunksZ);
        Slice = new SliceController(w.SizeY);
        Slice.Set(slice, Remesh);
        Remesh.EnqueueAll();
        Sim.Events.Drain();   // everything is queued; creation / load events carry nothing new
        _frameEvents.Clear();
        _pilesDirty = true;
        _tool.AbortDrag();
        _build.Release();
        SyncLostModal();
    }

    private T Add<T>(T node) where T : Node
    {
        AddChild(node);
        return node;
    }
}
