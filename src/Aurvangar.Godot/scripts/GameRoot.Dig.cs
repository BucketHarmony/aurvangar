using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Tools;
using Godot;

namespace Aurvangar.Client;

/// <summary>Digging in <see cref="GameRoot"/> (M11-T8): the dig tool's stair-down mode (VIEW-24, T or the dig options
/// row) with its preview of the cells a stair drag digs, the mouse label that says why a dig mark waits (VIEW-25), and
/// the view level line (VIEW-26). The rules are <see cref="StairDig"/>, <see cref="DigHover"/> and
/// <see cref="SliceHint"/> (ViewCore).</summary>
public partial class GameRoot
{
    private static readonly System.Numerics.Vector4 StairMark = new(1f, 0.6f, 0.24f, 0.4f);

    private TranslucentMesh _stairView = null!;
    private List<Int3>? _stairShown;
    private (Int3 Cell, long Tick, int Slice, string? Text) _digHover = (new Int3(-1, -1, -1), -1, -1, null);

    private void ReadyDigTools()
    {
        _stairView = new TranslucentMesh { Name = "StairPreview" };
        AddChild(_stairView);
        _hud.DigModeChosen += SetDigMode;
    }

    /// <summary>VIEW-24: sets the dig mode (and picks the dig tool).</summary>
    public void SetDigMode(DigMode mode)
    {
        if (_tool.Tool != ToolKind.Dig) SetTool(ToolKind.Dig);
        _tool.SetDigMode(mode);
        SyncDigHud();
    }

    /// <summary>T with the dig tool: box or stair; true when the key was used.</summary>
    private bool DigKey(InputEventKey key)
    {
        if (_tool.Tool != ToolKind.Dig || key.Echo || key.Keycode != Key.T) return false;
        _tool.ToggleDigMode();
        SyncDigHud();
        return true;
    }

    private void SyncDigHud() => _hud.SetDigOptions(_tool.Tool, _tool.DigMode);

    /// <summary>Physics frame: the cells a held stair drag would dig (re-uploaded only when they change).</summary>
    private void UpdateStairPreview()
    {
        var cells = _tool.StairPreview(Sim.World, SliceY);
        if (cells is null && _stairShown is null) return;
        if (cells is not null && _stairShown is not null && cells.SequenceEqual(_stairShown)) return;
        _stairShown = cells;
        _stairView.SetData(cells is null ? null : BlockGhostMesher.Marks(cells, StairMark));
    }

    /// <summary>The held stair drag's label, else why the hovered dig mark waits (one status query per cell and tick).</summary>
    private string? DigTooltip()
    {
        if (_tool.StairText(SliceY) is { } stair) return stair;
        if (Hover is not { } h) return null;
        if (_digHover.Cell != h.Cell || _digHover.Tick != Sim.Clock.Tick || _digHover.Slice != SliceY)
            _digHover = (h.Cell, Sim.Clock.Tick, SliceY, DigHover.For(Sim, h, SliceY));
        return _digHover.Text;
    }

    /// <summary>Screenshot harness (M11-T8): the dig tool in stair mode, hovering <paramref name="pick"/>.</summary>
    public void ShowStairTool(PickHit? pick)
    {
        SetTool(ToolKind.Dig);
        SetDigMode(DigMode.StairDown);
        PickOverride = pick;
    }
}
