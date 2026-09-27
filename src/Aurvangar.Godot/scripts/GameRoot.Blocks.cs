using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Tools;
using Godot;

namespace Aurvangar.Client;

/// <summary>Block construction in <see cref="GameRoot"/> (VIEW-21..23, M8-T5): the block tool (K) with its ghost, shape
/// modes (Tab), height (+/-, Ctrl + wheel) and plan mode (P); the Release tool (L) and "Release all"; the Deconstruct
/// tool's block-box drag; plan ghosts; the plan's material line in the top bar. The rules are <see cref="BlockTool"/>,
/// <see cref="ToolController"/>, <see cref="PlanGhostMesher"/> and <see cref="TopBarModel.PlanText"/> (ViewCore); this
/// file forwards input and enqueues the commands.</summary>
public partial class GameRoot
{
    /// <summary>CON-06: the top bar's plan line is rebuilt at most this often (frames).</summary>
    public const int PlanTextFrames = 10;

    private static readonly Color ReleaseMark = new(0.4f, 0.8f, 1f, 0.3f);

    private BlockTool _blocks = null!;
    private BlockColors _blockColors = null!;
    private TranslucentMesh _blockGhostView = null!;
    private BlockGhost? _shownGhost;
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
    }

    private void WireBlockHud()
    {
        _hud.BlockChosen += block => { _blocks.Select(block); SetTool(ToolKind.Blocks); };
        _hud.ShapeChosen += shape => { _blocks.SetShape(shape); SyncBlockHud(); };
        _hud.PlanToggled += () => { _blocks.TogglePlan(); SyncBlockHud(); };
        _hud.ReleaseAllPressed += () => Sim.Enqueue(ToolController.ReleaseAll(Sim.World));
    }

    private IReadOnlyList<(BlockId, string)> BlockTypes() =>
        _blocks.Blocks.Select(b => (b, Content.LabelOf(b))).ToList();

    /// <summary>Keys of the block tool; true when the key was used.</summary>
    private bool BlockKey(InputEventKey key)
    {
        if (_tool.Tool != ToolKind.Blocks) return false;
        switch (key.Keycode)
        {
            case Key.P: _blocks.TogglePlan(); break;
            case Key.Tab: _blocks.CycleShape(); break;
            case Key.Equal or Key.Plus or Key.KpAdd: AdjustBlockHeight(+1); break;
            case Key.Minus or Key.KpSubtract: AdjustBlockHeight(-1); break;
            default: return false;
        }
        SyncBlockHud();
        return true;
    }

    /// <summary>Ctrl + wheel with the block tool: height (the camera ignores a Ctrl + wheel). True when used.</summary>
    private bool BlockWheel(InputEventMouseButton mb)
    {
        if (_tool.Tool != ToolKind.Blocks || !mb.CtrlPressed || !mb.Pressed) return false;
        if (mb.ButtonIndex == MouseButton.WheelUp) AdjustBlockHeight(+1);
        else if (mb.ButtonIndex == MouseButton.WheelDown) AdjustBlockHeight(-1);
        else return false;
        SyncBlockHud();
        return true;
    }

    private void AdjustBlockHeight(int delta) =>
        _blocks.AdjustHeight(delta, _blocks.CurrentAnchor(Hover)?.Y ?? SliceY, SliceY, Sim.World.SizeY);

    private void BlockRelease()
    {
        var click = _blocks.Release(Sim, Hover, SliceY, Input.IsKeyPressed(Key.Shift));
        if (click.Command != null) Sim.Enqueue(click.Command);
        if (click.Message != null) _hud.Toast(click.Message);
        if (!click.KeepTool) SetTool(ToolKind.Select);
    }

    /// <summary>Physics frame with the block tool: the drag end, then the ghost (re-uploaded only when it changed).</summary>
    private void UpdateBlockPreview()
    {
        _toolPreview.Visible = false;
        _blocks.Move(Hover);
        BlockGhost = _blocks.Ghost(Sim, Hover, SliceY);
        ShowBlockGhost(BlockGhost);
    }

    private void ShowBlockGhost(BlockGhost? ghost)
    {
        if (ReferenceEquals(ghost, _shownGhost)) return;
        _shownGhost = ghost;
        _blockGhostView.SetData(ghost == null ? null : BlockGhostMesher.Build(ghost, _blockColors, _entityColors));
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
    private void SyncBlockHud()
    {
        int? height = null;
        if (BuildShapes.UsesHeight(_blocks.Shape))
            height = _blocks.Height(_blocks.CurrentAnchor(Hover)?.Y ?? SliceY, SliceY, Sim.World.SizeY);
        _hud.SetBlockOptions(_tool.Tool, Content.LabelOf(_blocks.Block), _blocks.Shape, _blocks.Plan, height);
    }

    /// <summary>Screenshot harness (M8-T5): the block tool with a held drag from <paramref name="from"/> to
    /// <paramref name="to"/> (picking is off; <paramref name="to"/> becomes the pick override).</summary>
    public void ShowBlockDrag(PickHit from, PickHit to, BlockId block, BuildShape shape)
    {
        SetTool(ToolKind.Blocks);
        _blocks.Select(block);
        _blocks.SetShape(shape);
        _blocks.Press(from);
        PickOverride = to;
        SyncBlockHud();
    }

    /// <summary>VIEW-23: the plan line, every <see cref="PlanTextFrames"/> frames.</summary>
    private void UpdatePlanText(bool now)
    {
        if (!now && ++_planTextFrame < PlanTextFrames) return;
        _planTextFrame = 0;
        _hud.TopBar.ShowPlan(TopBarModel.PlanText(Sim));
    }
}
