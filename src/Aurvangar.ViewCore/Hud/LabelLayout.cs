using System.Numerics;

namespace Aurvangar.ViewCore.Hud;

/// <summary>An axis-aligned rectangle in screen pixels (origin top left, Y down).</summary>
public readonly record struct ScreenRect(float X, float Y, float W, float H)
{
    public float Right => X + W;
    public float Bottom => Y + H;

    /// <summary>True when the two rectangles share area (touching edges do not count).</summary>
    public bool Intersects(ScreenRect o) => X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;

    public float OverlapArea(ScreenRect o)
    {
        float w = MathF.Min(Right, o.Right) - MathF.Max(X, o.X);
        float h = MathF.Min(Bottom, o.Bottom) - MathF.Max(Y, o.Y);
        return w > 0 && h > 0 ? w * h : 0f;
    }

    public ScreenRect Inflate(float d) => new(X - d, Y - d, W + 2 * d, H + 2 * d);

    /// <summary>The smallest rectangle holding every point (projected corners of a 3D box, M9-T3).</summary>
    public static ScreenRect Bounding(IEnumerable<Vector2> points)
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var p in points)
        {
            x0 = MathF.Min(x0, p.X); y0 = MathF.Min(y0, p.Y);
            x1 = MathF.Max(x1, p.X); y1 = MathF.Max(y1, p.Y);
        }
        return x0 > x1 ? default : new ScreenRect(x0, y0, x1 - x0, y1 - y0);
    }
}

/// <summary>Screen layout of the mouse label (VIEW-14/15, M7-T7): the build ghost tooltip, pile counts and farm hints
/// are placed next to the mouse (or the harness's fixed pick) where they cover no building billboard (its name and
/// progress label, progress bar or NO WATER icon) and stay on screen. Pure math; the Godot side projects the
/// billboards to <see cref="ScreenRect"/>s.</summary>
public static class LabelLayout
{
    /// <summary>The label's default offset from the point: below and to the right of the mouse cursor.</summary>
    public static readonly Vector2 MouseOffset = new(16, 12);

    /// <summary>Clear space kept between the label and a billboard.</summary>
    public const float Gap = 4f;

    /// <summary>Screen pixels per font pixel of a fixed-size, billboarded Label3D: Godot scales such a label by its view
    /// depth, so its height on screen is <c>pixelSize * fontPx * viewportHeight / (2 tan(fov / 2))</c> whatever the
    /// camera distance (perspective camera, vertical FOV in degrees).</summary>
    public static float BillboardScale(float pixelSize, float viewportHeight, float fovYDegrees) =>
        pixelSize * viewportHeight / (2f * MathF.Tan(fovYDegrees * MathF.PI / 360f));

    /// <summary>A bottom-aligned, horizontally centred billboard label at the projected <paramref name="anchor"/>:
    /// <paramref name="fontSize"/> is its text size in font pixels (outline included), scaled by
    /// <see cref="BillboardScale"/>.</summary>
    public static ScreenRect BillboardRect(Vector2 anchor, Vector2 fontSize, float scale)
    {
        float w = fontSize.X * scale, h = fontSize.Y * scale;
        return new ScreenRect(anchor.X - w / 2f, anchor.Y - h, w, h);
    }

    /// <summary>Top-left corner for a label of <paramref name="size"/> pointing at <paramref name="point"/>. Tries
    /// below-right (the default, <see cref="MouseOffset"/>), above-right, below-left, above-left, then just below or
    /// above each billboard in the way; the first spot on screen that clears every <paramref name="avoid"/> rectangle
    /// by <see cref="Gap"/> wins, nearest to the default first. With no clear spot, the one with the least overlap
    /// (earliest on ties). Always clamped on screen. Deterministic.</summary>
    public static Vector2 PlaceTooltip(Vector2 point, Vector2 size, IReadOnlyList<ScreenRect> avoid, Vector2 viewport)
    {
        var right = point.X + MouseOffset.X;
        var left = point.X - MouseOffset.X - size.X;
        var below = point.Y + MouseOffset.Y;
        var above = point.Y - MouseOffset.Y - size.Y;
        var home = new Vector2(right, below);
        var candidates = new List<Vector2>
        {
            home, new(right, above), new(left, below), new(left, above),
        };
        foreach (var r in avoid)
            foreach (float x in new[] { right, left })
            {
                candidates.Add(new Vector2(x, r.Bottom + Gap));
                candidates.Add(new Vector2(x, r.Y - Gap - size.Y));
            }

        Vector2? best = null;
        float bestDistance = float.MaxValue;
        foreach (var c in candidates)
        {
            var p = Clamp(c, size, viewport);
            if (Overlap(p, size, avoid) > 0f) continue;
            float d = Vector2.DistanceSquared(p, home);
            if (d < bestDistance) { best = p; bestDistance = d; }
        }
        if (best is { } clear) return clear;

        var least = Clamp(home, size, viewport);
        float leastOverlap = Overlap(least, size, avoid);
        foreach (var c in candidates)
        {
            var p = Clamp(c, size, viewport);
            float o = Overlap(p, size, avoid);
            if (o < leastOverlap) { least = p; leastOverlap = o; }
        }
        return least;
    }

    /// <summary>Screen pixels each billboard label moves up so no two overlap (M7-T7; e.g. the two breach levee sites
    /// side by side). Labels are placed lowest on screen first (the nearer ones, ties by index), and each one that
    /// overlaps an already placed label moves up just above it, repeatedly, until it is clear. Returns a lift of 0 or
    /// more per input rectangle.</summary>
    public static float[] Declutter(IReadOnlyList<ScreenRect> labels)
    {
        var lifts = new float[labels.Count];
        var order = Enumerable.Range(0, labels.Count).OrderByDescending(i => labels[i].Bottom).ThenBy(i => i).ToList();
        var placed = new List<ScreenRect>(labels.Count);
        foreach (int i in order)
        {
            var r = labels[i];
            for (int guard = 0; guard <= placed.Count; guard++)
            {
                int hit = placed.FindIndex(p => p.Intersects(r));
                if (hit < 0) break;
                r = r with { Y = placed[hit].Y - r.H };
            }
            lifts[i] = labels[i].Y - r.Y;
            placed.Add(r);
        }
        return lifts;
    }

    private static float Overlap(Vector2 p, Vector2 size, IReadOnlyList<ScreenRect> avoid)
    {
        var label = new ScreenRect(p.X, p.Y, size.X, size.Y);
        float sum = 0f;
        foreach (var r in avoid) sum += label.OverlapArea(r.Inflate(Gap));
        return sum;
    }

    private static Vector2 Clamp(Vector2 p, Vector2 size, Vector2 viewport) => new(
        Math.Clamp(p.X, 0f, MathF.Max(0f, viewport.X - size.X)),
        Math.Clamp(p.Y, 0f, MathF.Max(0f, viewport.Y - size.Y)));
}
