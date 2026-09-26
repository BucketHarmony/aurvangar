using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Hud;
using Godot;

namespace Aurvangar.Client;

/// <summary>Left colonist panel (VIEW-16): per dwarf a name, hunger / thirst / health bars and the current activity
/// from <see cref="ColonistPanelModel"/>. Clicking a row raises <see cref="Clicked"/> (GameRoot centers the
/// camera). Rows are created on demand and only their values change afterwards.</summary>
public partial class ColonistPanel : PanelContainer
{
    public const float Width = 230f;

    public event System.Action<AgentId>? Clicked;

    private sealed record Row(Control Root, Label Name, ProgressBar Hunger, ProgressBar Thirst, ProgressBar Health, Label Activity);

    private readonly List<Row> _rows = new();
    private readonly List<AgentId> _ids = new();
    private VBoxContainer _list = null!;

    public override void _Ready()
    {
        Name = "ColonistPanel";
        Position = new Vector2(8, 52);
        CustomMinimumSize = new Vector2(Width, 0);
        SelfModulate = new Color(0, 0, 0, 0.55f);
        var outer = new VBoxContainer();
        var title = new Label { Text = "Dwarves" };
        title.AddThemeFontSizeOverride("font_size", 16);
        outer.AddChild(title);
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 6);
        outer.AddChild(_list);
        AddChild(outer);
    }

    public void SetRows(IReadOnlyList<ColonistRow> rows)
    {
        while (_rows.Count < rows.Count) _rows.Add(CreateRow(_rows.Count));
        for (int i = 0; i < _rows.Count; i++)
        {
            var ui = _rows[i];
            ui.Root.Visible = i < rows.Count;
            if (i >= rows.Count) continue;
            var r = rows[i];
            if (_ids.Count <= i) _ids.Add(r.Id); else _ids[i] = r.Id;
            SetText(ui.Name, r.Name);
            ui.Hunger.Value = r.Hunger;
            ui.Thirst.Value = r.Thirst;
            ui.Health.Value = r.Health;
            SetText(ui.Activity, r.Activity);
            ui.Root.Modulate = r.Alive ? Colors.White : new Color(1, 1, 1, 0.5f);
        }
    }

    private Row CreateRow(int index)
    {
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Stop, TooltipText = "Click to center the camera" };
        box.AddThemeConstantOverride("separation", 1);
        var name = new Label();
        name.AddThemeFontSizeOverride("font_size", 14);
        box.AddChild(name);
        var hunger = Bar(box, "Food", new Color(0.85f, 0.6f, 0.25f));
        var thirst = Bar(box, "Drink", new Color(0.3f, 0.6f, 0.95f));
        var health = Bar(box, "Health", new Color(0.8f, 0.25f, 0.25f));
        var activity = new Label();
        activity.AddThemeFontSizeOverride("font_size", 12);
        activity.Modulate = new Color(0.85f, 0.85f, 0.85f);
        box.AddChild(activity);
        box.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && index < _ids.Count)
                Clicked?.Invoke(_ids[index]);
        };
        _list.AddChild(box);
        return new Row(box, name, hunger, thirst, health, activity);
    }

    private static ProgressBar Bar(Control parent, string label, Color color)
    {
        var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        var text = new Label { Text = label, CustomMinimumSize = new Vector2(52, 0) };
        text.AddThemeFontSizeOverride("font_size", 11);
        line.AddChild(text);
        var bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false,
            CustomMinimumSize = new Vector2(150, 10), SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = color });
        line.AddChild(bar);
        parent.AddChild(line);
        return bar;
    }

    private static void SetText(Label l, string text)
    {
        if (l.Text != text) l.Text = text;
    }
}
