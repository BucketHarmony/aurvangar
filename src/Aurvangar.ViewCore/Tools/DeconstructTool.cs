using Aurvangar.Sim;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Tools;

/// <summary>Deconstruct tool (VIEW-12, BLD-09). A click on a building sends <c>Deconstruct</c>: a blueprint or site is
/// cancelled, a complete building is torn down. The tool stays active. Engine-neutral.</summary>
public static class DeconstructTool
{
    /// <summary>The building under the pick: the one covering the picked cell (complete buildings are solid blocks),
    /// else the one covering the empty cell in front of the picked face (blueprints and sites stand on the ground).</summary>
    public static Building? Target(Simulation sim, PickHit? hit)
    {
        if (hit is not { } h) return null;
        return sim.Buildings.BuildingAt(h.Cell) ?? sim.Buildings.BuildingAt(h.Adjacent);
    }

    /// <summary>Why the building cannot be deconstructed (the same checks as the command, BLD-09), or null.</summary>
    public static string? Refusal(Simulation sim, Building b)
    {
        if (b.Def.PrebuiltOnly) return $"The {b.Def.Name} cannot be torn down";
        if (b.State == BuildingState.Deconstructing) return "Already being torn down";
        if (Construction.HasBuildingOnTop(sim, b)) return "Something is built on top of it";
        return null;
    }

    /// <summary>The hover text: what a click would do, or why it would not.</summary>
    public static string Tooltip(Simulation sim, Building b) => Refusal(sim, b) ?? b.State switch
    {
        BuildingState.Complete => $"Tear down {b.Def.Name} (half the materials back)",
        _ => $"Cancel {b.Def.Name} (all delivered materials back)",
    };

    /// <summary>Left button down (M8-T5, M9-T1): over a building it is <see cref="Click"/>; over anything else (ground,
    /// built blocks) it starts a per-block paint drag (<see cref="DeconstructPaint"/>, CON-18, VIEW-21); over nothing it
    /// does nothing.</summary>
    public static DeconstructPress Press(Simulation sim, PickHit? hit)
    {
        if (hit is null) return default;
        if (Target(sim, hit) is null) return new DeconstructPress(null, null, true);
        var (command, message) = Click(sim, hit);
        return new DeconstructPress(command, message, false);
    }

    /// <summary>The command for a click: null over nothing; a message instead of a command when refused.</summary>
    public static (ICommand? Command, string? Message) Click(Simulation sim, PickHit? hit)
    {
        if (Target(sim, hit) is not { } b) return (null, null);
        if (Refusal(sim, b) is { } why) return (null, why);
        return (new Deconstruct(b.Id), null);
    }
}

/// <summary>What a Deconstruct press did: a building command or refusal, or the start of a block paint drag.</summary>
public readonly record struct DeconstructPress(ICommand? Command, string? Message, bool StartDrag);

/// <summary>Deconstruct by block (VIEW-21, CON-18, M9-T1, ADR-067): a click marks the picked built block; a drag paints
/// the built blocks on the first block's layer (<see cref="PaintDrag"/>). The release sends one
/// <c>DesignateDeconstructBlocks</c> per built block, so the ground under a painted cell is never marked.</summary>
public sealed class DeconstructPaint
{
    private PaintDrag? _drag;

    public bool Dragging => _drag is not null;

    /// <summary>Starts a drag on the picked cell's layer (the block itself, not the air in front of the face).</summary>
    public void Start(PickHit hit) => _drag = new PaintDrag(hit, hit.Cell);

    public void Abort() => _drag = null;

    /// <summary>The cursor ray moved; falls back to <paramref name="hover"/> when the ray misses the layer's plane.</summary>
    public void MoveRay(System.Numerics.Vector3 origin, System.Numerics.Vector3 direction, PickHit? hover)
    {
        if (_drag is null) return;
        if (_drag.OnPlane(origin, direction) is { } cell) _drag.MoveTo(cell);
        else Move(hover);
    }

    /// <summary>A pick moved: paints toward the picked cell, kept on the drag's layer.</summary>
    public void Move(PickHit? hit)
    {
        if (_drag is not null && hit is { } h) _drag.MoveTo(h.Cell);
    }

    /// <summary>A player-built block (CON-01 construction block) is in the cell.</summary>
    public static bool IsBuilt(Simulation sim, Int3 cell) =>
        sim.World.InBounds(cell) && sim.Content.IsConstruction(sim.World.GetBlock(cell));

    /// <summary>The built blocks a release would mark: the painted ones during a drag, else the hovered block.</summary>
    public IReadOnlyList<Int3> Marked(Simulation sim, PickHit? hover)
    {
        if (_drag is not null) return _drag.Cells.Where(c => IsBuilt(sim, c)).ToList();
        return hover is { } h && IsBuilt(sim, h.Cell) ? new[] { h.Cell } : Array.Empty<Int3>();
    }

    /// <summary>Left button up: one <c>DesignateDeconstructBlocks</c> per painted built block, in paint order; a message
    /// when there is none.</summary>
    public (IReadOnlyList<DesignateDeconstructBlocks> Commands, string? Message) Release(Simulation sim)
    {
        if (_drag is null) return (Array.Empty<DesignateDeconstructBlocks>(), null);
        var cells = Marked(sim, null);
        _drag = null;
        if (cells.Count == 0) return (Array.Empty<DesignateDeconstructBlocks>(), "No built blocks here");
        return (cells.Select(c => new DesignateDeconstructBlocks(c, c)).ToList(), null);
    }

    /// <summary>The mouse label over built blocks: how many would come down; null over none.</summary>
    public static string? Tooltip(IReadOnlyList<Int3> marked, bool dragging) => marked.Count switch
    {
        0 => dragging ? "Drag over built blocks to take them down" : null,
        1 when !dragging => "Take down this block (full refund). Drag: paint a course",
        _ => $"Take down {marked.Count} block{(marked.Count == 1 ? "" : "s")} (full refund)",
    };
}
