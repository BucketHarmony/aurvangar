using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Tools;
using Godot;

namespace Aurvangar.Client;

/// <summary>Player HUD: the tool bar (VIEW-12) with the Build menu (Warehouse / Pump / Levee), the top bar (VIEW-15),
/// the colonist panel (VIEW-16), a short-lived message line (quick save / load results, refused commands) and the
/// label that follows the mouse (pile counts VIEW-10, build and deconstruct tooltips VIEW-14, farm tiles and the farm
/// tool's moisture hint, M6-T5). M8-T5 adds the Blocks menu (block picker), the Release tool and the block options row
/// (block, Plan, Release all; VIEW-21; single blocks since M9-T1, so no height controls). M11-T11 adds the shape picker
/// and Rotate to that row (VIEW-27).</summary>
public partial class Hud : CanvasLayer
{
    public const double ToastSeconds = 3.0;

    /// <summary>The block options row's controls text (M9-T1).</summary>
    public const string BlockToolHint = "· click a face for one block, drag from a top face for a course, from a side face for a wall ·";

    public event System.Action<ToolKind>? ToolChosen;

    /// <summary>A building picked in the Build menu (its definition id).</summary>
    public event System.Action<string>? BuildChosen;

    /// <summary>A construction block picked in the Blocks menu.</summary>
    public event System.Action<BlockId>? BlockChosen;
    public event System.Action? PlanToggled;

    /// <summary>VIEW-27: a shape picked in the block options row, and the Rotate button (M11-T11).</summary>
    public event System.Action<BlockShape>? ShapeChosen;
    public event System.Action? RotatePressed;

    /// <summary>VIEW-24: a dig mode picked in the dig options row.</summary>
    public event System.Action<DigMode>? DigModeChosen;
    public event System.Action? ReleaseAllPressed;

    public ColonistPanel Colonists { get; private set; } = null!;
    public TopBarView TopBar { get; private set; } = null!;

    /// <summary>Buildable (id, name) pairs for the Build menu; set by GameRoot before the node enters the tree.</summary>
    public IReadOnlyList<(string Id, string Name)> Buildable { get; set; } = System.Array.Empty<(string, string)>();

    /// <summary>Construction blocks (id, label) for the Blocks menu; set by GameRoot before the node enters the tree.</summary>
    public IReadOnlyList<(BlockId Id, string Label)> BlockTypes { get; set; } = System.Array.Empty<(BlockId, string)>();

    /// <summary>CON-19 shapes (shape, label) for the shape picker; set by GameRoot before the node enters the tree.</summary>
    public IReadOnlyList<(BlockShape Shape, string Label)> ShapeTypes { get; set; } = System.Array.Empty<(BlockShape, string)>();

    private readonly Dictionary<ToolKind, Button> _toolButtons = new();
    private HBoxContainer _toolbar = null!;
    private MenuButton _buildButton = null!;
    private MenuButton _blocksButton = null!;
    private HBoxContainer _blockRow = null!;
    private Button _planButton = null!;
    private Label _blockLabel = null!;
    private Label _hintLabel = null!;
    private readonly Dictionary<BlockShape, Button> _shapeButtons = new();
    private Button _rotateButton = null!;
    private Label _toast = null!;
    private Label _hoverLabel = null!;
    private HBoxContainer _digRow = null!;
    private Button _digBoxButton = null!;
    private Button _digStairButton = null!;
    private Label _digHint = null!;
    private Label _sliceLabel = null!;
    private double _toastLeft;

    public override void _Ready()
    {
        Layer = 5;
        var bar = new HBoxContainer { Name = "Toolbar", Position = new Vector2(8, 8) };
        bar.AddThemeConstantOverride("separation", 4);
        AddTool(bar, ToolKind.Select, "Select (Esc)");
        AddTool(bar, ToolKind.Dig, "Dig (G)");
        AddTool(bar, ToolKind.Chop, "Chop (C)");
        AddTool(bar, ToolKind.Farm, "Farm (F)");
        AddBuildMenu(bar);
        AddBlocksMenu(bar);
        AddTool(bar, ToolKind.Release, "Release (L)");
        AddTool(bar, ToolKind.Deconstruct, "Deconstruct (X)");
        AddTool(bar, ToolKind.Cancel, "Cancel (Z)");
        _toolbar = bar;
        AddChild(bar);
        AddBlockRow();
        AddDigRow();

        _sliceLabel = OutlinedLabel("");
        _sliceLabel.Name = "SliceHint";
        AddChild(_sliceLabel);

        TopBar = new TopBarView();
        AddChild(TopBar);

        Colonists = new ColonistPanel();
        AddChild(Colonists);

        _toast = new Label { Name = "Toast", Visible = false, Position = new Vector2(8, 0) };
        _toast.AddThemeFontSizeOverride("font_size", 16);
        _toast.AddThemeColorOverride("font_outline_color", Colors.Black);
        _toast.AddThemeConstantOverride("outline_size", 4);
        AddChild(_toast);

        _hoverLabel = new Label { Name = "HoverLabel", Visible = false };
        _hoverLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _hoverLabel.AddThemeConstantOverride("outline_size", 4);
        AddChild(_hoverLabel);

        SetTool(ToolKind.Select);
    }

