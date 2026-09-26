using System.Numerics;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Camera;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Picking;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>Item piles (VIEW-10, M4-T16, gate G2 answer 3): a fixed-size marker in the item's color readable at the
/// default hub zoom, a count label above it, and a post down to the floor when the ground under the pile was dug.</summary>
[Trait("Category", "Unit")]
public class PileViewTests
{
    private static readonly EntityColors Colors = new(TestContent.Db);

    [Fact]
    public void Marker_IsFixedSize_WhateverTheCount()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Pile(new Int3(2, 5, 2), "log", 1).Pile(new Int3(6, 5, 2), "log", 500).Build();
        var markers = PileMesher.Markers(sim, 15, Colors);
        Assert.Equal(2, markers.Count);
        var one = PileMesher.Build(new[] { markers[0] });
        var many = PileMesher.Build(new[] { markers[1] });
        Assert.Equal(one.QuadCount, many.QuadCount);
        var (lo1, hi1) = Bounds(one.Positions);
        var (lo2, hi2) = Bounds(many.Positions);
        Assert.True(Vector3.Distance(hi1 - lo1, hi2 - lo2) < 1e-4f, "same size for 1 and 500 items");
        Assert.True(Vector3.Distance(lo1 + new Vector3(4, 0, 0), lo2) < 1e-4f);

