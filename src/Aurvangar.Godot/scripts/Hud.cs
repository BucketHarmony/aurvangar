using Aurvangar.ViewCore.Tools;
using Godot;

namespace Aurvangar.Client;

/// <summary>Player HUD (M4 part): the tool bar (VIEW-12), the colonist panel (VIEW-16), a short-lived message line
/// (quick save / load results, VIEW-19) and the pile count label that follows the mouse (VIEW-10). Farm, Build and
/// Deconstruct buttons are shown disabled until their systems exist (M5-T6, M6-T5).</summary>
public partial class Hud : CanvasLayer
{
    public const double ToastSeconds = 3.0;

    public event System.Action<ToolKind>? ToolChosen;

    public ColonistPanel Colonists { get; private set; } = null!;

    private readonly Dictionary<ToolKind, Button> _toolButtons = new();
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
        AddDisabled(bar, "Farm (F)");
        AddDisabled(bar, "Build (B)");
        AddDisabled(bar, "Deconstruct (X)");
        AddTool(bar, ToolKind.Cancel, "Cancel (Z)");
        AddChild(bar);

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

    /// <summary>Highlights the active tool button.</summary>
    public void SetTool(ToolKind tool)
    {
        foreach (var (kind, button) in _toolButtons) button.SetPressedNoSignal(kind == tool);
    }

    /// <summary>Shows a message at the bottom left for a few seconds.</summary>
    public void Toast(string text)
    {
        _toast.Text = text;
        _toast.Position = new Vector2(8, GetViewport().GetVisibleRect().Size.Y - 36);
        _toast.Visible = true;
        _toastLeft = ToastSeconds;
    }

    /// <summary>The label next to the mouse (pile counts); null hides it.</summary>
    public void SetHoverLabel(string? text, Vector2 mouse)
    {
        _hoverLabel.Visible = text != null;
        if (text == null) return;
        if (_hoverLabel.Text != text) _hoverLabel.Text = text;
        _hoverLabel.Position = mouse + new Vector2(16, 12);
    }

    private void AddTool(HBoxContainer bar, ToolKind kind, string text)
    {
        var b = new Button { Text = text, ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
        b.Pressed += () => ToolChosen?.Invoke(kind);
        bar.AddChild(b);
        _toolButtons[kind] = b;
    }

    private static void AddDisabled(HBoxContainer bar, string text) =>
        bar.AddChild(new Button { Text = text, Disabled = true, FocusMode = Control.FocusModeEnum.None, TooltipText = "Not yet" });
}
