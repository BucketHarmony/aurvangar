using Aurvangar.ViewCore.Camera;
using Godot;
using NVec3 = System.Numerics.Vector3;

namespace Aurvangar.Client;

/// <summary>Orbit camera (VIEW-06). Input only; the math lives in <see cref="OrbitRig"/> (ViewCore, unit-tested).
/// WASD/arrows pan, Q/E rotate 90 degree steps, wheel zooms, middle-drag or right-drag orbits (M7-T1). A right click
/// without drag movement (<see cref="ClickDragGesture"/>) raises <see cref="RightClicked"/>, which GameRoot uses to
/// abort a tool drag. GameRoot calls <see cref="Init"/>; until then the camera keeps its scene transform.</summary>
public partial class CameraRig : Camera3D
{
    public OrbitRig? Rig { get; private set; }

    private System.Func<int> _sliceY = () => int.MaxValue;
    private bool _dragging;
    private readonly ClickDragGesture _right = new();

    /// <summary>Right button pressed and released without passing the drag threshold.</summary>
    public event System.Action? RightClicked;

    public void Init(OrbitRig rig, System.Func<int> sliceY)
    {
        Rig = rig;
        _sliceY = sliceY;
        Apply();
    }

    public override void _Process(double delta)
    {
        if (Rig == null) return;
        float right = Axis(Key.D, Key.Right) - Axis(Key.A, Key.Left);
        float forward = Axis(Key.W, Key.Up) - Axis(Key.S, Key.Down);
        if (right != 0 || forward != 0) Rig.Pan(right, forward, (float)delta);
        Rig.Update((float)delta, _sliceY());
        Apply();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (Rig == null) return;
        switch (e)
        {
            case InputEventKey { Pressed: true, Echo: false } key:
                if (key.Keycode == Key.Q) Rig.RotateStep(-1);
                else if (key.Keycode == Key.E) Rig.RotateStep(+1);
                break;
            case InputEventMouseButton mb:
                if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelUp) Rig.Zoom(+1);
                else if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelDown) Rig.Zoom(-1);
                else if (mb.ButtonIndex == MouseButton.Middle) _dragging = mb.Pressed;
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    if (mb.Pressed) _right.Press();
                    else if (_right.Release()) RightClicked?.Invoke();
                }
                break;
            case InputEventMouseMotion motion:
                if (_dragging) Rig.Drag(motion.Relative.X, motion.Relative.Y);
                else if (_right.Move(motion.Relative.X, motion.Relative.Y) is var (dx, dy) && (dx != 0 || dy != 0))
                    Rig.Drag(dx, dy);
                break;
        }
    }

    private static float Axis(Key a, Key b) => Input.IsKeyPressed(a) || Input.IsKeyPressed(b) ? 1f : 0f;

    /// <summary>Moves the camera to the rig's current view immediately (screenshot harness).</summary>
    public void ApplyNow() => Apply();

    private void Apply()
    {
        if (Rig == null) return;
        LookAtFromPosition(ToGodot(Rig.CameraPosition), ToGodot(Rig.Focus), Vector3.Up);
    }

    public static Vector3 ToGodot(NVec3 v) => new(v.X, v.Y, v.Z);
    public static NVec3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);
}
