using Aurvangar.Sim;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Tools;

/// <summary>One cell of the block tool's ghost and its CON-08 verdict (red when not <see cref="Ok"/>).</summary>
public readonly record struct GhostCell(Int3 Cell, PlanResult Result)
{
    public bool Ok => Result == PlanResult.Ok;
}

/// <summary>The block tool's ghost (VIEW-21): the command a release would send and each cell's verdict. A shape over
/// <see cref="BuildShapes.MaxCells"/> has no cells and <see cref="TooLarge"/> set.</summary>
public sealed record BlockGhost(DesignateBuild Command, IReadOnlyList<GhostCell> Cells, bool TooLarge)
{
    public int ValidCount => Cells.Count(c => c.Ok);
}

/// <summary>What a block-tool release did: the command to enqueue (null when nothing is sent), whether the tool stays
/// active (Shift), and a message for the player.</summary>
public readonly record struct BlockClick(DesignateBuild? Command, bool KeepTool, string? Message);

/// <summary>Block tool state (VIEW-21, M8-T5, ADR-065). Engine-neutral: the Godot layer feeds it picks and keys, draws
/// <see cref="Ghost"/> and enqueues the command <see cref="Release"/> returns.
/// <list type="bullet">
/// <item>The anchor <c>A</c> is the air cell on the picked face (<see cref="PickHit.Adjacent"/>); the drag's end sets
/// <c>B</c> on the same level. Without a drag the ghost is the shape at the hovered cell.</item>
/// <item>Height (Wall, Box) starts at <see cref="StartHeight"/> and +/- change it; the other shapes send 1.</item>
/// <item>The ghost's verdicts come from one batched sim query per changed command (<see cref="BlockPlans.CanPlanAll"/>),
/// recomputed at most every <see cref="GhostRecheckTicks"/> ticks while it stays the same.</item>
/// </list></summary>
public sealed class BlockTool
{
    public const int DefaultHeight = 3;
    public const int GhostRecheckTicks = 5;

    /// <summary>Shape modes in toolbar and Tab order.</summary>
    public static readonly IReadOnlyList<BuildShape> Shapes = new[]
        { BuildShape.Single, BuildShape.Line, BuildShape.Wall, BuildShape.Floor, BuildShape.HollowBox, BuildShape.Stair };

    private readonly List<BlockId> _blocks = new();
    private PickHit? _start;
    private PickHit? _end;
    private int? _height;
    private BlockGhost? _cached;
    private long _cachedTick = long.MinValue;

    public BlockTool(ContentDb content)
    {
        foreach (BlockId id in Enum.GetValues<BlockId>())
            if (content.IsConstruction(id)) _blocks.Add(id);
        if (_blocks.Count == 0) throw new InvalidOperationException("BlockTool: no construction blocks");
        Block = _blocks[0];
    }

    /// <summary>The construction blocks (CON-01) in id order; the picker lists them by label.</summary>
    public IReadOnlyList<BlockId> Blocks => _blocks;

    public BlockId Block { get; private set; }
    public BuildShape Shape { get; private set; } = BuildShape.Wall;
    public bool Plan { get; private set; }
    public bool Dragging => _start.HasValue;

    public void Select(BlockId block)
    {
        if (_blocks.Contains(block)) Block = block;
    }

    public void SetShape(BuildShape shape) => Shape = shape;

    /// <summary>Tab: the next shape mode, wrapping around.</summary>
    public void CycleShape()
    {
        int i = 0;
        for (int k = 0; k < Shapes.Count; k++) if (Shapes[k] == Shape) i = k;
        Shape = Shapes[(i + 1) % Shapes.Count];
    }

    /// <summary>P: plan mode (the command is sent with <c>Plan = true</c>).</summary>
    public void TogglePlan() => Plan = !Plan;

    /// <summary>Tool switched: drops the drag and any height set with +/-.</summary>
    public void Reset()
    {
        AbortDrag();
        _height = null;
    }

    public void AbortDrag() { _start = null; _end = null; }

    /// <summary>VIEW-21: <c>SliceY - A.Y + 1</c> when the slice is active (<c>SliceY &lt; SizeY - 1</c>), else
    /// <see cref="DefaultHeight"/>; clamped to 1..32.</summary>
    public static int StartHeight(int anchorY, int sliceY, int sizeY) =>
        sliceY < sizeY - 1 ? Math.Clamp(sliceY - anchorY + 1, BuildShapes.MinHeight, BuildShapes.MaxHeight) : DefaultHeight;

    /// <summary>The Wall/Box height: set by +/- since the last <see cref="Reset"/>, else <see cref="StartHeight"/>.</summary>
    public int Height(int anchorY, int sliceY, int sizeY) => _height ?? StartHeight(anchorY, sliceY, sizeY);

    /// <summary>+/- (or Ctrl + wheel): the height by <paramref name="delta"/>, clamped to 1..32.</summary>
    public void AdjustHeight(int delta, int anchorY, int sliceY, int sizeY) =>
        _height = Math.Clamp(Height(anchorY, sliceY, sizeY) + delta, BuildShapes.MinHeight, BuildShapes.MaxHeight);

    /// <summary>The anchor of a pick: the air cell the picked face looks into.</summary>
    public static Int3 Anchor(PickHit hit) => hit.Adjacent;

    /// <summary>The anchor the height is measured from: the drag start, else the hovered cell.</summary>
    public Int3? CurrentAnchor(PickHit? hover) => (_start ?? hover) is { } h ? Anchor(h) : null;

