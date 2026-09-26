using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M5-T3: levees built by colonists hold water back (BLD-08 completion writes BuildingSolid, which pushes
/// the cell's water out per WAT-12), and a warehouse takes goods the hub has no room for. Worlds are stone to y = 4
/// with a stone layer at y = 5 cut by open channels, so agents walk on y = 6 and step down into a channel.
/// A levee at rotation 90 has its entrance one cell toward +x (downstream here).</summary>
[Trait("Category", "Scenario")]
public class LeveeScenarioTests
{
    private const int C = 5;          // channel level
    private const int Bank = 6;       // agents walk on the bank top

    private static string Row(int width, Func<int, char> at) => new(Enumerable.Range(0, width).Select(at).ToArray());

    /// <summary>A 1-wide river along z = 10 at y = 5: a source at x = 0, a drain at x = 31. Hub on the bank with logs,
    /// two agents.</summary>
    private static Simulation River()
    {
        var b = new ScenarioBuilder().Ground(C - 1);
        var rows = new string[32];
        for (int z = 0; z < 32; z++) rows[z] = Row(32, _ => z == 10 ? '.' : 'S');
        b.Layer(C, rows).Source(new Int3(0, C, 10)).Drain(new Int3(31, C, 10));
        return b.Hub(new Int3(20, Bank, 20)).Stock("log", 20)
            .Agent(new Int3(15, Bank, 15)).Agent(new Int3(16, Bank, 15)).Build();
    }

    /// <summary>buildings.md scenario 3: a levee placed in a flowing river is built by the colonists (its cell is wet
    /// while it is a site); on completion the cell is BuildingSolid and holds no water (WAT-12), and the level just
    /// downstream drops within 200 ticks while the river upstream stays wet.</summary>
    [Fact]
    public void LeveeInRiver_LowersDownstream()
    {
        var sim = River();
        var leveeCell = new Int3(22, C, 10);
        var downstream = new Int3(24, C, 10);
        var upstream = new Int3(20, C, 10);
        sim.RunTicks(600);
        Assert.True(sim.Water.GetLevel(leveeCell) > 0, "the river does not reach the levee cell");
        Assert.True(sim.Water.GetLevel(downstream) > 0, "the river does not reach the downstream cell");
        Assert.False(sim.Water.IsDeep(new Int3(23, C, 10)), "the levee entrance is deep water");

        sim.Enqueue(new PlaceBuilding("levee", leveeCell, 90));
        sim.Tick();
        var levee = Site(sim, "levee");
        Assert.Equal(new Int3(23, C, 10), levee.EntranceCell);

        bool pushChecked = false;
        RunUntil(sim, () => levee.State == BuildingState.Complete, 3000, () =>
        {
            // The tick it completes, WAT-12 has already emptied the cell (Simulation's end-of-tick water hook).
            if (levee.State == BuildingState.Complete)
            {
                Assert.Equal(0, sim.Water.GetLevel(leveeCell));
                pushChecked = true;
            }
        });
        Assert.True(pushChecked);
        Assert.Equal(BlockId.BuildingSolid, sim.World.GetBlock(leveeCell));
        Assert.Equal(20 - 2, Stored(Hub(sim), "log"));

        int atCompletion = sim.Water.GetLevel(downstream);
        int upstreamAtCompletion = sim.Water.GetLevel(upstream);
        Assert.True(atCompletion > 0);
        sim.RunTicks(200);
        int after = sim.Water.GetLevel(downstream);
        Assert.True(after < atCompletion / 2, $"downstream level {atCompletion} -> {after}");
        Assert.Equal(0, sim.Water.GetLevel(leveeCell));
        Assert.True(sim.Water.GetLevel(upstream) >= upstreamAtCompletion,
            $"upstream of the levee fell {upstreamAtCompletion} -> {sim.Water.GetLevel(upstream)}");
        Assert.All(sim.Agents.All, a => Assert.True(a.IsAlive));
    }

    /// <summary>DoD step 7 in miniature: a full reservoir (kept full by a source) sits behind a stone wall; a 2-wide
    /// tunnel runs from the wall to drains at the far end. The player digs through the wall; water floods the tunnel.
    /// The player places a line of two levees across the tunnel; once the colonists have built them, no water passes
    /// the line: every tunnel cell beyond it dries out and stays dry, while the tunnel before it stays flooded.</summary>
    [Fact]
    public void LeveeLine_StopsBreachFlood()
    {
        const int wallX = 6, lineX = 18;
        var b = new ScenarioBuilder().Ground(C - 1);
        var rows = new string[32];
        for (int z = 0; z < 32; z++)
            rows[z] = Row(32, x =>
                (x is >= 1 and <= 5 && z is >= 1 and <= 6) ? 'W'
                : (x > wallX && x <= 30 && z is 3 or 4) ? '.'
                : 'S');
        b.Layer(C, rows).Source(new Int3(1, C, 1)).Source(new Int3(1, C, 6))
            .Drain(new Int3(30, C, 3)).Drain(new Int3(30, C, 4));
        var sim = b.Hub(new Int3(20, Bank, 20)).Stock("log", 20)
            .Agent(new Int3(10, Bank, 10)).Agent(new Int3(12, Bank, 10)).Build();

        sim.RunTicks(50);
        Assert.Equal(0, sim.Water.GetLevel(new Int3(wallX + 1, C, 3)));

        // The breach: the colonists dig the wall between reservoir and tunnel.
        sim.Enqueue(new DesignateDig(new Int3(wallX, C, 3), new Int3(wallX, C, 4)));
        RunUntil(sim, () => sim.Water.GetLevel(new Int3(wallX + 1, C, 3)) > 0, 3000);
        RunUntil(sim, () => sim.World.GetBlock(new Int3(wallX, C, 3)) == BlockId.Air
            && sim.World.GetBlock(new Int3(wallX, C, 4)) == BlockId.Air, 1000);

        // The line goes down while the flood spreads.
        sim.Enqueue(new PlaceBuilding("levee", new Int3(lineX, C, 3), 90));
        sim.Enqueue(new PlaceBuilding("levee", new Int3(lineX, C, 4), 90));
        sim.Tick();
        var line = sim.Buildings.All.Where(x => x.Def.Id == "levee").ToList();
        Assert.Equal(2, line.Count);
        RunUntil(sim, () => line.All(l => l.State == BuildingState.Complete), 4000);
        Assert.All(line, l => Assert.Equal(0, sim.Water.GetLevel(l.Origin)));

        // Whatever passed before the line closed drains away; after that nothing crosses it.
        sim.RunTicks(300);
        for (int i = 0; i < 300; i++)
        {
            sim.Tick();
            for (int x = lineX + 1; x <= 30; x++)
                for (int z = 3; z <= 4; z++)
                    Assert.True(sim.Water.GetLevel(new Int3(x, C, z)) == 0,
                        $"water beyond the levee line at {new Int3(x, C, z)} on tick {sim.Clock.Tick}");
        }
        for (int x = wallX + 1; x < lineX; x++)
            Assert.True(sim.Water.GetLevel(new Int3(x, C, 3)) >= WaterGrid.Full / 2, $"tunnel at x = {x} is not flooded");
        Assert.All(sim.Agents.All, a => Assert.True(a.IsAlive));
    }

    /// <summary>BLD-10/11: a complete warehouse stores solid goods up to 150 in total. Logs the hub has no room for
    /// (100 per item) go to the warehouse; water is never taken.</summary>
    [Fact]
    public void Warehouse_TakesHubOverflow_NotWater()
    {
        var sim = new ScenarioBuilder().Ground(C - 1).Hub(new Int3(20, C, 20)).Stock("log", 95).Stock("stone", 100)
            .Storage("warehouse", new Int3(10, C, 10))
            .Agent(new Int3(15, C, 15)).Agent(new Int3(16, C, 15))
            .Pile(new Int3(15, C, 18), "log", 10).Pile(new Int3(16, C, 18), "log", 10)
            .Build();
        var wh = Site(sim, "warehouse");
        RunUntil(sim, () => sim.Piles.Count == 0 && CarriedTotal(sim, "log") == 0, 2000);
        Assert.Equal(100, Stored(Hub(sim), "log"));
        Assert.Equal(15, Stored(wh, "log"));

        var water = TestContent.Db.Item("water");
        Assert.False(sim.Actions.Accepts(wh, water));
        Assert.True(sim.Actions.Accepts(Hub(sim), water));

        // The total cap: a warehouse holding 150 takes nothing more; the pile stays (G2 answer 4).
        wh.Stored[TestContent.Db.Item("stone").Value] = 135;
        sim.Piles.Add(new Int3(15, C, 18), TestContent.Db.Item("stone"), 3);
        sim.RunTicks(400);
        Assert.Equal(150, wh.Stored.Values.Sum());
        Assert.Equal(3, PileTotal(sim, "stone"));
        Assert.Equal(0, sim.Counters.JobsFailed);
    }
}