        // Big enough to read at the default hub zoom: most of a cell wide, about half a cell tall, on the floor.
        Assert.True(hi1.X - lo1.X >= 0.7f, $"marker width {hi1.X - lo1.X}");
        Assert.True(hi1.Z - lo1.Z >= 0.7f);
        Assert.InRange(hi1.Y - lo1.Y, 0.45f, 0.8f);
        Assert.Equal(5f, lo1.Y);
        Assert.True(lo1.X >= 2f && hi1.X <= 3f && lo1.Z >= 2f && hi1.Z <= 3f, "the marker stays inside its cell");
    }

    [Fact]
    public void Marker_InItemColor_WithLighterCap_OutwardWinding()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Pile(new Int3(2, 5, 2), "stone", 12).Pile(new Int3(6, 5, 2), "log", 1).Build();
        var mesh = PileMesher.Build(PileMesher.Markers(sim, 15, Colors));
        var stone = Colors.Item(TestContent.Db.Item("stone"));
        var log = Colors.Item(TestContent.Db.Item("log"));
        Assert.Contains(stone, mesh.Colors);
        Assert.Contains(log, mesh.Colors);
        Assert.Contains(PileMesher.CapColor(log), mesh.Colors);
        var cap = PileMesher.CapColor(log);
        Assert.True(cap.X > log.X && cap.Y > log.Y && cap.Z > log.Z, "the cap is lighter than the body");
        EntityViewTests.AssertOutwardWinding(mesh);
    }

    [Fact]
    public void Markers_CarryCountText_AndLabelAnchorAboveTheMarker()
    {
        var sim = new ScenarioBuilder().Ground(4).Pile(new Int3(2, 5, 2), "stone", 12).Build();
        var m = Assert.Single(PileMesher.Markers(sim, 15, Colors));
        Assert.Equal(new Int3(2, 5, 2), m.Cell);
        Assert.Equal(12, m.Count);
        Assert.Equal("12", m.Text);
        Assert.Equal(TestContent.Db.Item("stone"), m.Item);
        var (_, hi) = Bounds(PileMesher.Build(new[] { m }).Positions);
        Assert.True(m.LabelAnchor.Y > hi.Y, "the count label floats above the marker");
        Assert.Equal(2.5f, m.LabelAnchor.X);
        Assert.Equal(2.5f, m.LabelAnchor.Z);
    }

    [Fact]
    public void Markers_HiddenAboveSlice()
    {
        var sim = new ScenarioBuilder().Ground(4).Pile(new Int3(2, 5, 2), "stone", 12).Build();
        Assert.Single(PileMesher.Markers(sim, 5, Colors));
        Assert.Empty(PileMesher.Markers(sim, 4, Colors));
        Assert.True(PileMesher.Build(PileMesher.Markers(sim, 4, Colors)).IsEmpty);
    }

    [Fact]
    public void FloatingPile_GetsAPostDownToTheFloorBelow()
    {
        // Piles do not fall (ECO-08): digging the ground under one leaves it in its cell over air.
        var sim = new ScenarioBuilder().Ground(4)
            .Pile(new Int3(2, 5, 2), "log", 4).Pile(new Int3(6, 5, 2), "log", 4).Build();
        Dig(sim, new Int3(2, 4, 2));
        Dig(sim, new Int3(2, 3, 2));
        var markers = PileMesher.Markers(sim, 15, Colors);
        Assert.Equal(2, markers[0].PostDepth);
        Assert.Equal(0, markers[1].PostDepth);

        var floating = PileMesher.Build(new[] { markers[0] });
        var resting = PileMesher.Build(new[] { markers[1] });
        Assert.Equal(resting.QuadCount + 6, floating.QuadCount);
        var (lo, hi) = Bounds(floating.Positions);
        Assert.Equal(3f, lo.Y);                      // top of the solid block at y = 2
        Assert.Equal(Bounds(resting.Positions).Hi.Y, hi.Y);
        EntityViewTests.AssertOutwardWinding(floating);
        // The post is thin and sits under the marker's center.
        var post = floating.Positions.Where(p => p.Y < 5f).ToArray();
        Assert.All(post, p => Assert.InRange(p.X, 2.3f, 2.7f));
    }

    [Fact]
    public void FloatingPile_PostIsCapped_OverADeepShaft()
    {
        var sim = new ScenarioBuilder().Ground(12).Pile(new Int3(2, 13, 2), "log", 4).Build();
        for (int y = 12; y >= 1; y--) Dig(sim, new Int3(2, y, 2));
        Assert.Equal(PileMesher.MaxPostDepth, Assert.Single(PileMesher.Markers(sim, 30, Colors)).PostDepth);
    }

    [Fact]
    public void Hover_FindsPileOnTopOfPickedBlock_WithLabel()
    {
        var sim = new ScenarioBuilder().Ground(4).Pile(new Int3(2, 5, 2), "stone", 12).Build();
        var hit = PileMesher.AtPick(sim, new PickHit(new Int3(2, 4, 2), Int3.Up), 15);
        Assert.NotNull(hit);
        Assert.Equal(new Int3(2, 5, 2), hit!.Value.Cell);
        Assert.Equal("Stone ×12", PileMesher.Label(TestContent.Db, hit.Value.Stack));
        Assert.Null(PileMesher.AtPick(sim, new PickHit(new Int3(3, 4, 2), Int3.Up), 15));
        Assert.Null(PileMesher.AtPick(sim, new PickHit(new Int3(2, 4, 2), Int3.Up), 4));
    }

    [Fact]
    public void Hover_OnTheFloorUnderAFloatingPile_FindsThePile()
    {
        var sim = new ScenarioBuilder().Ground(4).Pile(new Int3(2, 5, 2), "log", 4).Build();
        Dig(sim, new Int3(2, 4, 2));
        Dig(sim, new Int3(2, 3, 2));
        var hit = PileMesher.AtPick(sim, new PickHit(new Int3(2, 2, 2), Int3.Up), 15);
        Assert.Equal(new Int3(2, 5, 2), Assert.NotNull(hit).Cell);
        // Sliced below the pile: hidden, so not hovered.
        Assert.Null(PileMesher.AtPick(sim, new PickHit(new Int3(2, 2, 2), Int3.Up), 4));
    }

    [Fact]
    public void CountLabels_ShowAtTheDefaultAndHubZoom_HideAtTheOverview()
    {
        Assert.True(PileMesher.LabelsVisible(60f));   // OrbitRig default distance
        Assert.True(PileMesher.LabelsVisible(40f));   // hub screenshot preset
        Assert.True(PileMesher.LabelsVisible(OrbitRig.MinDistance));
        Assert.False(PileMesher.LabelsVisible(OrbitRig.MaxDistance));
    }

    private static void Dig(Simulation sim, Int3 c) => sim.World.SetBlockRaw(sim.World.Index(c), BlockId.Air);

    private static (Vector3 Lo, Vector3 Hi) Bounds(IEnumerable<Vector3> ps)
    {
        var lo = new Vector3(float.MaxValue);
        var hi = new Vector3(float.MinValue);
        foreach (var p in ps) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
        return (lo, hi);
    }
}
