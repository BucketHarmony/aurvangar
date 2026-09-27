using System.Numerics;
using Aurvangar.ViewCore.Hud;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M7-T7: the mouse label (build ghost tooltip, pile counts, farm hints) never covers a building billboard
/// (the G3 build1600 shot had the ghost tooltip over the pump's NO WATER).</summary>
[Trait("Category", "Unit")]
public class LabelLayoutTests
{
    private static readonly Vector2 View = new(1600, 900);
    private static readonly Vector2 Size = new(260, 50);

    [Fact]
    public void NoObstacles_LabelSitsBelowRightOfThePoint()
    {
        var p = LabelLayout.PlaceTooltip(new Vector2(800, 450), Size, Array.Empty<ScreenRect>(), View);
        Assert.Equal(new Vector2(800, 450) + LabelLayout.MouseOffset, p);
    }

    [Fact]
    public void BillboardOverDefaultSpot_LabelMovesClear_StaysNearAndOnScreen()
    {
        var point = new Vector2(800, 450);
        var billboard = new ScreenRect(790, 460, 200, 30);   // right where the default label would go
        var p = LabelLayout.PlaceTooltip(point, Size, new[] { billboard }, View);
        var label = new ScreenRect(p.X, p.Y, Size.X, Size.Y);
        Assert.False(label.Intersects(billboard), $"label {label} overlaps {billboard}");
        AssertOnScreen(label);
        Assert.True(Vector2.Distance(p, point) < 150f, $"label at {p} drifted far from {point}");
    }

    [Fact]
    public void AllFourCornersBlocked_LabelStillFindsAClearSpot()
    {
        var point = new Vector2(800, 450);
        var avoid = new[]
        {
            new ScreenRect(700, 380, 400, 60),   // above
            new ScreenRect(700, 455, 400, 60),   // below
        };
        var p = LabelLayout.PlaceTooltip(point, Size, avoid, View);
        var label = new ScreenRect(p.X, p.Y, Size.X, Size.Y);
        foreach (var r in avoid) Assert.False(label.Intersects(r), $"label {label} overlaps {r}");
        AssertOnScreen(label);
    }

    [Fact]
    public void NearBottomRightCorner_LabelFlipsToStayOnScreen()
    {
        var p = LabelLayout.PlaceTooltip(new Vector2(1580, 880), Size, Array.Empty<ScreenRect>(), View);
        AssertOnScreen(new ScreenRect(p.X, p.Y, Size.X, Size.Y));
    }

    [Fact]
    public void NoClearSpot_PicksLeastOverlap_AndIsDeterministic()
    {
        var everything = new[] { new ScreenRect(0, 0, 1600, 900) };
        var a = LabelLayout.PlaceTooltip(new Vector2(800, 450), Size, everything, View);
        var b = LabelLayout.PlaceTooltip(new Vector2(800, 450), Size, everything, View);
        Assert.Equal(a, b);
        AssertOnScreen(new ScreenRect(a.X, a.Y, Size.X, Size.Y));
    }

    [Fact]
    public void BillboardScale_FixedSizeLabel_MatchesAbout20PxAt900High()
    {
        // PileRenderer: font 36, pixel size 0.0009, about 20 px tall on a 900 px view with the default 75 degree FOV.
        float k = LabelLayout.BillboardScale(0.0009f, 900f, 75f);
        Assert.InRange(36 * k, 18f, 21f);
        // Twice the window height, twice the pixels (fixed-size labels scale with the viewport, not the distance).
        Assert.Equal(2 * k, LabelLayout.BillboardScale(0.0009f, 1800f, 75f), 4);
    }

    [Fact]
    public void BillboardRect_IsBottomAlignedAndCentredOnTheAnchor()
    {
        var r = LabelLayout.BillboardRect(new Vector2(400, 300), new Vector2(100, 40), 0.5f);
        Assert.Equal(400f, r.X + r.W / 2f, 3);
        Assert.Equal(300f, r.Bottom, 3);
        Assert.Equal(50f, r.W, 3);
        Assert.Equal(20f, r.H, 3);
    }

    [Fact]
    public void Rects_IntersectOnlyWhenTheyShareArea()
    {
        var a = new ScreenRect(0, 0, 10, 10);
        Assert.True(a.Intersects(new ScreenRect(5, 5, 10, 10)));
        Assert.False(a.Intersects(new ScreenRect(10, 0, 10, 10)));   // touching edges do not overlap
        Assert.Equal(25f, a.OverlapArea(new ScreenRect(5, 5, 10, 10)), 3);
    }

    [Fact]
    public void Declutter_ApartLabels_StayPut()
    {
        var lifts = LabelLayout.Declutter(new[] { new ScreenRect(0, 0, 100, 20), new ScreenRect(200, 0, 100, 20) });
        Assert.Equal(new[] { 0f, 0f }, lifts);
    }

    [Fact]
    public void Declutter_OverlappingLabels_TheOneHigherOnScreenMovesUpClear()
    {
        // Two adjacent levee sites (tunnel shot): "Building Levee 25%" over "Levee: log 0/2".
        var near = new ScreenRect(600, 410, 160, 22);
        var far = new ScreenRect(585, 395, 175, 22);
        var lifts = LabelLayout.Declutter(new[] { far, near });
        Assert.Equal(0f, lifts[1]);          // the lower (nearer) label keeps its place
        Assert.True(lifts[0] > 0f);
        var moved = far with { Y = far.Y - lifts[0] };
        Assert.False(moved.Intersects(near), $"{moved} still overlaps {near}");
    }

    [Fact]
    public void Declutter_ThreeInAPile_NoneOverlap()
    {
        var rects = new[] { new ScreenRect(0, 100, 100, 20), new ScreenRect(10, 105, 100, 20), new ScreenRect(20, 110, 100, 20) };
        var lifts = LabelLayout.Declutter(rects);
        var placed = rects.Select((r, i) => r with { Y = r.Y - lifts[i] }).ToList();
        for (int i = 0; i < placed.Count; i++)
            for (int j = i + 1; j < placed.Count; j++)
                Assert.False(placed[i].Intersects(placed[j]), $"{placed[i]} overlaps {placed[j]}");
        Assert.All(lifts, l => Assert.True(l >= 0f));
    }

    private static void AssertOnScreen(ScreenRect r)
    {
        Assert.True(r.X >= 0 && r.Y >= 0 && r.Right <= View.X && r.Bottom <= View.Y, $"label {r} is off screen");
    }
}
