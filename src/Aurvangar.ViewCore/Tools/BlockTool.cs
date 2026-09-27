using System.Numerics;
using Aurvangar.Sim;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Tools;

/// <summary>One cell of the block tool's ghost and its CON-08 verdict (red when not <see cref="Ok"/>). A valid cell is
/// <see cref="Short"/> (amber, M10-T2) when the free stock does not cover it; it is still sent.</summary>
public readonly record struct GhostCell(Int3 Cell, PlanResult Result, bool Short = false)
{
    public bool Ok => Result == PlanResult.Ok;
}

/// <summary>The ghost's material (M10-T2): the cost item, its count per block, and the free units
/// (<see cref="FreeStock"/>) the drag is compared with.</summary>
public readonly record struct GhostCost(ItemId Item, int PerBlock, int Free);

/// <summary>The block tool's ghost (VIEW-21): the block, the plan flag, and each painted cell with its verdict.
/// <see cref="Cost"/> is null when the ghost was made without stock or the block costs nothing.</summary>
public sealed record BlockGhost(BlockId Block, bool Plan, IReadOnlyList<GhostCell> Cells, GhostCost? Cost = null)
{
    public int ValidCount => Cells.Count(c => c.Ok);

    /// <summary>Valid cells beyond the free stock.</summary>
    public int ShortCount => Cells.Count(c => c.Short);

    /// <summary>The inclusive cell box around every ghost cell (the mouse label keeps clear of it on screen, M9-T3).
    /// Needs at least one cell.</summary>
    public (Int3 Min, Int3 Max) Bounds() => (
        new Int3(Cells.Min(c => c.Cell.X), Cells.Min(c => c.Cell.Y), Cells.Min(c => c.Cell.Z)),
        new Int3(Cells.Max(c => c.Cell.X), Cells.Max(c => c.Cell.Y), Cells.Max(c => c.Cell.Z)));
}

/// <summary>What a block-tool release did: the commands to enqueue in order (empty when nothing is sent) and a message
/// for the player.</summary>
public readonly record struct BlockClick(IReadOnlyList<DesignateBuild> Commands, string? Message);

/// <summary>Block tool state (VIEW-21; single blocks since M9-T1, ADR-067, which amends ADR-065). Engine-neutral: the
/// Godot layer feeds it picks, the mouse ray and keys, draws <see cref="Ghost"/> and enqueues what
/// <see cref="Release"/> returns.
/// <list type="bullet">
/// <item>A click plans or places one block in the cell the picked face looks into (<see cref="Anchor"/>): a top face
/// stacks upward, a side face places beside.</item>
/// <item>A drag paints one block into each new cell the cursor passes over (<see cref="PaintDrag"/>). From a top or
/// bottom face it stays on the layer of the first cell (a course); from a side face it stays in that face's vertical
/// plane (a wall face, M10-T3).</item>
/// <item>The release sends one <c>DesignateBuild(Single)</c> per valid cell, bottom-up and then in support order
/// (<see cref="SendOrder"/>), so every cell is supported when its command is applied.</item>
/// <item>The ghost's verdicts come from one batched sim query (<see cref="BlockPlans.CanPlanAll"/>) with the painted
/// cells as pending, recomputed when the cells change or every <see cref="GhostRecheckTicks"/> ticks.</item>
/// <item>The tool stays active after a release; Esc leaves it and a right click drops the drag.</item>
/// </list></summary>
public sealed class BlockTool
{
    public const int GhostRecheckTicks = 5;

    private readonly List<BlockId> _blocks = new();
    private PaintDrag? _drag;
    private BlockGhost? _cached;
    private FreeStock? _cachedStock;
    private (int Version, Int3? Hover, BlockId Block, bool Plan) _cachedKey;
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
    public bool Plan { get; private set; }
    public bool Dragging => _drag is not null;

    /// <summary>The drag in progress paints a vertical plane (it started on a side face, M10-T3).</summary>
    public bool Vertical => _drag?.Vertical ?? false;

    /// <summary>The cells painted by the drag in progress (empty when not dragging).</summary>
    public IReadOnlyList<Int3> Painted => _drag?.Cells ?? (IReadOnlyList<Int3>)Array.Empty<Int3>();

    public void Select(BlockId block)
    {
        if (_blocks.Contains(block)) Block = block;
    }

    /// <summary>P: plan mode (the commands are sent with <c>Plan = true</c>).</summary>
    public void TogglePlan() => Plan = !Plan;

    /// <summary>Tool switched: drops the drag.</summary>
    public void Reset() => AbortDrag();

    public void AbortDrag() => _drag = null;

    /// <summary>The cell a pick places into: the air cell the picked face looks into.</summary>
    public static Int3 Anchor(PickHit hit) => hit.Adjacent;

