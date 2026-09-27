using Aurvangar.ViewCore.Hud;
using Godot;

namespace Aurvangar.Client;

/// <summary>HUD top bar (VIEW-15) at the top right: day, season and days left (ECO-18; orange in a drought), speed,
/// stored totals and red alerts (no food, no water, dry pump); below them the plan's material line (VIEW-23, M8-T5).
/// Shows a <see cref="TopBar"/> built by <see cref="TopBarModel"/> (ViewCore). Since M9-T3 it never overlaps the
/// toolbar: <see cref="Arrange"/> measures the items and lets <see cref="HudLayout.ArrangeTopBar"/> (ViewCore) wrap
/// them beside the toolbar, or drop the bar below it.</summary>
public partial class TopBarView : PanelContainer
{
    private static readonly Color WetColor = new(0.6f, 0.85f, 1f);
    private static readonly Color DroughtColor = new(1f, 0.65f, 0.25f);
    private static readonly Color AlertColor = new(1f, 0.35f, 0.3f);
    private static readonly Color ShortColor = new(1f, 0.65f, 0.25f);

    private Label _day = null!;
    private Label _season = null!;
    private Label _speed = null!;
    private Label _totals = null!;
    private readonly List<Label> _alerts = new();
    private readonly List<(HBoxContainer Box, Label Lead)> _atoms = new();
    private VBoxContainer _rows = null!;
    private readonly List<HBoxContainer> _lines = new();
    private string _planText = "";
    private string _layoutKey = "";

    public override void _Ready()
    {
        Name = "TopBar";
        SelfModulate = new Color(0, 0, 0, 0.6f);
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 0);
        AddChild(_rows);
        _day = NewLabel(Colors.White);
        _season = NewLabel(WetColor);
        _speed = NewLabel(new Color(0.8f, 0.85f, 1f));
        _totals = NewLabel(Colors.White);
    }

    /// <summary>VIEW-23: the plan's material against stock after the first row, short items orange; hidden when
    /// empty. It wraps between items (<see cref="PlanText.Atoms"/>).</summary>
    public void ShowPlan(PlanText plan)
    {
        string text = plan.Text;
        if (text == _planText) return;
        _planText = text;
        foreach (var (box, _) in _atoms) { box.GetParent()?.RemoveChild(box); box.QueueFree(); }
        _atoms.Clear();
        foreach (var atom in plan.Atoms())
        {
            var box = new HBoxContainer();
            box.AddThemeConstantOverride("separation", 0);
            var lead = NewLabel(Colors.White);
            lead.Text = atom.Lead;
            box.AddChild(lead);
            foreach (var (run, isShort) in atom.Runs) box.AddChild(NewLabel(isShort ? ShortColor : Colors.White, run));
            _atoms.Add((box, lead));
        }
        _layoutKey = "";
    }

    public void Show(TopBar bar)
    {
        Set(_day, bar.DayText);
        Set(_season, bar.SeasonText);
        _season.AddThemeColorOverride("font_color", bar.Season == Aurvangar.Sim.Water.Season.Drought ? DroughtColor : WetColor);
        Set(_speed, bar.Speed);
        Set(_totals, bar.TotalsText);
        while (_alerts.Count < bar.Alerts.Count) _alerts.Add(NewLabel(AlertColor));
        while (_alerts.Count > bar.Alerts.Count)
        {
            var l = _alerts[^1];
            _alerts.RemoveAt(_alerts.Count - 1);
            l.GetParent()?.RemoveChild(l);
            l.QueueFree();
            _layoutKey = "";
        }
        for (int i = 0; i < bar.Alerts.Count; i++) Set(_alerts[i], bar.Alerts[i]);
    }

    /// <summary>M9-T3: wraps the items so the bar fits between the toolbar and the right edge (or below the toolbar),
    /// and places it. Called every frame by the HUD; nodes move between lines only when the wrap changes.</summary>
    public void Arrange(ScreenRect toolbar, float viewportWidth)
    {
        var rowLabels = new List<Control> { _day, _season, _speed, _totals };
        rowLabels.AddRange(_alerts);
        var row = rowLabels.Select(l => new FlowItem(0f, l.GetCombinedMinimumSize().X)).ToList();
        var plan = _atoms.Select(a =>
        {
            float lead = a.Lead.GetCombinedMinimumSize().X;
            return new FlowItem(lead, a.Box.GetCombinedMinimumSize().X - (a.Lead.Visible ? lead : 0f));
        }).ToList();
        float padding = GetCombinedMinimumSize().X - _rows.GetCombinedMinimumSize().X;
        var bar = HudLayout.ArrangeTopBar(viewportWidth, toolbar, padding, row, plan);

        string key = $"{string.Join(',', bar.RowLines)}|{string.Join(',', bar.PlanLines)}|{rowLabels.Count}|{_atoms.Count}";
        if (key != _layoutKey)
        {
            _layoutKey = key;
            Relayout(rowLabels, bar);
        }
        ResetSize();
        var size = GetCombinedMinimumSize();
        Position = new Vector2(viewportWidth - HudLayout.Margin - size.X, bar.Rect.Y);
    }

    private void Relayout(List<Control> rowLabels, TopBarArrangement bar)
    {
        foreach (var c in rowLabels) c.GetParent()?.RemoveChild(c);
        foreach (var (box, _) in _atoms) box.GetParent()?.RemoveChild(box);
        while (_lines.Count < bar.LineCount)
        {
            var line = new HBoxContainer();
            _rows.AddChild(line);
            _lines.Add(line);
        }
        for (int i = 0; i < _lines.Count; i++)
        {
            bool rowLine = i < bar.LineCount && (bar.PlanLines.Length == 0 || i < bar.PlanLines[0]);
            _lines[i].AddThemeConstantOverride("separation", rowLine ? (int)HudLayout.RowSeparation : 0);
            _lines[i].Visible = i < bar.LineCount;
        }
        for (int i = 0; i < rowLabels.Count; i++) _lines[bar.RowLines[i]].AddChild(rowLabels[i]);
        for (int i = 0; i < _atoms.Count; i++)
        {
            int line = bar.PlanLines[i];
            _atoms[i].Lead.Visible = i > 0 && bar.PlanLines[i - 1] == line;
            _lines[line].AddChild(_atoms[i].Box);
        }
    }

    private static void Set(Label l, string text)
    {
        if (l.Text != text) l.Text = text;
    }

    private static Label NewLabel(Color color, string text = "")
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", 16);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }
}
