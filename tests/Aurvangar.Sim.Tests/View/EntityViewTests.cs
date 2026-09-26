using System.Numerics;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M4-T11: agent, pile and designation render data (VIEW-08, VIEW-10, VIEW-11).</summary>
[Trait("Category", "Unit")]
public class EntityViewTests
{
    private static readonly EntityColors Colors = new(TestContent.Db);

    [Fact]
    public void Colors_ComeFromPalette()
    {
        Assert.Equal(BlockColors.ParseHex("#b0b0b0"), Colors.Idle);
        Assert.Equal(BlockColors.ParseHex("#f0f0f0"), Colors.Working);
        Assert.Equal(BlockColors.ParseHex("#c03030"), Colors.Dead);
        Assert.Equal(BlockColors.ParseHex("#ff9a3c"), Colors.Dig);
        Assert.Equal(BlockColors.ParseHex("#e03a3a"), Colors.Unreachable);
        Assert.Equal(BlockColors.ParseHex("#8c8c8c"), Colors.Item(TestContent.Db.Item("stone")));
        Assert.NotEqual(Colors.Idle, Colors.Trapped);
        Assert.NotEqual(Colors.Dead, Colors.Trapped);
    }

    [Fact]
    public void AgentPosition_LerpsCellToNextCell_ByProgress()
    {
        var a = new Agent { Cell = new Int3(2, 5, 3), NextCell = new Int3(3, 5, 3), MoveProgress = 1, MoveTotal = 4 };
        Assert.Equal(new Vector3(2.75f, 5f, 3.5f), AgentVisuals.Position(a));
        // The sub-tick fraction smooths motion but never passes the next cell.
        Assert.Equal(new Vector3(3.0f, 5f, 3.5f), AgentVisuals.Position(a, 1f));
        a.MoveProgress = 4;
        Assert.Equal(new Vector3(3.5f, 5f, 3.5f), AgentVisuals.Position(a, 1f));
        var still = new Agent { Cell = new Int3(2, 5, 3), NextCell = new Int3(2, 5, 3) };
        Assert.Equal(new Vector3(2.5f, 5f, 3.5f), AgentVisuals.Position(still, 0.7f));
    }

    [Fact]
    public void AgentLook_FollowsState_AndCarriedItemHasItsColor()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(new Int3(2, 5, 2)).Agent(new Int3(4, 5, 2))
            .Agent(new Int3(6, 5, 2)).Agent(new Int3(8, 5, 2)).Build();
        var agents = sim.Agents.All.ToArray();
        agents[1].State = AgentState.Working;
        agents[1].Carried = new Items.ItemStack(TestContent.Db.Item("log"), 3);
        agents[2].State = AgentState.Trapped;
        sim.Agents.Kill(sim, agents[3], DeathCause.Drowned);

