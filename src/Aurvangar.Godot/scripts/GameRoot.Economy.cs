using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Events;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Tools;
using Godot;

namespace Aurvangar.Client;

/// <summary>Workshop and trade panels of <see cref="GameRoot"/> (M11-T6): a Select click on a workshop opens its panel
/// (VIEW-28), the toolbar's Trade button and the arrival toast open the trade panel (VIEW-29). The rules and texts are
/// <see cref="WorkshopPanelModel"/> and <see cref="TradePanelModel"/> (ViewCore); this file forwards the buttons and
/// enqueues the commands. One panel is open at a time.</summary>
public partial class GameRoot
{
    private readonly WorkshopPanelModel _workshopPanel = new();
    private bool _tradeOpen;

    private void ReadyEconomyPanels()
    {
        _hud.Workshop.StepPressed += (recipe, sign) =>
            Send(_workshopPanel.StepCount(Sim, recipe, sign * WorkshopPanelModel.StepSize(Input.IsKeyPressed(Key.Shift))));
        _hud.Workshop.ModeChosen += (recipe, mode) => Send(_workshopPanel.SetMode(Sim, recipe, mode));
        _hud.Workshop.ClearPressed += recipe => Send(_workshopPanel.Clear(Sim, recipe));
        _hud.Workshop.CloseRequested += _workshopPanel.Close;
        _hud.TradePressed += () => { if (_tradeOpen) _tradeOpen = false; else OpenTradePanel(); };
        _hud.Trade.CloseRequested += () => _tradeOpen = false;
        _hud.Trade.AcceptPressed += AcceptPressed;
    }

    /// <summary>Opens the workshop panel on <paramref name="b"/> (closing the trade panel); false when it is not a
    /// workshop.</summary>
    public bool OpenWorkshopPanel(Building? b)
    {
        if (!_workshopPanel.Open(b)) return false;
        _tradeOpen = false;
        UpdateEconomyPanels();
        return true;
    }

    /// <summary>Opens the trade panel (closing the workshop panel) while a trader is here.</summary>
    public void OpenTradePanel()
    {
        if (!Sim.Trader.IsHere) return;
        _workshopPanel.Close();
        _tradeOpen = true;
        UpdateEconomyPanels();
    }

    /// <summary>A Select click: over a workshop it opens the panel. Returns whether it did.</summary>
    private bool SelectClick()
    {
        if (_tool.Tool != ToolKind.Select) return false;
        return OpenWorkshopPanel(DeconstructTool.Target(Sim, Hover));
    }

    private void AcceptPressed(int offer, bool all)
    {
        if (TradePanelModel.Build(Sim) is not { } panel || offer >= panel.Offers.Count) return;
        var row = panel.Offers[offer];
        var button = all ? row.AcceptAll : row.AcceptOne;
        if (button.Command(offer) is { } command) Sim.Enqueue(command);
        else if (button.Tooltip is { } why) _hud.Toast(why);
    }

    private void Send(Aurvangar.Sim.Commands.ICommand? command)
    {
        if (command != null) Sim.Enqueue(command);
    }

    /// <summary>Each frame: the panels' contents and the Trade button. A gone workshop or a departed trader closes its
    /// panel.</summary>
    private void UpdateEconomyPanels()
    {
        var workshop = _workshopPanel.Build(Sim);
        if (workshop is null && _workshopPanel.IsOpen) _workshopPanel.Close();
        _hud.Workshop.ShowPanel(workshop);
        var trade = _tradeOpen ? TradePanelModel.Build(Sim) : null;
        if (trade is null) _tradeOpen = false;
        _hud.Trade.ShowPanel(trade);
        _hud.SetTradeButton(Sim.Trader.IsHere, TradePanelModel.ButtonTooltip(Sim));
    }

    /// <summary>VIEW-29 toasts for the trader's events.</summary>
    private void HandleTraderEvent(SimEvent e)
    {
        switch (e)
        {
            case TraderArrived:
                _hud.Toast(TradePanelModel.ArrivedToast(Sim));
                break;
            case TraderLeft:
                _hud.Toast(TradePanelModel.LeftToast);
                break;
            case TraderNoRoom:
                _hud.Toast(TradePanelModel.NoRoomToast);
                break;
        }
    }
}