    public override void _Process(double delta)
    {
        var view = GetViewport().GetVisibleRect().Size;
        var tb = _toolbar.GetRect();
        TopBar.Arrange(new ScreenRect(tb.Position.X, tb.Position.Y, tb.Size.X, tb.Size.Y), view.X);
        if (_blockRow.Visible) _blockRow.Position = new Vector2(8, GetViewport().GetVisibleRect().Size.Y - 76);
        if (_digRow.Visible) _digRow.Position = new Vector2(8, view.Y - 76);
        var sliceSize = _sliceLabel.GetCombinedMinimumSize();
        _sliceLabel.Position = new Vector2(view.X - sliceSize.X - 8, view.Y - sliceSize.Y - 8);
        if (!_toast.Visible) return;
        _toastLeft -= delta;
        if (_toastLeft <= 0) _toast.Visible = false;
    }

    /// <summary>Highlights the active tool button; the Build button names the building being placed.</summary>
    public void SetTool(ToolKind tool, string? buildName = null)
    {
        foreach (var (kind, button) in _toolButtons) button.SetPressedNoSignal(kind == tool);
        _buildButton.Text = tool == ToolKind.Build && buildName != null ? $"Build: {buildName} (B)" : "Build (B)";
    }

    /// <summary>VIEW-21: the block options row shows while the Blocks or Release tool is active; the Blocks button
    /// starts with the block's label, "Plan: Stone wall" in plan mode. It sits at the bottom left, above the toast.</summary>
    /// VIEW-27 (M11-T11): the shape buttons (V cycles) and Rotate (R), shown while a shape with rotations is picked.
    public void SetBlockOptions(ToolKind tool, string blockLabel, bool plan, BlockShape shape = BlockShape.Full, bool canRotate = false)
    {
        bool blocks = tool == ToolKind.Blocks;
        _blockRow.Visible = blocks || tool == ToolKind.Release;
        _blockLabel.Visible = blocks;
        _blockLabel.Text = plan ? $"Plan: {blockLabel}" : blockLabel;
        _hintLabel.Visible = blocks;
        _planButton.Visible = blocks;
        _planButton.SetPressedNoSignal(plan);
        foreach (var (s, b) in _shapeButtons)
        {
            b.Visible = blocks;
            b.SetPressedNoSignal(s == shape);
        }
        _rotateButton.Visible = blocks && canRotate;
    }

    /// <summary>VIEW-24: the dig options row (Box, Stair down) shows while the dig tool is active.</summary>
    public void SetDigOptions(ToolKind tool, DigMode mode)
    {
        _digRow.Visible = tool == ToolKind.Dig;
        _digBoxButton.SetPressedNoSignal(mode == DigMode.Box);
        _digStairButton.SetPressedNoSignal(mode == DigMode.StairDown);
        _digHint.Text = mode == DigMode.StairDown ? DigStairHint : DigBoxHint;
    }

    /// <summary>VIEW-26: the view level and its keys at the bottom right (<see cref="SliceHint"/>).</summary>
    public void SetSliceHint(string text)
    {
        if (_sliceLabel.Text == text) return;
        _sliceLabel.Text = text;
        _sliceLabel.ResetSize();
    }

    public const string DigBoxHint = "· drag a box; it goes down to the view level (lower it with PageDown or [ )";
    public const string DigStairHint = "· drag from the top cell: one level down per cell, to the view level (lower it with PageDown or [ )";

    /// <summary>Shows a message at the bottom left for a few seconds.</summary>
    public void Toast(string text)
    {
        _toast.Text = text;
        _toast.Position = new Vector2(8, GetViewport().GetVisibleRect().Size.Y - 36);
        _toast.Visible = true;
        _toastLeft = ToastSeconds;
    }

    /// <summary>The label next to the mouse (pile counts, tool tooltips); null hides it. It goes where it covers none
    /// of the <paramref name="avoid"/> billboards and stays on screen (M7-T7, <see cref="LabelLayout.PlaceTooltip"/>).</summary>
    public void SetHoverLabel(string? text, Vector2 mouse, IReadOnlyList<ScreenRect> avoid)
    {
        _hoverLabel.Visible = text != null;
        if (text == null) return;
        if (_hoverLabel.Text != text)
        {
            _hoverLabel.Text = text;
            _hoverLabel.ResetSize();
        }
        var size = _hoverLabel.GetCombinedMinimumSize();
        var view = GetViewport().GetVisibleRect().Size;
        var p = LabelLayout.PlaceTooltip(new System.Numerics.Vector2(mouse.X, mouse.Y),
            new System.Numerics.Vector2(size.X, size.Y), avoid, new System.Numerics.Vector2(view.X, view.Y));
        _hoverLabel.Position = new Vector2(p.X, p.Y);
    }