    /// <summary>Left button down over a pick: paints its cell and starts a drag, on that cell's layer for a top or
    /// bottom face and in the face's vertical plane for a side face (M10-T3).</summary>
    public void Press(PickHit? hit)
    {
        if (hit is not { } h) return;
        _drag = new PaintDrag(h, Anchor(h), vertical: true);
    }

    /// <summary>The cursor ray moved during a drag (the preferred input: it stays in the drag's plane). Falls back to
    /// <paramref name="hover"/> when the ray misses the plane.</summary>
    public void MoveRay(Vector3 origin, Vector3 direction, PickHit? hover)
    {
        if (_drag is null) return;
        if (_drag.OnPlane(origin, direction) is { } cell) _drag.MoveTo(cell);
        else Move(hover);
    }

    /// <summary>A pick moved during a drag: paints toward the cell it looks into, kept in the drag's plane. A null pick
    /// keeps the drag as it is.</summary>
    public void Move(PickHit? hit)
    {
        if (_drag is not null && hit is { } h) _drag.MoveTo(Anchor(h));
    }

    /// <summary>Paints toward <paramref name="cell"/> (its coordinate across the drag's plane is ignored). For scripts
    /// and the screenshot harness.</summary>
    public void DragTo(Int3 cell) => _drag?.MoveTo(cell);

    /// <summary>The ghost cells: the painted cells during a drag, else the one cell a click on <paramref name="hover"/>
    /// would place; empty over nothing.</summary>
    public IReadOnlyList<Int3> Cells(PickHit? hover)
    {
        if (_drag is not null) return _drag.Cells;
        return hover is { } h ? new[] { Anchor(h) } : Array.Empty<Int3>();
    }

    /// <summary>The ghost of <see cref="Cells"/> with each cell's verdict (null over nothing), cached while the cells,
    /// block and mode are unchanged and fewer than <see cref="GhostRecheckTicks"/> ticks have passed.</summary>
    public BlockGhost? Ghost(Simulation sim, PickHit? hover, FreeStock? stock = null)
    {
        var key = (_drag?.Version ?? -1, _drag is null && hover is { } h ? Anchor(h) : (Int3?)null, Block, Plan);
        if (_drag is null && hover is null) return null;
        long tick = sim.Clock.Tick;
        if (_cached is not null && _cachedKey == key && ReferenceEquals(_cachedStock, stock)
            && tick - _cachedTick < GhostRecheckTicks && tick >= _cachedTick) return _cached;
        _cached = GhostFor(sim, Block, Plan, Cells(hover), stock);
        _cachedKey = key;
        _cachedStock = stock;
        _cachedTick = tick;
        return _cached;
    }

    /// <summary>The ghost of a set of cells: one batched CON-08 query, with the cells as each other's pending set.
    /// With <paramref name="stock"/> (M10-T2), the valid cells take the free units of the block's cost item bottom-up
    /// and then in paint order (<see cref="BottomUp"/>, the order they are sent in, M10-T3); the valid cells after the
    /// stock runs out are <see cref="GhostCell.Short"/>, so on a wall face the top cells are short. Invalid cells take
    /// nothing.</summary>
    public static BlockGhost GhostFor(Simulation sim, BlockId block, bool plan, IReadOnlyList<Int3> cells, FreeStock? stock = null)
    {
        var results = BlockPlans.CanPlanAll(sim, cells);
        var (item, perBlock) = sim.Content.CostOf(block);
        GhostCost? cost = stock is not null && perBlock > 0 ? new GhostCost(item, perBlock, stock.Of(item, plan)) : null;
        int affordable = cost is { } gc ? gc.Free / gc.PerBlock : int.MaxValue;
        var list = new GhostCell[cells.Count];
        int valid = 0;
        foreach (int k in BottomUp(cells))
        {
            bool ok = results[k] == PlanResult.Ok;
            list[k] = new GhostCell(cells[k], results[k], ok && valid >= affordable);
            if (ok) valid++;
        }
        return new BlockGhost(block, plan, list, cost);
    }

    /// <summary>Left button up: ends the drag and returns one <c>DesignateBuild(Single)</c> per valid painted cell, in
    /// <see cref="SendOrder"/>. Nothing is sent when no cell is valid (the message says why).</summary>
    public BlockClick Release(Simulation sim)
    {
        if (_drag is not { } drag) return new BlockClick(Array.Empty<DesignateBuild>(), null);
        var ghost = GhostFor(sim, Block, Plan, drag.Cells);
        AbortDrag();
        var valid = ghost.Cells.Where(c => c.Ok).Select(c => c.Cell).ToList();
        if (valid.Count == 0)
            return new BlockClick(Array.Empty<DesignateBuild>(),
                $"Can't build here: {ReasonText(ghost.Cells.FirstOrDefault().Result) ?? "nothing to build"}");
        var commands = SendOrder(sim, valid)
            .Select(c => new DesignateBuild(BuildShape.Single, c, c, 1, Block, Plan))
            .ToList();
        return new BlockClick(commands, null);
    }