        var v = AgentVisuals.Build(sim, 15, Colors);
        Assert.Equal(new[] { AgentLook.Idle, AgentLook.Working, AgentLook.Trapped, AgentLook.Dead }, v.Select(x => x.Look));
        Assert.Equal(Colors.Idle, v[0].Color);
        Assert.Equal(Colors.Dead, v[3].Color);
        Assert.False(v[0].Carried.IsValid);
        Assert.Equal(TestContent.Db.Item("log"), v[1].Carried);
        Assert.Equal(BlockColors.ParseHex("#8a5a2b"), v[1].CarriedColor);
    }

    [Fact]
    public void Agent_HiddenAboveSlice()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(new Int3(2, 5, 2)).Build();
        Assert.True(AgentVisuals.Build(sim, 5, Colors)[0].Visible);
        Assert.False(AgentVisuals.Build(sim, 4, Colors)[0].Visible);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(40, 8)]
    [InlineData(500, 8)]
    public void PileCubes_OnePerFiveItems_Capped(int count, int cubes) => Assert.Equal(cubes, PileMesher.CubesFor(count));

    [Fact]
    public void Piles_CubeStackInItemColor_HiddenAboveSlice()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Pile(new Int3(2, 5, 2), "stone", 12).Pile(new Int3(6, 5, 2), "log", 1).Build();
        var mesh = PileMesher.Build(sim.Piles, 15, Colors);
        Assert.Equal((3 + 1) * 6, mesh.QuadCount);
        Assert.Contains(Colors.Item(TestContent.Db.Item("stone")), mesh.Colors);
        Assert.Contains(Colors.Item(TestContent.Db.Item("log")), mesh.Colors);
        AssertOutwardWinding(mesh);
        // Cubes sit on the floor of the pile's cell, inside it.
        Assert.All(mesh.Positions, p => Assert.InRange(p.Y, 5f, 6f));
        Assert.True(PileMesher.Build(sim.Piles, 4, Colors).IsEmpty);
    }

    [Fact]
    public void PileHover_FindsPileOnTopOfPickedBlock_WithLabel()
    {
        var sim = new ScenarioBuilder().Ground(4).Pile(new Int3(2, 5, 2), "stone", 12).Build();
        var hit = PileMesher.AtPick(sim.Piles, new PickHit(new Int3(2, 4, 2), Int3.Up), 15);
        Assert.NotNull(hit);
        Assert.Equal(new Int3(2, 5, 2), hit!.Value.Cell);
        Assert.Equal("Stone ×12", PileMesher.Label(TestContent.Db, hit.Value.Stack));
        Assert.Null(PileMesher.AtPick(sim.Piles, new PickHit(new Int3(3, 4, 2), Int3.Up), 15));
        Assert.Null(PileMesher.AtPick(sim.Piles, new PickHit(new Int3(2, 4, 2), Int3.Up), 4));
    }

    [Fact]
    public void Designations_DigCubes_ChopRings_UnreachableRed()
    {
        var sim = new ScenarioBuilder().Ground(4)
            .Layer(5, "..........", "..T.......", "......T...").Build();
        sim.Designations.Set(new Int3(1, 4, 1), DesignationMark.Dig);
        sim.Designations.Set(new Int3(8, 4, 1), DesignationMark.DigUnreachable);
        var trees = sim.Plants.All.ToArray();
        trees[0].MarkedForChop = true;
        trees[1].MarkedForChop = true;
        trees[1].ChopUnreachable = true;

        var mesh = DesignationMesher.Build(sim, 15, Colors);
        Assert.Equal(2 * 6 + 2 * 4 * 6, mesh.QuadCount);   // 2 cubes, 2 rings of 4 boxes
        AssertOutwardWinding(mesh);
        var dig = Colors.Dig with { W = EntityColors.DigAlpha };
        var digRed = Colors.Unreachable with { W = EntityColors.DigAlpha };
        var ring = Colors.Chop with { W = DesignationMesher.RingAlpha };
        var ringRed = Colors.Unreachable with { W = DesignationMesher.RingAlpha };
        Assert.Equal(6 * 4, mesh.Colors.Count(c => c == dig));
        Assert.Equal(6 * 4, mesh.Colors.Count(c => c == digRed));
        Assert.Equal(24 * 4, mesh.Colors.Count(c => c == ring));
        Assert.Equal(24 * 4, mesh.Colors.Count(c => c == ringRed));
        Assert.True(mesh.Colors.All(c => c.W < 1f), "the overlay is translucent");

        // Slice at y = 4 hides the rings (tree bases are at y = 5) but keeps the dig cubes.
        Assert.Equal(2 * 6, DesignationMesher.Build(sim, 4, Colors).QuadCount);
        Assert.True(DesignationMesher.Build(sim, 3, Colors).IsEmpty);
    }

    [Fact]
    public void DesignationSignature_ChangesWithMarks()
    {
        var sim = new ScenarioBuilder().Ground(4).Layer(5, "..........", "..T.......").Build();
        ulong s0 = DesignationMesher.Signature(sim);
        sim.Designations.Set(new Int3(1, 4, 1), DesignationMark.Dig);
        ulong s1 = DesignationMesher.Signature(sim);
        sim.Designations.MarkUnreachable(new Int3(1, 4, 1));
        ulong s2 = DesignationMesher.Signature(sim);
        sim.Plants.All.First().MarkedForChop = true;
        ulong s3 = DesignationMesher.Signature(sim);
        Assert.Equal(4, new[] { s0, s1, s2, s3 }.Distinct().Count());
        Assert.Equal(s3, DesignationMesher.Signature(sim));
    }

    /// <summary>Every quad is counter-clockwise seen from its normal side (the MeshData contract).</summary>
    private static void AssertOutwardWinding(MeshData m)
    {
        for (int q = 0; q < m.QuadCount; q++)
        {
            var a = m.Positions[q * 4]; var b = m.Positions[q * 4 + 1]; var c = m.Positions[q * 4 + 2];
            Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), m.Normals[q * 4]) > 0, $"quad {q} is wound inward");
        }
    }
}
