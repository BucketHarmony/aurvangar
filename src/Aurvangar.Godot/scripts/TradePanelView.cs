using Aurvangar.ViewCore.Hud;
using Godot;

namespace Aurvangar.Client;

/// <summary>Trade panel (VIEW-29): "Trade wagon: leaves in 3h", one row per offer (the swap, lots left, what the colony
/// has of the give item, Accept 1 and Accept all, disabled with the CRF-18 reason as tooltip) and the open deals with
/// their paid and granted lots. It shows a <see cref="TradePanel"/> built by <see cref="TradePanelModel"/> (ViewCore);
/// an accept button raises <see cref="AcceptPressed"/> with the offer and whether it was Accept all.</summary>
public partial class TradePanelView : PanelContainer
{
    public event System.Action<int, bool>? AcceptPressed;   // offer, all
    public event System.Action? CloseRequested;

    private sealed record Row(Control Root, Label Text, Label Lots, Label Have, Button One, Button All);

    private readonly List<Row> _rows = new();
    private readonly List<Label> _deals = new();
    private Label _title = null!;
    private GridContainer _grid = null!;
    private Label _dealsTitle = null!;
    private VBoxContainer _dealList = null!;

    public override void _Ready()
    {
        Name = "TradePanel";
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
        outer.AddChild(PanelText.Label("Each lot: we give the left side, the wagon gives the right. Dwarves carry the goods.", 12, new Color(0.8f, 0.8f, 0.8f)));
        _grid = new GridContainer { Columns = 4 };
        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 4);
        outer.AddChild(_grid);
        _dealsTitle = PanelText.Label("Deals", 15);
        outer.AddChild(_dealsTitle);
        _dealList = new VBoxContainer();
        outer.AddChild(_dealList);
        AddChild(outer);
    }

    public void ShowPanel(TradePanel? panel)
    {
        Visible = panel is not null;
        if (panel is null) return;
        PanelText.Set(_title, panel.Title);
        while (_rows.Count < panel.Offers.Count) _rows.Add(CreateRow(_rows.Count));
        for (int i = 0; i < _rows.Count; i++)
        {
            var ui = _rows[i];
            bool on = i < panel.Offers.Count;
            foreach (var c in new Control[] { ui.Text, ui.Lots, ui.Have, ui.One.GetParent<Control>() }) c.Visible = on;
            if (!on) continue;
            var r = panel.Offers[i];
            PanelText.Set(ui.Text, r.Text);
            PanelText.Set(ui.Lots, r.LotsText);
            PanelText.Set(ui.Have, r.HaveText);
            ui.Have.AddThemeColorOverride("font_color", r.AcceptOne.Reason == "NotEnough" ? new Color(1f, 0.65f, 0.25f) : Colors.White);
            SetButton(ui.One, "Accept 1", r.AcceptOne);
            SetButton(ui.All, $"Accept all ({r.AcceptAll.Lots})", r.AcceptAll);
        }
        _dealsTitle.Visible = panel.Deals.Count > 0;
        while (_deals.Count < panel.Deals.Count)
        {
            var l = PanelText.Label("", 13, new Color(0.85f, 0.95f, 0.85f));
            _dealList.AddChild(l);
            _deals.Add(l);
        }
        for (int i = 0; i < _deals.Count; i++)
        {
            _deals[i].Visible = i < panel.Deals.Count;
            if (i < panel.Deals.Count) PanelText.Set(_deals[i], panel.Deals[i].Text);
        }
    }

    private static void SetButton(Button b, string text, AcceptButton model)
    {
        if (b.Text != text) b.Text = text;
        b.Disabled = !model.Enabled;
        b.TooltipText = model.Tooltip ?? "";
    }

    private Row CreateRow(int offer)
    {
        var text = PanelText.Label("", 15);
        var lots = PanelText.Label("", 14, new Color(0.85f, 0.85f, 0.85f));
        var have = PanelText.Label("", 14);
        var one = new Button { FocusMode = FocusModeEnum.None };
        one.Pressed += () => AcceptPressed?.Invoke(offer, false);
        var all = new Button { FocusMode = FocusModeEnum.None };
        all.Pressed += () => AcceptPressed?.Invoke(offer, true);
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 4);
        buttons.AddChild(one);
        buttons.AddChild(all);
        foreach (var c in new Control[] { text, lots, have, buttons }) _grid.AddChild(c);
        return new Row(text, text, lots, have, one, all);
    }
}