    /// <summary>The indices of <paramref name="cells"/> from the lowest layer up, in paint order within a layer (a
    /// stable sort; a horizontal drag keeps its paint order).</summary>
    public static IEnumerable<int> BottomUp(IReadOnlyList<Int3> cells) =>
        Enumerable.Range(0, cells.Count).OrderBy(k => cells[k].Y);

    /// <summary>The order a release sends cells in (M10-T3): bottom-up (<see cref="BottomUp"/>), then
    /// <see cref="SupportOrder"/>, so a wall face painted from the top down is still sent from the bottom.</summary>
    public static IReadOnlyList<Int3> SendOrder(Simulation sim, IReadOnlyList<Int3> cells) =>
        SupportOrder(sim, BottomUp(cells).Select(k => cells[k]).ToList());

    /// <summary>The cells in an order in which each one has a supporting neighbour (CON-09: below or beside) that is
    /// solid, has a plan entry, or comes earlier in the order. Cells are taken in paint order, pass after pass; a cell
    /// that never gets such a neighbour goes last (the sim then rejects it). Read-only.</summary>
    public static IReadOnlyList<Int3> SupportOrder(Simulation sim, IReadOnlyList<Int3> cells)
    {
        var order = new List<Int3>(cells.Count);
        var placed = new HashSet<Int3>();
        var left = new List<Int3>(cells);
        while (left.Count > 0)
        {
            var next = new List<Int3>();
            foreach (var c in left)
            {
                if (HasSupport(sim, c, placed)) { order.Add(c); placed.Add(c); }
                else next.Add(c);
            }
            if (next.Count == left.Count) { order.AddRange(next); break; }
            left = next;
        }
        return order;
    }

    private static readonly Int3[] SupportSteps = { Int3.Down, Int3.East, Int3.West, Int3.North, Int3.South };

    private static bool HasSupport(Simulation sim, Int3 c, HashSet<Int3> placed)
    {
        foreach (var d in SupportSteps)
        {
            var n = c + d;
            if (placed.Contains(n)) return true;
            if (!sim.World.InBounds(n)) continue;
            if (sim.World.IsSolid(n) || sim.Plans.Get(n) is not null) return true;
        }
        return false;
    }

    /// <summary>Mouse label: mode, block, count and cost; then the reason of the first red cell and how many are red;
    /// then the controls.</summary>
    public static string Tooltip(Simulation sim, BlockGhost g)
    {
        string label = sim.Content.LabelOf(g.Block);
        string mode = g.Plan ? "Plan" : "Build";
        int valid = g.ValidCount;
        var (item, cost) = sim.Content.CostOf(g.Block);
        string costText = cost > 0 ? $", {valid * cost} {sim.Content.ItemDef(item).Name.ToLowerInvariant()}" : "";
        var lines = new List<string> { $"{mode} {label} ({valid} block{(valid == 1 ? "" : "s")}{costText})" };
        if (StockText(sim, g) is { } stockText) lines.Add(stockText);
        int bad = g.Cells.Count - valid;
        if (bad > 0)
        {
            var first = g.Cells.First(c => !c.Ok);
            lines.Add(bad == 1 ? ReasonText(first.Result)! : $"{ReasonText(first.Result)} ({bad} cells skipped)");
        }
        lines.Add(ControlsHint);
        return string.Join("\n", lines);
    }

    /// <summary>M10-T2: the drag's cost against free stock ("40 stone free", "Only 3 stone free: 2 blocks short
    /// (amber)"; "... free after the plan" in plan mode); null without <see cref="BlockGhost.Cost"/>.</summary>
    public static string? StockText(Simulation sim, BlockGhost g)
    {
        if (g.Cost is not { } c) return null;
        string item = sim.Content.ItemDef(c.Item).Name.ToLowerInvariant();
        string after = g.Plan ? " after the plan" : "";
        int shortBlocks = g.ShortCount;
        if (shortBlocks == 0) return $"{c.Free} {item} free{after}";
        string head = c.Free == 0 ? $"No {item} free{after}" : $"Only {c.Free} {item} free{after}";
        return $"{head}: {shortBlocks} block{(shortBlocks == 1 ? "" : "s")} short (amber)";
    }

    /// <summary>The controls line of the tooltip.</summary>
    public const string ControlsHint = "Click a face: one block. Drag from a top face: a course; from a side face: a wall. P plan";

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
