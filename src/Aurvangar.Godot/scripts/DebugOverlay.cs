using Godot;

namespace Aurvangar.Client;

/// <summary>F3 debug overlay (VIEW-17): a Label on its own CanvasLayer. GameRoot builds the text with
/// <c>ViewCore.Diagnostics.DebugOverlayText</c>; this node only shows it. Hidden by default. Sits at the top right,
/// clear of the tool bar and the colonist panel.</summary>
public partial class DebugOverlay : CanvasLayer
{
    private Label _label = null!;
    private PanelContainer _panel = null!;

    public override void _Ready()
    {
        Layer = 10;
        Visible = false;
        var panel = new PanelContainer { Name = "Panel", Position = new Vector2(8, 8) };
        _panel = panel;
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
        float width = GetViewport().GetVisibleRect().Size.X;
        _panel.Position = new Vector2(Mathf.Max(8f, width - _panel.Size.X - 8f), 8f);
    }
}
