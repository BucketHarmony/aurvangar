namespace Aurvangar.ViewCore.Hud;

/// <summary>VIEW-26 (M11-T8, G6 follow-up "Is there a slice view?"): the HUD line that names the view level (VIEW-04)
/// and its keys, so the slice view can be found.</summary>
public static class SliceHint
{
    public const string Keys = "PageUp/PageDown or [ ]";

    public static string Text(int sliceY, int maxY) =>
        sliceY >= maxY
            ? $"View level: top ({maxY}) · {Keys} to slice"
            : $"View level: {sliceY} of {maxY} ({maxY - sliceY} down) · {Keys} to move";
}
