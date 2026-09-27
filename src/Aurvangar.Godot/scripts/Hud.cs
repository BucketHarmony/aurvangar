using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Tools;
using Godot;

namespace Aurvangar.Client;

/// <summary>Player HUD: the tool bar (VIEW-12) with the Build menu (Warehouse / Pump / Levee), the top bar (VIEW-15),
/// the colonist panel (VIEW-16), a short-lived message line (quick save / load results, refused commands) and the
/// label that follows the mouse (pile counts VIEW-10, build and deconstruct tooltips VIEW-14, farm tiles and the farm
/// tool's moisture hint, M6-T5).</summary>
public partial class Hud : CanvasLayer
{
    public const double ToastSeconds = 3.0;

    public event System.Action<ToolKind>? ToolChosen;

    /// <summary>A building picked in the Build menu (its definition id).</summary>
    public event System.Action<string>? BuildChosen;

    public ColonistPanel Colonists { get; private set; } = null!;
    public TopBarView TopBar { get; private set; } = null!;

    /// <summary>Buildable (id, name) pairs for the Build menu; set by GameRoot before the node enters the tree.</summary>
    public IReadOnlyList<(string Id, string Name)> Buildable { get; set; } = System.Array.Empty<(string, string)>();

    private readonly Dictionary<ToolKind, Button> _toolButtons = new();
    private MenuButton _buildButton = null!;
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
        AddTool(bar, ToolKind.Deconstruct, "Deconstruct (X)");
        AddTool(bar, ToolKind.Cancel, "Cancel (Z)");
        AddChild(bar);

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
