using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Tools;
using Godot;

namespace Aurvangar.Client;

/// <summary>Keyboard and mouse input of <see cref="GameRoot"/>: slice keys (VIEW-04), speed keys (VIEW-01), F3
/// (VIEW-17), F5/F9 (VIEW-19), tool hotkeys and tool drags (VIEW-12, VIEW-13), B and R for the build tool (VIEW-14).
/// The drag logic is <see cref="ToolController"/> (ViewCore); this file only forwards events and enqueues the
/// resulting command. A right click aborts a tool drag; a right-drag orbits the camera (CameraRig, M7-T1).</summary>
public partial class GameRoot
{
    public ToolKind Tool => _tool.Tool;

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey { Pressed: true } key:
                HandleKey(key);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                if (mb.Pressed) { if (!ClickToolPress()) _tool.Press(Hover); }
                else
                {
                    _build.Release();
                    if (_tool.Tool == ToolKind.Blocks) BlockRelease();
                    else if (_tool.Release(Hover, SliceY) is { } command) Sim.Enqueue(command);
                }
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                BlockWheel(wheel);
                break;
            // A right click (no drag movement) aborts the tool drag: CameraRig.RightClicked, wired in _Ready (M7-T1).
        }
    }

    private void HandleKey(InputEventKey key)
    {
        switch (key.Keycode)
        {
            // VIEW-04: slice keys repeat while held.
            case Key.Pageup or Key.Bracketright: Slice.Step(+1, Remesh); return;
            case Key.Pagedown or Key.Bracketleft: Slice.Step(-1, Remesh); return;
        }
        if (BlockKey(key)) return;   // Tab, P, +/- with the block tool (+/- repeat)
        if (key.Echo) return;
        switch (key.Keycode)
        {
            case Key.Space: SpeedIndex = SpeedIndex == 0 ? 1 : 0; return;
            case Key.Key1: SpeedIndex = 1; return;
            case Key.Key2: SpeedIndex = 2; return;
            case Key.Key3: SpeedIndex = 3; return;
            case Key.F3: _overlay.Toggle(); return;
            case Key.F5: QuickSaveNow(); return;
            case Key.F9: QuickLoadNow(); return;
            case Key.Escape: SetTool(ToolKind.Select); return;
            case Key.B: BuildHotkey(); return;
            case Key.R: RotateBuild(); return;
        }
        if (key.Keycode is >= Key.A and <= Key.Z && ToolController.ForHotkey((char)key.Keycode) is { } tool)
            SetTool(tool);
    }

    /// <summary>Switches the active tool (hotkey or toolbar button); a drag in progress is dropped.</summary>
    public void SetTool(ToolKind tool)
    {
        _tool.SetTool(tool);
        _build.Release();
        _blocks.Reset();
        _hud.SetTool(tool, _build.Def.Name);
        SyncBlockHud();
    }

    /// <summary>VIEW-16: a click on a colonist row centers the camera on that dwarf.</summary>
    private void CenterOnAgent(AgentId id)
    {
        var agent = Sim.Agents.Get(id);
        var rig = GetNode<CameraRig>("Camera").Rig;
        if (agent == null || rig == null) return;
        rig.CenterOn(AgentVisuals.Position(agent));
    }
}