    /// <summary>Left button down over a pick: starts a drag.</summary>
    public void Press(PickHit? hit)
    {
        if (hit is null) return;
        _start = hit;
        _end = hit;
    }

    /// <summary>Mouse moved; a null pick keeps the last end.</summary>
    public void Move(PickHit? hit)
    {
        if (Dragging && hit is not null) _end = hit;
    }

    /// <summary>The command for the drag, or for the hovered cell when not dragging; null over nothing.</summary>
    public DesignateBuild? Command(PickHit? hover, int sliceY, int sizeY)
    {
        var first = _start ?? hover;
        var second = _start is null ? hover : _end;
        if (first is not { } f || second is not { } s) return null;
        var a = Anchor(f);
        var b = Anchor(s) with { Y = a.Y };
        int height = BuildShapes.UsesHeight(Shape) ? Height(a.Y, sliceY, sizeY) : 1;
        return new DesignateBuild(Shape, a, b, height, Block, Plan);
    }

    /// <summary>The ghost of <see cref="Command"/> with each cell's verdict, cached while the command is unchanged and
    /// fewer than <see cref="GhostRecheckTicks"/> ticks have passed.</summary>
    public BlockGhost? Ghost(Simulation sim, PickHit? hover, int sliceY)
    {
        if (Command(hover, sliceY, sim.World.SizeY) is not { } cmd) return null;
        long tick = sim.Clock.Tick;
        if (_cached is { } c && c.Command == cmd && tick - _cachedTick < GhostRecheckTicks && tick >= _cachedTick) return c;
        _cached = GhostFor(sim, cmd);
        _cachedTick = tick;
        return _cached;
    }

    /// <summary>The ghost of a command: <see cref="BuildShapes.Cells"/> with one batched CON-08 query.</summary>
    public static BlockGhost GhostFor(Simulation sim, DesignateBuild cmd)
    {
        if (BuildShapes.Count(cmd.Shape, cmd.A, cmd.B, cmd.Height) > BuildShapes.MaxCells)
            return new BlockGhost(cmd, Array.Empty<GhostCell>(), true);
        var cells = BuildShapes.Cells(cmd.Shape, cmd.A, cmd.B, cmd.Height);
        var results = BlockPlans.CanPlanAll(sim, cells);
        var list = new GhostCell[cells.Count];
        for (int k = 0; k < cells.Count; k++) list[k] = new GhostCell(cells[k], results[k]);
        return new BlockGhost(cmd, list, false);
    }

    /// <summary>Left button up: ends the drag and returns its command. Nothing is sent when no cell is valid (the
    /// message says why). The tool stays active with <paramref name="shift"/>.</summary>
    public BlockClick Release(Simulation sim, PickHit? hit, int sliceY, bool shift)
    {
        if (!Dragging) return new BlockClick(null, true, null);
        Move(hit);
        var ghost = Ghost(sim, null, sliceY);
        AbortDrag();
        if (ghost is null) return new BlockClick(null, true, null);
        if (ghost.TooLarge) return new BlockClick(null, true, $"Too large: at most {BuildShapes.MaxCells} blocks");
        if (ghost.ValidCount == 0)
            return new BlockClick(null, true, $"Can't build here: {ReasonText(ghost.Cells.FirstOrDefault().Result) ?? "nothing to build"}");
        return new BlockClick(ghost.Command, shift, null);
    }

    /// <summary>Mouse label: block, shape, size, cost and mode; then the reason of the first red cell and how many are
    /// red; then the controls.</summary>
    public static string Tooltip(Simulation sim, BlockGhost g)
    {
        var cmd = g.Command;
        string label = sim.Content.LabelOf(cmd.Block);
        string mode = cmd.Plan ? "Plan" : "Build";
        string shape = ShapeName(cmd.Shape) + (BuildShapes.UsesHeight(cmd.Shape) ? $", height {cmd.Height}" : "");
        if (g.TooLarge) return $"{mode} {label}: {shape}\nToo large: at most {BuildShapes.MaxCells} blocks";
        int valid = g.ValidCount;
        var (item, cost) = sim.Content.CostOf(cmd.Block);
        string costText = cost > 0 ? $", {valid * cost} {sim.Content.ItemDef(item).Name.ToLowerInvariant()}" : "";
        var lines = new List<string> { $"{mode} {label}: {shape} ({valid} block{(valid == 1 ? "" : "s")}{costText})" };
        int bad = g.Cells.Count - valid;
        if (bad > 0)
        {
            var first = g.Cells.First(c => !c.Ok);
            lines.Add(bad == 1 ? ReasonText(first.Result)! : $"{ReasonText(first.Result)} ({bad} cells skipped)");
        }
        lines.Add("Tab shape, +/- height, P plan, Shift keeps the tool");
        return string.Join("\n", lines);
    }

    public static string ShapeName(BuildShape shape) => shape switch
    {
        BuildShape.HollowBox => "Box",
        _ => shape.ToString(),
    };

    /// <summary>Player-facing text for a CON-08 result (null for Ok).</summary>
    public static string? ReasonText(PlanResult r) => r switch
    {
        PlanResult.Ok => null,
        PlanResult.OutOfWorld => "Outside the map",
        PlanResult.Solid => "Something solid is there",
        PlanResult.Building => "A building or its entrance is there",
        PlanResult.Plant => "A plant is in the way",
        PlanResult.Farm => "Farmland below",
        PlanResult.Unsupported => "Needs support below or beside",
        _ => r.ToString(),
    };
}
