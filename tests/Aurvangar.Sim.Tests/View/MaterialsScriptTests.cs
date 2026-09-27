using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Screenshots;
using Aurvangar.ViewCore.Scripts;
using Aurvangar.ViewCore.Tools;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M9-T4: the <c>materials</c> screenshot script shows every construction block, and the picker lists them.</summary>
[Trait("Category", "Unit")]
public class MaterialsScriptTests
{
    [Fact]
    public void MaterialsScript_Seed1_OneSampleOfEveryBlock_AllAccepted_PresetFindsRow()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        var blocks = MaterialsScript.Blocks(sim);
        Assert.Equal(new BlockTool(TestContent.Db).Blocks, blocks);   // the Blocks menu order
        Assert.Equal(6, blocks.Count);
        var site = MaterialsScript.Site(sim);
        Assert.NotNull(site);

        ScreenshotScripts.Run("materials", sim, 1);
        Assert.Empty(sim.Events.Drain().OfType<CommandRejected>());
        int wall = MaterialsScript.SampleW * MaterialsScript.SampleH, top = MaterialsScript.SampleW;
        Assert.Equal(blocks.Count * (wall + top), sim.Plans.Count);
        foreach (var b in blocks)
        {
            Assert.Equal(wall, sim.Plans.All.Count(p => p.Item2.Block == b && p.Item2.State == PlanState.Released));
            Assert.Equal(top, sim.Plans.All.Count(p => p.Item2.Block == b && p.Item2.State == PlanState.Planned));
        }
        Assert.Equal(site, MaterialsScript.Site(sim));   // recovered from the planned course after the run
        var shot = ScreenshotPresets.For("materials", sim);
        Assert.InRange(shot.Focus.X, site!.Value.X, site.Value.X + blocks.Count * MaterialsScript.SampleW);
    }
}
