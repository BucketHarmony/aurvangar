using System.Numerics;

namespace Aurvangar.ViewCore.Camera;

/// <summary>Engine-neutral orbit camera state (VIEW-06). The camera orbits <see cref="Focus"/> at
/// <see cref="Distance"/>, <see cref="Pitch"/> degrees above the horizon, <see cref="Yaw"/> degrees around +Y
/// (yaw 0 = camera on the +Z side of the focus, looking toward -Z). WASD pans the focus on the XZ plane relative to
/// the view direction, Q/E rotate in 90 degree steps tweened over 0.2 s, the wheel zooms, middle-drag
/// (or right-drag, M7-T1) orbits.
/// The focus height eases toward the slice level when the slice is below the base height (ADR-019).</summary>
public sealed class OrbitRig
{
    public const float MinDistance = 10f;
    public const float MaxDistance = 120f;
    public const float MinPitch = 25f;
    public const float MaxPitch = 80f;
    public const float RotateStepDegrees = 90f;
    public const float RotateSeconds = 0.2f;
    /// <summary>Distance multiplier per wheel step toward the focus.</summary>
    public const float ZoomFactor = 0.9f;
    /// <summary>Pan speed in cells per second per unit of distance.</summary>
    public const float PanSpeedPerDistance = 1f;
    public const float DragDegreesPerPixel = 0.3f;
    /// <summary>Exponential rate (1/s) at which the focus height approaches its target.</summary>
    public const float FocusFollowRate = 8f;

    private readonly float _sizeX, _sizeZ;
    private Vector3 _focus;

    public OrbitRig(float sizeX, float sizeZ, Vector3 focus, float baseFocusY)
    {
        _sizeX = sizeX;
        _sizeZ = sizeZ;
        _focus = focus;
        BaseFocusY = baseFocusY;
    }

    public Vector3 Focus => _focus;
    /// <summary>Focus height when the slice is not below it (e.g. the spawn flat surface).</summary>
    public float BaseFocusY { get; set; }
    public float Yaw { get; private set; } = 45f;
    public float YawTarget { get; private set; } = 45f;
    public float Pitch { get; private set; } = 45f;
    public float Distance { get; private set; } = 60f;

    public void SetYaw(float degrees) { Yaw = degrees; YawTarget = degrees; }

    /// <summary>Jumps to a view (screenshot presets, VIEW-20). Pitch, distance and focus XZ are clamped like user
    /// input; the focus height also becomes <see cref="BaseFocusY"/>.</summary>
    public void SetView(Vector3 focus, float yaw, float pitch, float distance)
    {
        _focus = new Vector3(Math.Clamp(focus.X, 0f, _sizeX), focus.Y, Math.Clamp(focus.Z, 0f, _sizeZ));
        BaseFocusY = focus.Y;
        SetYaw(yaw);
        Pitch = Math.Clamp(pitch, MinPitch, MaxPitch);
        Distance = Math.Clamp(distance, MinDistance, MaxDistance);
    }

    /// <summary>VIEW-16: centers the view on a point (a colonist) without changing yaw, pitch or zoom. The point's
    /// height becomes <see cref="BaseFocusY"/>; <see cref="Update"/> eases the focus height toward it.</summary>
    public void CenterOn(Vector3 point)
    {
        _focus.X = Math.Clamp(point.X, 0f, _sizeX);
        _focus.Z = Math.Clamp(point.Z, 0f, _sizeZ);
        BaseFocusY = point.Y;
    }

    /// <summary>Pans the focus. <paramref name="right"/> and <paramref name="forward"/> are input axes in -1..1.</summary>
    public void Pan(float right, float forward, float dt)
    {
        float yaw = Yaw * MathF.PI / 180f;
        var fwd = new Vector3(-MathF.Sin(yaw), 0, -MathF.Cos(yaw));
        var rgt = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
        var move = (fwd * forward + rgt * right) * (PanSpeedPerDistance * Distance * dt);
        _focus.X = Math.Clamp(_focus.X + move.X, 0f, _sizeX);
        _focus.Z = Math.Clamp(_focus.Z + move.Z, 0f, _sizeZ);
    }

    /// <summary>Q (-1) / E (+1): turn the yaw target by 90 degrees; <see cref="Update"/> tweens toward it.</summary>
    public void RotateStep(int direction) => YawTarget += Math.Sign(direction) * RotateStepDegrees;

    /// <summary>Positive steps zoom in (wheel up), negative zoom out.</summary>
    public void Zoom(int steps) =>
        Distance = Math.Clamp(Distance * MathF.Pow(ZoomFactor, steps), MinDistance, MaxDistance);

    /// <summary>Middle- or right-drag orbit: horizontal pixels turn the yaw, vertical pixels change the pitch (clamped).</summary>
    public void Drag(float dxPixels, float dyPixels)
    {
        SetYaw(Yaw - dxPixels * DragDegreesPerPixel);
        Pitch = Math.Clamp(Pitch + dyPixels * DragDegreesPerPixel, MinPitch, MaxPitch);
    }

    /// <summary>Focus height target for a slice level: on the slice when it is below the base height.</summary>
    public float FocusTargetY(int sliceY) => MathF.Min(sliceY + 1, BaseFocusY);

    /// <summary>Advances the yaw tween and eases the focus height toward <see cref="FocusTargetY"/>.</summary>
    public void Update(float dt, int sliceY)
    {
        float maxStep = RotateStepDegrees / RotateSeconds * dt;
        float diff = YawTarget - Yaw;
        Yaw = MathF.Abs(diff) <= maxStep ? YawTarget : Yaw + MathF.Sign(diff) * maxStep;

        float target = FocusTargetY(sliceY);
        float t = MathF.Min(1f, dt * FocusFollowRate);
        _focus.Y += (target - _focus.Y) * t;
        if (MathF.Abs(target - _focus.Y) < 0.001f) _focus.Y = target;
    }

    public Vector3 CameraPosition
    {
        get
        {
            float yaw = Yaw * MathF.PI / 180f, pitch = Pitch * MathF.PI / 180f;
            float h = MathF.Cos(pitch) * Distance;
            return _focus + new Vector3(MathF.Sin(yaw) * h, MathF.Sin(pitch) * Distance, MathF.Cos(yaw) * h);
        }
    }
}
