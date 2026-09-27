namespace Aurvangar.ViewCore.Hud;

/// <summary>One item of a wrapping HUD line: its width, and the width of the separator before it (", ", " · "), which
/// is dropped when the item starts a line.</summary>
public readonly record struct FlowItem(float Lead, float Width);

/// <summary>Where the top bar goes and how its items wrap (<see cref="HudLayout.ArrangeTopBar"/>): the line of each
/// first-row item (day, season, speed, totals, alerts) and of each plan-line item (VIEW-23), counted from the bar's
/// first line; the plan lines follow the first-row lines. <see cref="Beside"/> is false when the bar had to drop below
/// the toolbar.</summary>
public sealed record TopBarArrangement(ScreenRect Rect, bool Beside, int[] RowLines, int[] PlanLines, int LineCount);

/// <summary>Screen layout of the HUD top bar (VIEW-15, M9-T3). The bar sits at the top right beside the toolbar and
/// never overlaps it: its items wrap onto more lines (the first row by item, the plan line between items) so the bar
/// fits between the toolbar and the right edge; when a single item is too wide for that, the bar moves below the
/// toolbar and wraps to the screen width. Pure math; the Godot side measures the labels and applies the lines.</summary>
public static class HudLayout
{
    /// <summary>Space between the HUD and the screen edge.</summary>
    public const float Margin = 8f;
    /// <summary>Clear space between the toolbar and the top bar.</summary>
    public const float Gap = 12f;
    /// <summary>Space between first-row items.</summary>
    public const float RowSeparation = 18f;
    /// <summary>Height of one top-bar line (16 px font), used for the arrangement's rectangle.</summary>
    public const float LineHeight = 23f;

    /// <summary>Greedy wrap: the line index of each item. An item goes on the current line if it fits in
    /// <paramref name="maxWidth"/> with its lead and <paramref name="separation"/>; otherwise it starts the next line
    /// (without its lead). An item wider than the limit gets a line of its own.</summary>
    public static int[] Flow(IReadOnlyList<FlowItem> items, float maxWidth, float separation)
    {
        var lines = new int[items.Count];
        int line = 0;
        float x = 0f;
        for (int i = 0; i < items.Count; i++)
        {
            if (i == 0) { x = items[i].Width; continue; }
            float next = x + separation + items[i].Lead + items[i].Width;
            if (next <= maxWidth) x = next;
            else { line++; x = items[i].Width; }
            lines[i] = line;
        }
        return lines;
    }

    /// <summary>The width of each line of a <see cref="Flow"/> result.</summary>
    public static float[] LineWidths(IReadOnlyList<FlowItem> items, IReadOnlyList<int> lines, float separation)
    {
        int count = lines.Count == 0 ? 0 : lines[^1] + 1;
        var widths = new float[count];
        for (int i = 0; i < items.Count; i++)
        {
            bool first = i == 0 || lines[i - 1] != lines[i];
            widths[lines[i]] += first ? items[i].Width : separation + items[i].Lead + items[i].Width;
        }
        return widths;
    }

    /// <summary>Places the top bar for a viewport <paramref name="viewportWidth"/> wide with the toolbar at
    /// <paramref name="toolbar"/>. <paramref name="padding"/> is the bar's horizontal padding (panel margins). The bar
    /// is right-aligned at <see cref="Margin"/> from the edge, as wide as its widest line plus padding.</summary>
    public static TopBarArrangement ArrangeTopBar(float viewportWidth, ScreenRect toolbar, float padding,
        IReadOnlyList<FlowItem> row, IReadOnlyList<FlowItem> plan)
    {
        float right = viewportWidth - Margin;
        float beside = right - (toolbar.Right + Gap) - padding;
        float widest = row.Concat(plan).Select(i => i.Width).DefaultIfEmpty(0f).Max();
        bool isBeside = widest <= beside;
        float maxWidth = isBeside ? beside : right - Margin - padding;

        var rowLines = Flow(row, maxWidth, RowSeparation);
        var planLines = Flow(plan, maxWidth, 0f);
        var rowWidths = LineWidths(row, rowLines, RowSeparation);
        var planWidths = LineWidths(plan, planLines, 0f);
        int rowCount = rowWidths.Length;
        for (int i = 0; i < planLines.Length; i++) planLines[i] += rowCount;
        int lineCount = rowCount + planWidths.Length;

        float width = rowWidths.Concat(planWidths).DefaultIfEmpty(0f).Max() + padding;
        float y = isBeside ? Margin : toolbar.Bottom + Gap;
        var rect = new ScreenRect(right - width, y, width, lineCount * LineHeight);
        return new TopBarArrangement(rect, isBeside, rowLines, planLines, lineCount);
    }
}
