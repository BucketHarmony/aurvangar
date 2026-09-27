using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Tools;

/// <summary>Player tools (VIEW-12). Dig, Chop, Farm (M6-T5), Cancel and Release (M8-T5) are drag tools (this class);
/// Build is a click tool (<see cref="BuildTool"/>); Deconstruct clicks a building or, over no building, drags a box of
/// built blocks (<see cref="DeconstructTool"/>, M8-T5); Blocks is the block tool (<see cref="BlockTool"/>, VIEW-21).</summary>
public enum ToolKind : byte { Select, Dig, Chop, Cancel, Build, Deconstruct, Farm, Blocks, Release }

/// <summary>Tool state and drag boxes (VIEW-12, VIEW-13, ADR-033). Engine-neutral: the Godot layer feeds it mouse
/// presses, picks and key presses, draws <see cref="PreviewBox"/>, and enqueues the command a release returns.
/// A tool stays active after a drag; Esc (<see cref="ToolKind.Select"/>) returns to Select. The click tools (Build,
/// Deconstruct) never start a drag here.</summary>
public sealed class ToolController
{
    public ToolKind Tool { get; private set; } = ToolKind.Select;
    public bool Dragging => _start.HasValue;

    private PickHit? _start;
    private PickHit? _end;

    /// <summary>VIEW-12 hotkeys: G dig, C chop, F farm, Z cancel, B build, X deconstruct, K blocks, L release plan
    /// (VIEW-21). Null for any other key.</summary>
    public static ToolKind? ForHotkey(char key) => char.ToUpperInvariant(key) switch
    {
        'G' => ToolKind.Dig,
        'C' => ToolKind.Chop,
        'F' => ToolKind.Farm,
        'Z' => ToolKind.Cancel,
        'B' => ToolKind.Build,
        'X' => ToolKind.Deconstruct,
        'K' => ToolKind.Blocks,
        'L' => ToolKind.Release,
        _ => null,
    };

    /// <summary>Dig, Chop, Farm, Cancel and Release define a box by dragging.</summary>
    public static bool IsDragTool(ToolKind tool) =>
        tool is ToolKind.Dig or ToolKind.Chop or ToolKind.Farm or ToolKind.Cancel or ToolKind.Release;

    /// <summary>Switches tool; any drag in progress is dropped without a command.</summary>
    public void SetTool(ToolKind tool)
    {
        Tool = tool;
        AbortDrag();
    }

    public void AbortDrag() { _start = null; _end = null; }

    /// <summary>Left button down. Starts a drag when a tool is active and the mouse is over a pickable cell.</summary>
    public void Press(PickHit? hit)
    {
        if (!IsDragTool(Tool) || hit is null) return;
        _start = hit;
        _end = hit;
    }

    /// <summary>Starts a drag for any tool but Select, when the caller decides (Deconstruct over no building,
    /// <see cref="DeconstructTool.Press"/>).</summary>
    public void BeginDrag(PickHit hit)
    {
        if (Tool == ToolKind.Select) return;
        _start = hit;
        _end = hit;
    }

    /// <summary>Mouse moved during a drag. A null pick (over the sky or above the slice) keeps the last end.</summary>
    public void Move(PickHit? hit)
    {
        if (Dragging && hit is not null) _end = hit;
    }

    /// <summary>Left button up: ends the drag and returns its command, or null when no drag was running.</summary>
    public ICommand? Release(PickHit? hit, int sliceY)
    {
        if (_start is not { } start) return null;
        Move(hit);
        var end = _end ?? start;
        AbortDrag();
        return CommandFor(Tool, start, end, sliceY);
    }

    /// <summary>The cell box the current drag would cover (inclusive corners, min first), for the preview.</summary>
    public (Int3 Min, Int3 Max)? PreviewBox(int sliceY) =>
        _start is { } s && _end is { } e ? BoxFor(Tool, s, e, sliceY) : null;

