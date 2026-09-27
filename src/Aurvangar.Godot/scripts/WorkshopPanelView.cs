using Aurvangar.Sim.Buildings;
using Aurvangar.ViewCore.Hud;
using Godot;

namespace Aurvangar.Client;

/// <summary>Workshop panel (VIEW-28): the workshop's name and CRF-13 status, and per recipe its text, Make / Keep
/// toggles, - count + (Shift: 5), done/count for Make, and Clear. It shows a <see cref="WorkshopPanel"/> built by
/// <see cref="WorkshopPanelModel"/> (ViewCore); the buttons raise events that GameRoot turns into commands. Rows are
/// created on demand and only their values change afterwards.</summary>
public partial class WorkshopPanelView : PanelContainer
{
    public event System.Action<int, int>? StepPressed;          // recipe, sign (-1 / +1)
    public event System.Action<int, OrderMode>? ModeChosen;     // recipe, mode
    public event System.Action<int>? ClearPressed;              // recipe
    public event System.Action? CloseRequested;

    private sealed record Row(Control Root, Label Text, Button Make, Button Keep, Label Count, Label Progress, Button Clear);

    private readonly List<Row> _rows = new();
    private Label _title = null!;
    private Label _status = null!;
    private VBoxContainer _list = null!;

    public override void _Ready()
    {
        Name = "WorkshopPanel";
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        SelfModulate = new Color(0, 0, 0, 0.7f);
        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 6);
        var header = new HBoxContainer();
        _title = PanelText.Label("", 18);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_title);
        var close = new Button { Text = "Close", FocusMode = FocusModeEnum.None };
        close.Pressed += () => CloseRequested?.Invoke();
        header.AddChild(close);
        outer.AddChild(header);
        _status = PanelText.Label("", 14);
        outer.AddChild(_status);
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 4);
        outer.AddChild(_list);
        outer.AddChild(PanelText.Label("Make N: craft N, then stop · Keep N: keep N in stock · Shift: steps of 5", 12, new Color(0.8f, 0.8f, 0.8f)));
        AddChild(outer);
    }

    public void ShowPanel(WorkshopPanel? panel)
    {
        Visible = panel is not null;
        if (panel is null) return;
        PanelText.Set(_title, panel.Title);
        PanelText.Set(_status, panel.StatusText);
        _status.AddThemeColorOverride("font_color", panel.Status is WorkshopStatus.NoInput or WorkshopStatus.OutputFull
            ? new Color(1f, 0.45f, 0.35f) : new Color(0.75f, 0.9f, 1f));
        while (_rows.Count < panel.Rows.Count) _rows.Add(CreateRow(_rows.Count));
        for (int i = 0; i < _rows.Count; i++)
        {
            var ui = _rows[i];
            ui.Root.Visible = i < panel.Rows.Count;
            if (i >= panel.Rows.Count) continue;
            var r = panel.Rows[i];
            PanelText.Set(ui.Text, r.Text);
            ui.Make.SetPressedNoSignal(r.Mode == OrderMode.Make);
            ui.Keep.SetPressedNoSignal(r.Mode == OrderMode.Keep);
            PanelText.Set(ui.Count, r.HasOrder ? r.Count.ToString() : "-");
            PanelText.Set(ui.Progress, r.ProgressText);
            ui.Clear.Disabled = !r.HasOrder;
        }
    }

    private Row CreateRow(int recipe)
    {
        var box = new VBoxContainer();
        var text = PanelText.Label("", 15);
        box.AddChild(text);
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 4);
        var make = Toggle("Make", "Make N, then stop");
        make.Pressed += () => ModeChosen?.Invoke(recipe, OrderMode.Make);
        var keep = Toggle("Keep", "Keep at least N in stock");
        keep.Pressed += () => ModeChosen?.Invoke(recipe, OrderMode.Keep);
        var minus = new Button { Text = " - ", FocusMode = FocusModeEnum.None, TooltipText = "Shift: 5" };
        minus.Pressed += () => StepPressed?.Invoke(recipe, -1);
        var count = PanelText.Label("", 15);
        count.CustomMinimumSize = new Vector2(40, 0);
        count.HorizontalAlignment = HorizontalAlignment.Center;
        var plus = new Button { Text = " + ", FocusMode = FocusModeEnum.None, TooltipText = "Shift: 5" };
        plus.Pressed += () => StepPressed?.Invoke(recipe, +1);
        var progress = PanelText.Label("", 13, new Color(0.85f, 0.85f, 0.85f));
        progress.CustomMinimumSize = new Vector2(90, 0);
        var clear = new Button { Text = "Clear", FocusMode = FocusModeEnum.None };
        clear.Pressed += () => ClearPressed?.Invoke(recipe);
        foreach (var c in new Control[] { make, keep, minus, count, plus, progress, clear }) line.AddChild(c);
        box.AddChild(line);
        _list.AddChild(box);
        return new Row(box, text, make, keep, count, progress, clear);
    }

    private static Button Toggle(string text, string tooltip) =>
        new() { Text = text, ToggleMode = true, FocusMode = FocusModeEnum.None, TooltipText = tooltip };
}
