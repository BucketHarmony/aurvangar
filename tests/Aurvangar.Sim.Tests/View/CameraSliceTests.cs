using System.Numerics;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Camera;
using Aurvangar.ViewCore.Frame;
using Aurvangar.ViewCore.Picking;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M3-T5: orbit camera rig (VIEW-06).</summary>
public class OrbitRigTests
{
    private static OrbitRig NewRig() => new(128, 128, new Vector3(64, 20, 64), baseFocusY: 20);

    [Fact]
    public void Zoom_ClampedToTenAndHundredTwenty()
    {
        var rig = NewRig();
        for (int i = 0; i < 100; i++) rig.Zoom(+1);
        Assert.Equal(OrbitRig.MinDistance, rig.Distance, 3);
        for (int i = 0; i < 100; i++) rig.Zoom(-1);
        Assert.Equal(OrbitRig.MaxDistance, rig.Distance, 3);
        Assert.Equal(10f, OrbitRig.MinDistance);
        Assert.Equal(120f, OrbitRig.MaxDistance);
    }

    [Fact]
    public void Drag_PitchClampedTo25And80()
    {
        var rig = NewRig();
        rig.Drag(0, 10_000);
        Assert.Equal(80f, rig.Pitch, 3);
        rig.Drag(0, -10_000);
        Assert.Equal(25f, rig.Pitch, 3);
    }

    [Fact]
    public void RotateStep_Tweens90DegreesInPointTwoSeconds()
    {
        var rig = NewRig();
        float start = rig.Yaw;
        rig.RotateStep(+1);
        Assert.Equal(start + 90f, rig.YawTarget, 3);
        rig.Update(0.1f, sliceY: 63);
        Assert.Equal(start + 45f, rig.Yaw, 2);     // halfway after 0.1 s
        rig.Update(0.1f, sliceY: 63);
        Assert.Equal(start + 90f, rig.Yaw, 2);     // done after 0.2 s
        rig.Update(1f, sliceY: 63);
        Assert.Equal(start + 90f, rig.Yaw, 2);     // no overshoot
        rig.RotateStep(-1);
        rig.RotateStep(-1);
        rig.Update(1f, sliceY: 63);
        Assert.Equal(start - 90f, rig.Yaw, 2);
    }

    [Fact]
    public void Pan_MovesOnXZRelativeToYaw_AndStaysInWorld()
    {
        var rig = NewRig();
        rig.SetYaw(0);                              // camera on +Z of focus, looking toward -Z
        var f0 = rig.Focus;
        rig.Pan(0, 1, 0.1f);                        // forward = -Z
        Assert.True(rig.Focus.Z < f0.Z);
        Assert.Equal(f0.X, rig.Focus.X, 3);
        Assert.Equal(f0.Y, rig.Focus.Y, 3);
        var f1 = rig.Focus;
        rig.Pan(1, 0, 0.1f);                        // right = +X
        Assert.True(rig.Focus.X > f1.X);
        Assert.Equal(f1.Z, rig.Focus.Z, 3);

        rig.Pan(-1, 1, 1000f);
        Assert.Equal(0f, rig.Focus.X, 3);
        Assert.Equal(0f, rig.Focus.Z, 3);
    }

    [Fact]
    public void CameraPosition_IsDistanceFromFocus_AtPitch()
    {
        var rig = NewRig();
        rig.SetYaw(0);
        var offset = rig.CameraPosition - rig.Focus;
        Assert.Equal(rig.Distance, offset.Length(), 2);
        float pitch = MathF.Asin(offset.Y / offset.Length()) * 180f / MathF.PI;
        Assert.Equal(rig.Pitch, pitch, 2);
        Assert.True(offset.Z > 0);
        Assert.Equal(0f, offset.X, 3);
    }

    [Fact]
    public void Focus_FollowsSliceLevelHeight()
    {
        var rig = NewRig();
        rig.Update(10f, sliceY: 63);                 // not sliced: stays at base height
        Assert.Equal(20f, rig.Focus.Y, 2);
        rig.Update(10f, sliceY: 9);                  // sliced below base: focus drops onto the slice
        Assert.Equal(10f, rig.Focus.Y, 2);
        rig.Update(0.01f, sliceY: 19);               // moves smoothly, not instantly
        Assert.True(rig.Focus.Y > 10f && rig.Focus.Y < 20f);
        rig.Update(10f, sliceY: 40);
        Assert.Equal(20f, rig.Focus.Y, 2);
    }
}

