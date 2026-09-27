using Aurvangar.Sim.Events;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Tools;
using Godot;

namespace Aurvangar.Client;

/// <summary>Build and deconstruct tools of <see cref="GameRoot"/> (VIEW-12, VIEW-14): the ghost and entrance tile,
/// their tooltips, clicks, Shift-drag levee lines, R rotate and B next building. The rules are
/// <see cref="BuildTool"/> and <see cref="DeconstructTool"/> (ViewCore); this file forwards input and enqueues the
/// commands. Also the colony-lost modal (VIEW-18) and toasts for refused commands.</summary>
public partial class GameRoot
{
    private static readonly Color GhostOk = new(0.3f, 0.95f, 0.35f, 0.35f);
    private static readonly Color GhostBad = new(1f, 0.25f, 0.2f, 0.35f);
    private static readonly Color EntranceTile = new(1f, 1f, 1f, 0.6f);
    private static readonly Color DeconstructMark = new(1f, 0.45f, 0.15f, 0.35f);

    private BuildTool _build = null!;
    private ToolPreview _entrancePreview = null!;
    private ColonyLostModal _lostModal = null!;

    /// <summary>The build ghost under the mouse (null when the build tool is off or the mouse is over nothing).</summary>
    public BuildGhost? Ghost { get; private set; }

    private void ReadyBuildTools()
    {
        _build = new BuildTool(Content);
        _entrancePreview = new ToolPreview { Name = "EntrancePreview" };
        AddChild(_entrancePreview);
        _lostModal = new ColonyLostModal { Name = "ColonyLost" };
        AddChild(_lostModal);
        _lostModal.LoadPressed += QuickLoadNow;
    }

    /// <summary>Toolbar Build menu: choose the building and switch to the build tool.</summary>
    private void ChooseBuilding(string defId)
    {
        _build.Select(defId);
        SetTool(ToolKind.Build);
    }

    /// <summary>B: switch to the build tool, or, when it is already active, go to the next building.</summary>
    private void BuildHotkey()
    {
        if (_tool.Tool == ToolKind.Build) _build.Cycle();
        SetTool(ToolKind.Build);
    }

    private void RotateBuild()
    {
        if (_tool.Tool == ToolKind.Build) _build.Rotate();
    }

    /// <summary>Left button on a click tool. Returns false when the active tool is a drag tool.</summary>
    private bool ClickToolPress()
    {
        switch (_tool.Tool)
        {
            case ToolKind.Build:
            {
                var click = _build.Press(Sim, Hover, Input.IsKeyPressed(Key.Shift));
                if (click.Command != null) Sim.Enqueue(click.Command);
                if (click.Message != null) _hud.Toast(click.Message);
                if (!click.KeepTool) SetTool(ToolKind.Select);
                return true;
            }
            case ToolKind.Deconstruct:
            {
                // M8-T5: over a building it is the BLD-09 click; elsewhere a box drag of built blocks (CON-18).
                var press = DeconstructTool.Press(Sim, Hover);
                if (press.Command != null) Sim.Enqueue(press.Command);
                if (press.Message != null) _hud.Toast(press.Message);
                if (press.StartDrag && Hover is { } h) _tool.BeginDrag(h);
                return true;
            }
            case ToolKind.Blocks:
                _blocks.Press(Hover);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Physics frame: Shift-drag placements, then the ghost or the deconstruct target box.</summary>
    private void UpdateClickToolPreview()
    {
        Ghost = null;
        _entrancePreview.Visible = false;
        if (_tool.Tool == ToolKind.Build)
        {
            if (_build.Drag(Sim, Hover, Input.IsKeyPressed(Key.Shift)) is { } more) Sim.Enqueue(more);
            if (_build.Ghost(Sim, Hover) is { } g)
            {
                Ghost = g;
                _toolPreview.Show(g.Min, g.Max, g.Ok ? GhostOk : GhostBad);
                _entrancePreview.ShowTile(g.Entrance, EntranceTile);
            }
            else _toolPreview.Visible = false;
        }
        else if (_tool.Tool == ToolKind.Deconstruct)
        {
            if (_tool.PreviewBox(SliceY) is var (min0, max0)) _toolPreview.Show(min0, max0, DeconstructMark);
            else if (DeconstructTool.Target(Sim, Hover) is { } b)
            {
                var (min, max) = BuildingVisuals.Bounds(b);
                _toolPreview.Show(min, max, DeconstructTool.Refusal(Sim, b) == null ? DeconstructMark : GhostBad);
            }
            else _toolPreview.Visible = false;
        }
    }

    /// <summary>The mouse label of the build and deconstruct tools, or null.</summary>
    private string? ClickToolTooltip()
    {
        if (_tool.Tool == ToolKind.Build && Ghost is { } g) return BuildTool.Tooltip(Sim, g);
        if (_tool.Tool == ToolKind.Deconstruct && _tool.Dragging) return "Take down the built blocks in these columns (up to the view level)";
        if (_tool.Tool == ToolKind.Deconstruct && DeconstructTool.Target(Sim, Hover) is { } b) return DeconstructTool.Tooltip(Sim, b);
        return null;
    }

    /// <summary>Events for the HUD: the colony-lost modal, season changes (ECO-18) and refused commands.</summary>
    private void HandleHudEvent(SimEvent e)
    {
        switch (e)
        {
            case ColonyLost:
                _lostModal.Open(Sim.Clock.Day + 1);
                break;
            case SeasonChanged s:
                _hud.Toast(TopBarModel.SeasonMessage(s.Season));
                break;
            case CommandRejected r:
                _hud.Toast($"{r.Command} refused: {r.Reason}");
                break;
        }
    }

    /// <summary>After a new simulation is attached: the modal shows exactly when that colony is lost.</summary>
    private void SyncLostModal()
    {
        if (Sim.Agents.ColonyLost) _lostModal.Open(Sim.Clock.Day + 1);
        else _lostModal.Visible = false;
    }
}
