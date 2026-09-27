using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M9-T4: the added materials (CON-01 Rubble, Beam, Slate) cost their data cost to build, take their
/// buildTicks and hardness, and refund the whole cost when taken down (CON-17). Worlds are stone to y = 8.</summary>
[Trait("Category", "Scenario")]
public class BlockMaterialScenarioTests
{
    private const int GY = 9;
    private static readonly Int3 HubOrigin = new(20, GY, 20);

    private static readonly (BlockId Id, Int3 Cell, string Item, int Cost, int BuildTicks, int Hardness)[] Materials =
    {
        (BlockId.Rubble, new Int3(8, GY, 8), "stone", 1, 10, 30),
        (BlockId.Beam, new Int3(12, GY, 8), "log", 2, 25, 30),
        (BlockId.Slate, new Int3(16, GY, 8), "stone", 3, 50, 70),
    };

    [Fact]
    public void NewMaterials_BuildForTheirCost_AndRefundItWhenDug()
    {
        var sim = new ScenarioBuilder().Ground(GY - 1)
            .Hub(HubOrigin).Stock("stone", 20).Stock("log", 20)
            .Agent(new Int3(12, GY, 14)).Agent(new Int3(13, GY, 14)).Build();

        // Build: one released block of each; the Place step works buildTicks and takes the cost from the carried stack.
        foreach (var m in Materials)
            sim.Enqueue(new DesignateBuild(BuildShape.Single, m.Cell, m.Cell, 1, m.Id, false));
        var built = new Dictionary<BlockId, int>();
        RunUntil(sim, () => Materials.All(m => sim.World.GetBlock(m.Cell) == m.Id), 3000, () =>
        {
            foreach (var j in sim.Jobs.All.Where(j => j.Kind == JobKind.Build))
                foreach (var s in j.Steps.Where(s => s.Kind == StepKind.Work))
                    foreach (var m in Materials.Where(m => m.Cell == s.Cell))
                        built[m.Id] = s.Ticks;
        });
        Assert.All(Materials, m => Assert.Equal(m.Id, sim.World.GetBlock(m.Cell)));
        RunUntil(sim, () => CarriedTotal(sim, "stone") + CarriedTotal(sim, "log") + PileTotal(sim, "stone") + PileTotal(sim, "log") == 0, 2000);
        Assert.Equal(20 - 1 - 3, Stored(Hub(sim), "stone"));
        Assert.Equal(20 - 2, Stored(Hub(sim), "log"));
        foreach (var m in Materials) Assert.Equal(m.BuildTicks, built[m.Id]);

        // Take them down: each dig takes the hardness and drops the whole cost as one pile.
        sim.Enqueue(new DesignateDeconstructBlocks(new Int3(6, GY - 2, 6), new Int3(18, GY + 2, 10)));
        sim.Tick();
        foreach (var m in Materials)
        {
            var job = sim.Jobs.All.Single(j => j.Kind == JobKind.Dig && j.Target == m.Cell);
            Assert.Equal(m.Hardness, job.Steps.Single(s => s.Kind == StepKind.Work).Ticks);
        }
        var dropped = new Dictionary<BlockId, (string Item, int Count)>();
        RunUntil(sim, () => dropped.Count == Materials.Length, 3000, () =>
        {
            foreach (var m in Materials)
                if (!dropped.ContainsKey(m.Id) && sim.World.GetBlock(m.Cell) == BlockId.Air)
                {
                    var pile = sim.Piles.At(m.Cell);
                    dropped[m.Id] = (sim.Content.ItemDef(pile.Item).Id, pile.Count);
                }
        });
        foreach (var m in Materials) Assert.Equal((m.Item, m.Cost), dropped[m.Id]);

        // The refund comes back to storage in full.
        RunUntil(sim, () => sim.Piles.Count == 0 && CarriedTotal(sim, "stone") + CarriedTotal(sim, "log") == 0, 2000);
        Assert.Equal(20, Stored(Hub(sim), "stone"));
        Assert.Equal(20, Stored(Hub(sim), "log"));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }
}
