using Aurvangar.ViewCore.Hud;
using Godot;

namespace Aurvangar.Client;

/// <summary>HUD top bar (VIEW-15) at the top right: day, season and days left (ECO-18; orange in a drought), speed,
/// stored totals and red alerts (no food, no water, dry pump). Shows a <see cref="TopBar"/> built by <see cref="TopBarModel"/> (ViewCore).</summary>
public partial class TopBarView : PanelContainer
{
    private static readonly Color WetColor = new(0.6f, 0.85f, 1f);
    private static readonly Color DroughtColor = new(1f, 0.65f, 0.25f);

    private Label _day = null!;
    private Label _season = null!;
    private Label _speed = null!;
    private Label _totals = null!;
    private Label _alerts = null!;

    public override void _Ready()
    {
        Name = "TopBar";
        // Pinned to the top right; it grows to the left when its text gets longer (season, alerts).
        AnchorLeft = 1f;
        AnchorRight = 1f;
        OffsetLeft = -8f;
        OffsetRight = -8f;
        OffsetTop = 8f;
        GrowHorizontal = GrowDirection.Begin;
        SelfModulate = new Color(0, 0, 0, 0.6f);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        _day = AddLabel(row, Colors.White);
        _season = AddLabel(row, WetColor);
        _speed = AddLabel(row, new Color(0.8f, 0.85f, 1f));
        _totals = AddLabel(row, Colors.White);
        _alerts = AddLabel(row, new Color(1f, 0.35f, 0.3f));
        AddChild(row);
    }

    public void Show(TopBar bar)
    {
        Set(_day, bar.DayText);
        Set(_season, bar.SeasonText);
        _season.AddThemeColorOverride("font_color", bar.Season == Aurvangar.Sim.Water.Season.Drought ? DroughtColor : WetColor);
        Set(_speed, bar.Speed);
        Set(_totals, bar.TotalsText);
        Set(_alerts, string.Join("   ", bar.Alerts));
        _alerts.Visible = bar.Alerts.Count > 0;
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