/// <summary>M3-T5: slice controller (VIEW-04) and picking (VIEW-05).</summary>
public class SliceAndPickTests
{
    [Fact]
    public void Slice_DefaultsToTop_ClampsAndReportsChange()
    {
        var router = new RemeshRouter(4, 2, 4);
        var slice = new SliceController(64);
        Assert.Equal(63, slice.SliceY);
        Assert.False(slice.IsSliced);
        Assert.False(slice.Step(+1, router));        // already at the top
        Assert.Equal(0, router.Terrain.Count);
        Assert.True(slice.Step(-1, router));
        Assert.Equal(62, slice.SliceY);
        Assert.True(slice.IsSliced);
        for (int i = 0; i < 100; i++) slice.Step(-1, router);
        Assert.Equal(0, slice.SliceY);
    }

    [Fact]
    public void SliceChange_RemeshesOnlyChunkLayersOfOldAndNewSlice()
    {
        var router = new RemeshRouter(4, 2, 4);
        var slice = new SliceController(64);
        var batch = new List<int>();

        slice.Step(-1, router);                       // 63 -> 62: both in chunk layer 1
        router.Terrain.TakeBatch(100, batch);
        Assert.Equal(Enumerable.Range(16, 16), batch.OrderBy(i => i));
        router.Water.TakeBatch(100, batch);
        Assert.Equal(Enumerable.Range(16, 16), batch.OrderBy(i => i));

        slice.Set(32, router);
        router.Terrain.TakeBatch(100, batch);
        router.Water.TakeBatch(100, batch);
        slice.Set(31, router);                        // 32 -> 31 crosses the layer border: both layers
        router.Terrain.TakeBatch(100, batch);
        Assert.Equal(Enumerable.Range(0, 32), batch.OrderBy(i => i));
        router.Water.TakeBatch(100, batch);
        Assert.Equal(32, batch.Count);
    }

    [Fact]
    public void Pick_TopFace_ReturnsCellBelowHitAndUpNormal()
    {
        var world = new VoxelWorld(32, 32, 32, TestContent.Db.SolidTable);
        var hit = PickResolver.Resolve(world, new Vector3(3.4f, 5f, 7.9f), new Vector3(0, 1, 0), sliceY: 31);
        Assert.NotNull(hit);
        Assert.Equal(new Int3(3, 4, 7), hit!.Value.Cell);
        Assert.Equal(Int3.Up, hit.Value.Normal);
        Assert.Equal(new Int3(3, 5, 7), hit.Value.Adjacent);
    }

    [Fact]
    public void Pick_SideFaces_UseNormalToChooseTheSolidCell()
    {
        var world = new VoxelWorld(32, 32, 32, TestContent.Db.SolidTable);
        // East face of cell (3,4,7) sits at x = 4.
        var east = PickResolver.Resolve(world, new Vector3(4f, 4.5f, 7.5f), new Vector3(0.99f, 0.1f, 0), 31);
        Assert.Equal(new Int3(3, 4, 7), east!.Value.Cell);
        Assert.Equal(Int3.East, east.Value.Normal);
        // North face (-Z) of cell (3,4,7) sits at z = 7.
        var north = PickResolver.Resolve(world, new Vector3(3.5f, 4.5f, 7f), new Vector3(0, 0, -1), 31);
        Assert.Equal(new Int3(3, 4, 7), north!.Value.Cell);
        Assert.Equal(Int3.North, north.Value.Normal);
    }

    [Fact]
    public void Pick_AboveSlice_OrOutOfWorld_IsIgnored()
    {
        var world = new VoxelWorld(32, 32, 32, TestContent.Db.SolidTable);
        Assert.Null(PickResolver.Resolve(world, new Vector3(3.5f, 11f, 3.5f), Vector3.UnitY, sliceY: 9));
        Assert.NotNull(PickResolver.Resolve(world, new Vector3(3.5f, 10f, 3.5f), Vector3.UnitY, sliceY: 9));
        Assert.Null(PickResolver.Resolve(world, new Vector3(-0.5f, 10f, 3.5f), Vector3.UnitY, sliceY: 31));
    }
}
