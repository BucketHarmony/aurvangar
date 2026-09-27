using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Tools;
using Godot;

namespace Aurvangar.Client;

/// <summary>Block construction in <see cref="GameRoot"/> (VIEW-21..23; M8-T5, single blocks since M9-T1): the block tool
/// (K) that paints one block per cell with its ghost and plan mode (P); the Deconstruct tool's per-block marks; the
/// Release tool (L) and "Release all"; plan ghosts; the plan's material line in the top bar. The rules are
/// <see cref="BlockTool"/>, <see cref="DeconstructPaint"/>, <see cref="PaintDrag"/>, <see cref="PlanGhostMesher"/> and
/// <see cref="TopBarModel.PlanText"/> (ViewCore); this file forwards input and enqueues the commands.</summary>
public partial class GameRoot
{
    /// <summary>CON-06: the top bar's plan line is rebuilt at most this often (frames).</summary>
    public const int PlanTextFrames = 10;

    private static readonly Color ReleaseMark = new(0.4f, 0.8f, 1f, 0.3f);

    private BlockTool _blocks = null!;
    private readonly DeconstructPaint _deconPaint = new();
    private BlockColors _blockColors = null!;
    private TranslucentMesh _blockGhostView = null!;
    private TranslucentMesh _blockBadView = null!;
    private object? _shownGhost;
    private IReadOnlyList<Int3> _deconMarked = System.Array.Empty<Int3>();
    private int _planTextFrame;
    private (Int3 Cell, long Tick, string? Text) _planHover = (new Int3(-1, -1, -1), -1, null);

    /// <summary>Plan entry ghosts (VIEW-22); recreated with the simulation.</summary>
    public PlanRenderer PlanView { get; private set; } = null!;

    /// <summary>The block tool's ghost under the mouse or of the drag (null when the tool is off or over nothing).</summary>
    public BlockGhost? BlockGhost { get; private set; }

    private void ReadyBlockTools()
    {
        _blocks = new BlockTool(Content);
        _blockColors = new BlockColors(Content);
        _blockGhostView = new TranslucentMesh { Name = "BlockGhost" };
        AddChild(_blockGhostView);
        _blockBadView = new TranslucentMesh { Name = "BlockGhostInvalid", XRay = true };
        AddChild(_blockBadView);
    }

    private void WireBlockHud()
    {
        _hud.BlockChosen += block => { _blocks.Select(block); SetTool(ToolKind.Blocks); };
        _hud.PlanToggled += () => { _blocks.TogglePlan(); SyncBlockHud(); };
        _hud.ReleaseAllPressed += () => Sim.Enqueue(ToolController.ReleaseAll(Sim.World));
    }

    private IReadOnlyList<(BlockId, string)> BlockTypes() =>
        _blocks.Blocks.Select(b => (b, Content.LabelOf(b))).ToList();

    /// <summary>Keys of the block tool (P: plan mode); true when the key was used.</summary>
    private bool BlockKey(InputEventKey key)
    {
        if (_tool.Tool != ToolKind.Blocks || key.Echo || key.Keycode != Key.P) return false;
        _blocks.TogglePlan();
        SyncBlockHud();
        return true;
    }

    /// <summary>A right click or a tool change: drop the paint drags.</summary>
    private void AbortPaint()
    {
        _blocks.AbortDrag();
        _deconPaint.Abort();
    }

    private void BlockRelease()
    {
        MovePaint();
        var click = _blocks.Release(Sim);
        foreach (var c in click.Commands) Sim.Enqueue(c);
        if (click.Message != null) _hud.Toast(click.Message);
    }

    /// <summary>Deconstruct over no building: the left button went down on a pick (M9-T1).</summary>
    private void DeconstructPaintStart(PickHit hit) => _deconPaint.Start(hit);

    private void DeconstructPaintRelease()
    {
        if (!_deconPaint.Dragging) return;
        MovePaint();
        var (commands, message) = _deconPaint.Release(Sim);
        foreach (var c in commands) Sim.Enqueue(c);
        if (message != null) _hud.Toast(message);
    }

    /// <summary>Moves the paint drags to the cell under the mouse: the mouse ray cut with the drag's layer plane, or
    /// the hover pick when there is no ray (the screenshot harness).</summary>
    private void MovePaint()
    {
        if (!_blocks.Dragging && !_deconPaint.Dragging) return;
        if (MouseRay() is var (origin, dir))
        {
            _blocks.MoveRay(origin, dir, Hover);
            _deconPaint.MoveRay(origin, dir, Hover);
        }
        else
        {
            _blocks.Move(Hover);
            _deconPaint.Move(Hover);
        }
    }

    /// <summary>The camera ray through the mouse, or null with picking off.</summary>
    private (System.Numerics.Vector3 Origin, System.Numerics.Vector3 Direction)? MouseRay()
    {
        if (!PickingEnabled || GetViewport().GetCamera3D() is not { } camera) return null;
        var mouse = GetViewport().GetMousePosition();
        return (CameraRig.ToNumerics(camera.ProjectRayOrigin(mouse)), CameraRig.ToNumerics(camera.ProjectRayNormal(mouse)));
    }

