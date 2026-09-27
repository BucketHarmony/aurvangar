using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Tools;
using Godot;

namespace Aurvangar.Client;

/// <summary>Player HUD: the tool bar (VIEW-12) with the Build menu (Warehouse / Pump / Levee), the top bar (VIEW-15),
/// the colonist panel (VIEW-16), a short-lived message line (quick save / load results, refused commands) and the
/// label that follows the mouse (pile counts VIEW-10, build and deconstruct tooltips VIEW-14, farm tiles and the farm
/// tool's moisture hint, M6-T5). M8-T5 adds the Blocks menu (block picker), the Release tool and the block options row
/// (block, Plan, Release all; VIEW-21; single blocks since M9-T1, so no shape or height controls).</summary>
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
    public event System.Action? ReleaseAllPressed;

    public ColonistPanel Colonists { get; private set; } = null!;
    public TopBarView TopBar { get; private set; } = null!;

    /// <summary>Buildable (id, name) pairs for the Build menu; set by GameRoot before the node enters the tree.</summary>
    public IReadOnlyList<(string Id, string Name)> Buildable { get; set; } = System.Array.Empty<(string, string)>();

    /// <summary>Construction blocks (id, label) for the Blocks menu; set by GameRoot before the node enters the tree.</summary>
    public IReadOnlyList<(BlockId Id, string Label)> BlockTypes { get; set; } = System.Array.Empty<(BlockId, string)>();

    private readonly Dictionary<ToolKind, Button> _toolButtons = new();
    private HBoxContainer _toolbar = null!;
    private MenuButton _buildButton = null!;
    private MenuButton _blocksButton = null!;
    private HBoxContainer _blockRow = null!;
    private Button _planButton = null!;
    private Label _blockLabel = null!;
    private Label _hintLabel = null!;
    private Label _toast = null!;
    private Label _hoverLabel = null!;
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
    public void SetBlockOptions(ToolKind tool, string blockLabel, bool plan)
    {
        bool blocks = tool == ToolKind.Blocks;
        _blockRow.Visible = blocks || tool == ToolKind.Release;
        _blockLabel.Visible = blocks;
        _blockLabel.Text = plan ? $"Plan: {blockLabel}" : blockLabel;
        _hintLabel.Visible = blocks;
        _planButton.Visible = blocks;
        _planButton.SetPressedNoSignal(plan);
    }

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
