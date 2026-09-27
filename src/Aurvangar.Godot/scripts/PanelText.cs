using Godot;

namespace Aurvangar.Client;

/// <summary>Label helpers shared by the M11-T6 panels.</summary>
public static class PanelText
{
    public static Label Label(string text, int size, Color? color = null)
    {
        var l = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        if (color is { } c) l.AddThemeColorOverride("font_color", c);
        return l;
    }

    public static void Set(Label l, string text)
    {
        if (l.Text != text) l.Text = text;
    }
}