    /// <summary>Physics frame with the block tool: paint along the drag, then the ghost (re-uploaded only when it
    /// changed).</summary>
    private void UpdateBlockPreview()
    {
        _toolPreview.Visible = false;
        MovePaint();
        BlockGhost = _blocks.Ghost(Sim, Hover);
        ShowBlockGhost(BlockGhost);
    }

    /// <summary>Physics frame with the Deconstruct tool over no building: orange marks on the built blocks a release
    /// would take down.</summary>
    private void UpdateDeconstructMarks()
    {
        MovePaint();
        var marked = _deconPaint.Marked(Sim, Hover);
        if (!marked.SequenceEqual(_deconMarked)) _shownGhost = null;
        _deconMarked = marked;
        ShowMarks(marked);
    }

    private void ShowBlockGhost(BlockGhost? ghost)
    {
        if (ReferenceEquals(ghost, _shownGhost)) return;
        _shownGhost = ghost;
        _blockGhostView.SetData(ghost == null ? null : BlockGhostMesher.Build(ghost, _blockColors, _entityColors));
        _blockBadView.SetData(ghost == null ? null : BlockGhostMesher.BuildInvalid(ghost, _entityColors));
    }

    private void ShowMarks(IReadOnlyList<Int3> marked)
    {
        if (ReferenceEquals(_shownGhost, _deconPaint)) return;
        _shownGhost = _deconPaint;
        _blockGhostView.SetData(marked.Count == 0 ? null : BlockGhostMesher.Marks(marked, BlockGhostMesher.DeconstructColor));
        _blockBadView.SetData(null);
    }

    /// <summary>The block tool's and Release tool's mouse labels, and a hovered plan entry's status (VIEW-22).</summary>
    private string? BlockTooltip()
    {
        if (_tool.Tool == ToolKind.Blocks && BlockGhost is { } g) return BlockTool.Tooltip(Sim, g);
        if (_tool.Tool == ToolKind.Release && Hover is not null)
            return _tool.Dragging ? "Release the planned blocks in these columns (up to the view level)" : "Drag over planned blocks to release them";
        return PlanHoverText();
    }

    /// <summary>VIEW-22 hover text, recomputed when the hovered cell or the tick changes (one status query).</summary>
    private string? PlanHoverText()
    {
        if (Hover is not { } h || Sim.Plans.Count == 0) return null;
        var cell = h.Adjacent;
        if (_planHover.Cell != cell || _planHover.Tick != Sim.Clock.Tick)
            _planHover = (cell, Sim.Clock.Tick, PlanGhostMesher.HoverText(Sim, h, SliceY));
        return _planHover.Text;
    }

    /// <summary>The block options row and the Blocks button follow the tool state.</summary>
    private void SyncBlockHud() => _hud.SetBlockOptions(_tool.Tool, Content.LabelOf(_blocks.Block), _blocks.Plan);

    /// <summary>Screenshot harness (M9-T1): the block tool with a paint drag held from <paramref name="start"/> along
    /// the cursor cells <paramref name="path"/> (picking is off; a pick on the last cell becomes the pick override, for
    /// the mouse label).</summary>
    public void ShowBlockPaint(PickHit start, IReadOnlyList<Int3> path, BlockId block)
    {
        SetTool(ToolKind.Blocks);
        _blocks.Select(block);
        _blocks.Press(start);
        foreach (var c in path) _blocks.DragTo(c);
        var last = _blocks.Painted[^1];
        PickOverride = new PickHit(last + Int3.Down, Int3.Up);
        SyncBlockHud();
    }

    /// <summary>VIEW-23: the plan line, every <see cref="PlanTextFrames"/> frames.</summary>
    private void UpdatePlanText(bool now)
    {
        if (!now && ++_planTextFrame < PlanTextFrames) return;
        _planTextFrame = 0;
        _hud.TopBar.ShowPlan(TopBarModel.PlanText(Sim));
    }

    /// <summary>M9-T3: the block tool ghost's box on screen (its 8 projected corners), which the mouse label keeps clear
    /// of; null with no ghost, another tool, or a corner behind the camera.</summary>
    private ScreenRect? GhostScreenRect(Camera3D camera)
    {
        if (_tool.Tool != ToolKind.Blocks || BlockGhost is not { Cells.Count: > 0 } g) return null;
        var (min, max) = g.Bounds();
        var corners = new List<System.Numerics.Vector2>(8);
        for (int i = 0; i < 8; i++)
        {
            var p = new Vector3((i & 1) == 0 ? min.X : max.X + 1, (i & 2) == 0 ? min.Y : max.Y + 1, (i & 4) == 0 ? min.Z : max.Z + 1);
            if (camera.IsPositionBehind(p)) return null;
            var s = camera.UnprojectPosition(p);
            corners.Add(new System.Numerics.Vector2(s.X, s.Y));
        }
        return ScreenRect.Bounding(corners);
    }
}