    /// <summary>The command a drag from <paramref name="first"/> to <paramref name="second"/> sends (null for Select).
    /// <list type="bullet">
    /// <item>Dig (VIEW-13): the box from the first picked cell to the second, whose height is clamped to the slice
    /// level, so a drag on a sliced layer digs that layer.</item>
    /// <item>Chop (DSG-05): the XZ rectangle of the two picks.</item>
    /// <item>Farm (ECO-11): the XZ rectangle of the two picks; the sim takes each column's top surface.</item>
    /// <item>Cancel (DSG-06): the dig box raised one cell at the top, so trees standing on the dragged ground (their
    /// base is the cell above the picked block) are included.</item>
    /// <item>Release (VIEW-21) and Deconstruct over no building (CON-18): the columns under the drag from the lower
    /// pick up to the slice level (<see cref="ColumnBox"/>, ADR-065).</item>
    /// </list></summary>
    public static ICommand? CommandFor(ToolKind tool, PickHit first, PickHit second, int sliceY)
    {
        var a = first.Cell;
        var b = SecondCorner(second, sliceY);
        return tool switch
        {
            ToolKind.Dig => new DesignateDig(a, b),
            ToolKind.Chop => new DesignateChop(a.X, a.Z, b.X, b.Z),
            ToolKind.Farm => new DesignateFarm(a.X, a.Z, b.X, b.Z),
            ToolKind.Cancel => CancelCommand(a, b),
            ToolKind.Release => new ReleasePlan(ColumnBox(first, second, sliceY).Min, ColumnBox(first, second, sliceY).Max),
            ToolKind.Deconstruct => new DesignateDeconstructBlocks(ColumnBox(first, second, sliceY).Min, ColumnBox(first, second, sliceY).Max),
            _ => null,
        };
    }

    /// <summary>VIEW-21 "Release all": one <c>ReleasePlan</c> over the whole world.</summary>
    public static ReleasePlan ReleaseAll(Aurvangar.Sim.World.VoxelWorld world) =>
        new(new Int3(0, 0, 0), new Int3(world.SizeX - 1, world.SizeY - 1, world.SizeZ - 1));

    /// <summary>Release and Deconstruct-blocks box: the X/Z rectangle of the picks, from the lower picked cell up to
    /// <paramref name="sliceY"/> (planned blocks and built blocks stand above the picked ground; the slice limits a
    /// release to the courses in view).</summary>
    public static (Int3 Min, Int3 Max) ColumnBox(PickHit first, PickHit second, int sliceY)
    {
        var a = first.Cell;
        var b = second.Cell;
        int y0 = Math.Min(a.Y, b.Y);
        return (new Int3(Math.Min(a.X, b.X), y0, Math.Min(a.Z, b.Z)),
                new Int3(Math.Max(a.X, b.X), Math.Max(y0, sliceY), Math.Max(a.Z, b.Z)));
    }

    /// <summary>Inclusive cell box of a drag, min corner first. Chop covers the tree-base layer above the picks.</summary>
    public static (Int3 Min, Int3 Max)? BoxFor(ToolKind tool, PickHit first, PickHit second, int sliceY)
    {
        var a = first.Cell;
        var b = SecondCorner(second, sliceY);
        return tool switch
        {
            ToolKind.Dig or ToolKind.Farm => Sorted(a, b),
            ToolKind.Chop => Sorted(a + Int3.Up, b + Int3.Up),
            ToolKind.Cancel => CancelBox(a, b),
            // The column box reaches the slice; the preview shows its footprint one cell above the lower pick.
            ToolKind.Release or ToolKind.Deconstruct => ColumnPreview(ColumnBox(first, second, sliceY)),
            _ => null,
        };
    }

    private static (Int3 Min, Int3 Max) ColumnPreview((Int3 Min, Int3 Max) box) => (box.Min, box.Max with { Y = box.Min.Y + 1 });

    private static Int3 SecondCorner(PickHit second, int sliceY) =>
        second.Cell with { Y = Math.Min(second.Cell.Y, sliceY) };

    private static CancelDesignation CancelCommand(Int3 a, Int3 b)
    {
        var (min, max) = CancelBox(a, b);
        return new CancelDesignation(min, max);
    }

    private static (Int3 Min, Int3 Max) CancelBox(Int3 a, Int3 b)
    {
        var (min, max) = Sorted(a, b);
        return (min, max + Int3.Up);
    }

    private static (Int3 Min, Int3 Max) Sorted(Int3 a, Int3 b) =>
        (new Int3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)),
         new Int3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));
}
