using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Screenshots;
using Aurvangar.ViewCore.Scripts;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M7-T7: the screenshot harness runs timed scripts. <c>SCRIPT=survival</c> enqueues each SurvivalScript
/// command at its tick (it used to enqueue everything before tick 1), and the <c>tunnel</c> and <c>reservoir</c>
/// presets frame the flooded hill tunnel with its breach levees and the levee reservoir beside the farm.</summary>
[Trait("Category", "Unit")]
public class TimedScreenshotTests
{
    private static readonly Lazy<Simulation> Seed1 = new(() => WorldFactory.Create(1, TestContent.Db));

    [Fact]
    public void Args_AcceptSurvivalScript_AndNewPresets()
    {
        var a = ScreenshotArgs.Parse(new[] { "--script", "survival", "--shots", "tunnel,reservoir" });
        Assert.Equal("survival", a.Script);
        Assert.Equal(new[] { "tunnel", "reservoir" }, a.Shots);
        Assert.True(ScreenshotScripts.IsTimed("survival"));
        Assert.False(ScreenshotScripts.IsTimed("build"));
    }

    [Fact]
    public void Survival_EnqueuesEachCommandAtItsTick_SameAsSurvivalScriptRun()
    {
        const int ticks = 1300;   // past the pump (600) and the dam dig (1200)
        var harness = WorldFactory.Create(1, TestContent.Db);
        var events = new List<SimEvent>();
        ScreenshotScripts.Run("survival", harness, ticks, () => events.AddRange(harness.Events.Drain()));
        var reference = WorldFactory.Create(1, TestContent.Db);
        SurvivalScript.Run(reference, ticks);

        Assert.Empty(ScreenshotScripts.For("survival", harness));
        Assert.Equal(ticks, harness.Clock.Tick);
        Assert.DoesNotContain(events, e => e is CommandRejected);
        Assert.Equal(reference.Commands.Log.Select(l => l.Tick), harness.Commands.Log.Select(l => l.Tick));
        Assert.Contains(harness.Commands.Log, l => l.Tick > 1);   // not all at tick 1 any more
        Assert.Equal(reference.StateHash(), harness.StateHash());
    }

    [Fact]
    public void UntimedScript_Run_EnqueuesItsCommandsBeforeTickOne()
    {
        var harness = WorldFactory.Create(1, TestContent.Db);
        int after = 0;
        ScreenshotScripts.Run("digchop", harness, 5, () => after++);
        Assert.Equal(5, after);

        var reference = WorldFactory.Create(1, TestContent.Db);
        foreach (var c in ScreenshotScripts.For("digchop", reference)) reference.Enqueue(c);
        reference.RunTicks(5);
        Assert.Equal(reference.StateHash(), harness.StateHash());
    }

    [Fact]
    public void Tunnel_SlicesTheHillOpenOverTheTunnelAndBreach()
    {
        var sim = Seed1.Value;
        var shot = ScreenshotPresets.For("tunnel", sim);
        var a = SurvivalScript.TunnelA;
        var b = SurvivalScript.TunnelB;
        Assert.InRange(shot.Focus.X, a.X, b.X + 1);
        Assert.InRange(shot.Focus.Z, a.Z, SurvivalScript.BreachA.Z + 1);
        // The tunnel's headroom is visible and the hill above it is cut away.
        Assert.Equal(b.Y, shot.SliceY);
        for (int z = a.Z; z <= b.Z; z++)
            Assert.True(ScreenshotPresets.SurfaceY(sim, a.X, z) > shot.SliceY || z >= b.Z - 2,
                $"the hill over ({a.X},{z}) is not above the slice");
        Assert.True(shot.Distance <= 40f);
    }

    [Fact]
    public void Reservoir_FramesTheReservoirAndTheFarmBesideIt()
    {
        var sim = Seed1.Value;
        var shot = ScreenshotPresets.For("reservoir", sim);
        var farm = SurvivalScript.Farm;
        Assert.InRange(shot.Focus.X, farm.X0, SurvivalScript.ReservoirA.X + 1);
        Assert.InRange(shot.Focus.Z, SurvivalScript.ReservoirA.Z, SurvivalScript.ReservoirMouth.Z + 1);
        Assert.Equal(sim.World.SizeY - 1, shot.SliceY);
        Assert.True(shot.Distance <= 40f);
    }
}
