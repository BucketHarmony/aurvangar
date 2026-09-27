namespace Aurvangar.ViewCore.Camera;

/// <summary>Tells a click from a drag for one mouse button (M7-T1, ADR-054). The right button orbits the camera like
/// the middle button once the pointer has moved more than <see cref="ThresholdPixels"/> from where it was pressed;
/// a release before that is a click (which aborts the tool drag in progress). Engine-neutral; the Godot
/// <c>CameraRig</c> feeds it button and motion events.</summary>
public sealed class ClickDragGesture
{
    /// <summary>Net pointer displacement (pixels) since the press above which the gesture becomes a drag.</summary>
    public const float ThresholdPixels = 4f;

    private float _accX, _accY;

    /// <summary>The button is down (between <see cref="Press"/> and <see cref="Release"/>).</summary>
    public bool Held { get; private set; }

    /// <summary>The pointer has passed the threshold since the press; motion now goes to the camera.</summary>
    public bool IsDragging { get; private set; }

    public void Press()
    {
        Held = true;
        IsDragging = false;
        _accX = 0;
        _accY = 0;
    }

    /// <summary>Pointer motion while the button may be held. Returns the delta to apply to the camera: zero until the
    /// threshold is crossed, then everything moved since the press (so no pixels are lost), then each motion as is.</summary>
    public (float Dx, float Dy) Move(float dx, float dy)
    {
        if (!Held) return (0f, 0f);
        if (IsDragging) return (dx, dy);
        _accX += dx;
        _accY += dy;
        if (_accX * _accX + _accY * _accY <= ThresholdPixels * ThresholdPixels) return (0f, 0f);
        IsDragging = true;
        return (_accX, _accY);
    }

    /// <summary>Button up. Returns true when this was a click: pressed and released without becoming a drag.</summary>
    public bool Release()
    {
        bool click = Held && !IsDragging;
        Held = false;
        IsDragging = false;
        return click;
    }
}
