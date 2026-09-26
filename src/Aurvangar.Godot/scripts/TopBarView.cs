using Aurvangar.ViewCore.Hud;
using Godot;

namespace Aurvangar.Client;

/// <summary>HUD top bar (VIEW-15) at the top right: day, speed, stored totals and red alerts (no food, no water, dry
/// pump). Shows a <see cref="TopBar"/> built by <see cref="TopBarModel"/> (ViewCore).</summary>
public partial class TopBarView : PanelContainer
{
    private Label _day = null!;
    private Label _speed = null!;
    private Label _totals = null!;
    private Label _alerts = null!;

    public override void _Ready()
    {
        Name = "TopBar";
        SelfModulate = new Color(0, 0, 0, 0.6f);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        _day = AddLabel(row, Colors.White);
        _speed = AddLabel(row, new Color(0.8f, 0.85f, 1f));
        _totals = AddLabel(row, Colors.White);
        _alerts = AddLabel(row, new Color(1f, 0.35f, 0.3f));
        AddChild(row);
    }

    public void Show(TopBar bar)
    {
        Set(_day, bar.DayText);
        Set(_speed, bar.Speed);
        Set(_totals, bar.TotalsText);
        Set(_alerts, string.Join("   ", bar.Alerts));
        _alerts.Visible = bar.Alerts.Count > 0;
        float width = GetViewport().GetVisibleRect().Size.X;
        Position = new Vector2(Mathf.Max(8f, width - Size.X - 8f), 8f);
    }

    private static void Set(Label l, string text)
    {
        if (l.Text != text) l.Text = text;
    }

    private static Label AddLabel(HBoxContainer row, Color color)
    {
        var l = new Label();
        l.AddThemeFontSizeOverride("font_size", 16);
        l.AddThemeColorOverride("font_color", color);
        row.AddChild(l);
        return l;
    }
}
