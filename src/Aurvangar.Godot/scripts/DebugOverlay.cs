using Godot;

namespace Aurvangar.Client;

/// <summary>F3 debug overlay (VIEW-17): a Label on its own CanvasLayer. GameRoot builds the text with
/// <c>ViewCore.Diagnostics.DebugOverlayText</c>; this node only shows it. Hidden by default.</summary>
public partial class DebugOverlay : CanvasLayer
{
    private Label _label = null!;

    public override void _Ready()
    {
        Layer = 10;
        Visible = false;
        var panel = new PanelContainer { Name = "Panel", Position = new Vector2(8, 8) };
        panel.SelfModulate = new Color(0, 0, 0, 0.6f);
        _label = new Label { Name = "Text" };
        _label.AddThemeFontSizeOverride("font_size", 14);
        panel.AddChild(_label);
        AddChild(panel);
    }

    public void Toggle() => Visible = !Visible;

    public void SetText(string text)
    {
        if (_label.Text != text) _label.Text = text;
    }
}