    private void AddTool(HBoxContainer bar, ToolKind kind, string text)
    {
        var b = new Button { Text = text, ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
        b.Pressed += () => ToolChosen?.Invoke(kind);
        bar.AddChild(b);
        _toolButtons[kind] = b;
    }

    /// <summary>VIEW-21 block picker: a menu button listing the construction blocks by label.</summary>
    private void AddBlocksMenu(HBoxContainer bar)
    {
        _blocksButton = new MenuButton { Text = "Blocks (K)", ToggleMode = true, FocusMode = Control.FocusModeEnum.None, Flat = false };
        var popup = _blocksButton.GetPopup();
        for (int i = 0; i < BlockTypes.Count; i++) popup.AddItem(BlockTypes[i].Label, i);
        popup.IdPressed += id => BlockChosen?.Invoke(BlockTypes[(int)id].Id);
        bar.AddChild(_blocksButton);
        _toolButtons[ToolKind.Blocks] = _blocksButton;
    }

    /// <summary>VIEW-21 options row at the bottom left: the block, how to paint, Plan (P), Release all.</summary>
    private void AddBlockRow()
    {
        _blockRow = new HBoxContainer { Name = "BlockOptions", Visible = false };
        _blockRow.AddThemeConstantOverride("separation", 4);
        _blockLabel = OutlinedLabel("");
        _blockLabel.AddThemeFontSizeOverride("font_size", 16);
        _blockRow.AddChild(_blockLabel);
        foreach (var (shape, label) in ShapeTypes)
        {
            var b = new Button { Text = label, ToggleMode = true, FocusMode = Control.FocusModeEnum.None, TooltipText = "V: next shape" };
            b.Pressed += () => ShapeChosen?.Invoke(shape);
            _blockRow.AddChild(b);
            _shapeButtons[shape] = b;
        }
        _rotateButton = new Button { Text = "Rotate (R)", FocusMode = Control.FocusModeEnum.None };
        _rotateButton.Pressed += () => RotatePressed?.Invoke();
        _blockRow.AddChild(_rotateButton);
        _hintLabel = OutlinedLabel(BlockToolHint);
        _blockRow.AddChild(_hintLabel);
        _planButton = new Button { Text = "Plan (P)", ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
        _planButton.Pressed += () => PlanToggled?.Invoke();
        _blockRow.AddChild(_planButton);
        var releaseAll = new Button { Text = "Release all", FocusMode = Control.FocusModeEnum.None };
        releaseAll.Pressed += () => ReleaseAllPressed?.Invoke();
        _blockRow.AddChild(releaseAll);
        AddChild(_blockRow);
    }

    /// <summary>VIEW-24 dig options row at the bottom left: Box, Stair down (T), how the drag works.</summary>
    private void AddDigRow()
    {
        _digRow = new HBoxContainer { Name = "DigOptions", Visible = false };
        _digRow.AddThemeConstantOverride("separation", 4);
        var label = OutlinedLabel("Dig:");
        label.AddThemeFontSizeOverride("font_size", 16);
        _digRow.AddChild(label);
        _digBoxButton = new Button { Text = "Box", ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
        _digBoxButton.Pressed += () => DigModeChosen?.Invoke(DigMode.Box);
        _digRow.AddChild(_digBoxButton);
        _digStairButton = new Button { Text = "Stair down (T)", ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
        _digStairButton.Pressed += () => DigModeChosen?.Invoke(DigMode.StairDown);
        _digRow.AddChild(_digStairButton);
        _digHint = OutlinedLabel(DigBoxHint);
        _digRow.AddChild(_digHint);
        AddChild(_digRow);
    }

    private static Label OutlinedLabel(string text)
    {
        var l = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        l.AddThemeColorOverride("font_outline_color", Colors.Black);
        l.AddThemeConstantOverride("outline_size", 4);
        return l;
    }

    /// <summary>VIEW-12 "Build ▸ Warehouse / Pump / Levee": a menu button whose items choose the building.</summary>
    private void AddBuildMenu(HBoxContainer bar)
    {
        _buildButton = new MenuButton { Text = "Build (B)", ToggleMode = true, FocusMode = Control.FocusModeEnum.None, Flat = false };
        var popup = _buildButton.GetPopup();
        for (int i = 0; i < Buildable.Count; i++) popup.AddItem(Buildable[i].Name, i);
        popup.IdPressed += id => BuildChosen?.Invoke(Buildable[(int)id].Id);
        bar.AddChild(_buildButton);
        _toolButtons[ToolKind.Build] = _buildButton;
    }
}
