using Aurvangar.ViewCore.Camera;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M7-T1: right-drag orbits the camera like middle-drag; a right click without drag movement still aborts
/// the tool drag (VIEW-06, VIEW-13, ADR-054).</summary>
public class ClickDragGestureTests
{
    [Fact]
    public void PressRelease_NoMovement_IsClick()
    {
        var g = new ClickDragGesture();
        g.Press();
        Assert.True(g.Held);
        Assert.True(g.Release());
        Assert.False(g.Held);
    }

    [Fact]
    public void SmallJitter_WithinThreshold_StillClick_AndDoesNotMoveCamera()
    {
        var g = new ClickDragGesture();
        g.Press();
        Assert.Equal((0f, 0f), g.Move(2, 1));
        Assert.Equal((0f, 0f), g.Move(-1, 2));
        Assert.False(g.IsDragging);
        Assert.True(g.Release());
    }

    [Fact]
    public void MovementPastThreshold_IsDrag_NotClick()
    {
        var g = new ClickDragGesture();
        g.Press();
        g.Move(ClickDragGesture.ThresholdPixels + 1, 0);
        Assert.True(g.IsDragging);
        Assert.False(g.Release());
    }

    [Fact]
    public void ThresholdCrossing_ReturnsAccumulatedDelta_ThenPassesMotionThrough()
    {
        var g = new ClickDragGesture();
        g.Press();
        Assert.Equal((0f, 0f), g.Move(3, 0));
        // Crossing the threshold hands over everything moved since the press, so the drag does not lose pixels.
        Assert.Equal((6f, 1f), g.Move(3, 1));
        Assert.Equal((-2f, 5f), g.Move(-2, 5));
    }

    [Fact]
    public void JitterBackAndForth_UsesNetDisplacement()
    {
        var g = new ClickDragGesture();
        g.Press();
        for (int i = 0; i < 10; i++)
        {
            g.Move(3, 0);
            g.Move(-3, 0);
        }
        Assert.False(g.IsDragging);
        Assert.True(g.Release());
    }

    [Fact]
    public void MoveWhileNotHeld_Ignored_ReleaseWithoutPress_NotClick()
    {
        var g = new ClickDragGesture();
        Assert.Equal((0f, 0f), g.Move(50, 50));
        Assert.False(g.Release());
    }

    [Fact]
    public void NewPress_ResetsPreviousDrag()
    {
        var g = new ClickDragGesture();
        g.Press();
        g.Move(20, 0);
        Assert.False(g.Release());
        g.Press();
        Assert.False(g.IsDragging);
        Assert.True(g.Release());
    }

    [Fact]
    public void RightDrag_OrbitsLikeMiddleDrag()
    {
        var viaMiddle = new OrbitRig(128, 128, new System.Numerics.Vector3(64, 20, 64), 20);
        var viaRight = new OrbitRig(128, 128, new System.Numerics.Vector3(64, 20, 64), 20);
        (float x, float y)[] motion = [(2, 1), (5, 2), (10, -3), (-4, 6)];

        foreach (var (x, y) in motion) viaMiddle.Drag(x, y);

        var g = new ClickDragGesture();
        g.Press();
        foreach (var (x, y) in motion)
        {
            var (dx, dy) = g.Move(x, y);
            if (dx != 0 || dy != 0) viaRight.Drag(dx, dy);
        }
        Assert.False(g.Release());

        Assert.Equal(viaMiddle.Yaw, viaRight.Yaw, 3);
        Assert.Equal(viaMiddle.Pitch, viaRight.Pitch, 3);
    }
}
