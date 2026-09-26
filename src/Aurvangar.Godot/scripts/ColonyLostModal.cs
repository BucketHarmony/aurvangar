using Godot;

namespace Aurvangar.Client;

/// <summary>"Colony lost" modal (VIEW-18): dims the screen and offers Load (the F9 quick save) or Close (keep looking
/// at the fallen colony). GameRoot opens it on the <c>ColonyLost</c> event, or after attaching a simulation whose
/// colony is already lost, and hides it when a load brings living dwarves back.</summary>
public partial class ColonyLostModal : CanvasLayer
{
    public event System.Action? LoadPressed;

    private Label _detail = null!;

    public override void _Ready()
    {
        Layer = 20;
        Visible = false;
        var dim = new ColorRect { Name = "Dim", Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer { Name = "Center" };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { Name = "Panel", CustomMinimumSize = new Vector2(380, 0) };
        center.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);

        var title = new Label { Text = "Colony lost", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.35f));
        box.AddChild(title);
        _detail = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _detail.AddThemeFontSizeOverride("font_size", 16);
        box.AddChild(_detail);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        var load = new Button { Text = "Load quick save (F9)", FocusMode = Control.FocusModeEnum.None };
        load.Pressed += () => LoadPressed?.Invoke();
        var close = new Button { Text = "Close", FocusMode = Control.FocusModeEnum.None };
        close.Pressed += () => Visible = false;
        buttons.AddChild(load);
        buttons.AddChild(close);
        box.AddChild(buttons);
    }

    public void Open(int day)
    {
        _detail.Text = $"All dwarves have died. Day {day}.";
        Visible = true;
    }
}
