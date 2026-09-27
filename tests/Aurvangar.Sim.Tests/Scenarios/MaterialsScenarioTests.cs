using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Screenshots;
using Aurvangar.ViewCore.Scripts;
using Xunit;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M10-T5 (G5 issue 8): the <c>materials</c> screenshot script on seed 1 builds its six samples by tick 12,000
/// and, with its pump, keeps all 5 dwarves alive through the day-5 drought to day 10.</summary>
[Trait("Category", "Scenario")]
public class MaterialsScenarioTests
{
    [Fact]
    public void Seed1_Materials_SamplesBuiltByTick12000_AllAliveAtDay10()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var site = MaterialsScript.Site(sim)!.Value;
        var blocks = MaterialsScript.Blocks(sim);
        int alive = sim.Agents.All.Count(a => a.IsAlive);
        Assert.Equal(5, alive);

        ScreenshotScripts.Run("materials", sim, 1);
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        var pump = Assert.Single(sim.Buildings.All, b => b.Def.Id == "pump");

        ScreenshotScripts.Run("none", sim, 12000 - 1);
        Assert.Equal(12000, sim.Clock.Tick);
        int y = site.Y + 1, z = site.Z + MaterialsScript.RowDz;
        for (int i = 0; i < blocks.Count; i++)
            for (int dx = 0; dx < MaterialsScript.SampleW; dx++)
                for (int dy = 0; dy < MaterialsScript.SampleH; dy++)
                {
                    var c = new Int3(site.X + MaterialsScript.SampleW * i + dx, y + dy, z);
                    Assert.True(blocks[i] == sim.World.GetBlock(c), $"{c}: {sim.World.GetBlock(c)}, expected {blocks[i]}");
                }
        Assert.Equal(BuildingState.Complete, pump.State);

        ScreenshotScripts.Run("none", sim, (int)(10 * SimClock.TicksPerDay - sim.Clock.Tick));
        Assert.Equal(10, sim.Clock.Day);
        Assert.Equal(5, sim.Agents.All.Count(a => a.IsAlive));
        Assert.Equal(site, MaterialsScript.Site(sim));   // the planned course is still there for the preset
    }
}
