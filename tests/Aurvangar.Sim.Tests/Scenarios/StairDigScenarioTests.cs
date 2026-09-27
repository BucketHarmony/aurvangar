using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Tools;
using Xunit;
using Xunit.Abstractions;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M11-T8 (G6 follow-up "How do I dig down deeper than 1 tile?"): the dig tool's stair-down mode (VIEW-24)
/// reaches stone on seed 1 and the dwarves quarry it and climb out; a straight-sided pit says why its cells wait
/// (DSG-10).</summary>
[Trait("Category", "Scenario")]
public class StairDigScenarioTests
{
    private readonly ITestOutputHelper _out;

    public StairDigScenarioTests(ITestOutputHelper output) => _out = output;

    private static Building Hall(Simulation sim) => sim.Buildings.All.First(b => b.Def.Id == DigStrand.HallDefId);

    private static int SurfaceY(Simulation sim, int x, int z)
    {
        for (int y = sim.World.SizeY - 1; y > 0; y--)
            if (sim.World.IsSolid(new Int3(x, y, z))) return y;
        return 0;
    }

    private static void AssertAllReachHall(Simulation sim, Building hall)
    {
        int region = sim.Regions.RegionOf(hall.EntranceCell);
        Assert.NotEqual(Paths.Regions.None, region);
        foreach (var a in sim.Agents.All)
            if (a.IsAlive)
                Assert.True(sim.Regions.RegionOf(a.Cell) == region,
                    $"tick {sim.Clock.Tick}: {a.Name} at {a.Cell} is cut off from the hall");
    }

    /// <summary>Seed 1: a stair from the ground east of the hall, one step past the first stone step, then a 3x3 room
    /// two high at the bottom. Every cell is dug, the stone reaches the stores, and every dwarf can walk back to the
    /// hall the whole time (the bottom has a path to the hall's entrance).</summary>
    [Fact]
    public void Seed1_StairToStone_QuarriedAndClimbedOut()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        sim.Tick();   // storage totals are counted from the first tick
        var hall = Hall(sim);
        int maxX = hall.FootprintCells().Max(c => c.X);
        int x0 = maxX + 4, z = hall.Origin.Z + 1;
        int top = SurfaceY(sim, x0, z);
        var first = new PickHit(new Int3(x0, top, z), Int3.Up);

        // The first step whose own cell is stone, and one more.
        int n = 0;
        for (int k = 1; k < 16; k++)
            if (sim.World.GetBlock(new Int3(x0 + k, top - k, z)) == BlockId.Stone) { n = k + 1; break; }
        Assert.True(n > 1, "no stone within 16 steps");
        int bottom = top - n;
        var second = new PickHit(new Int3(x0 + n, SurfaceY(sim, x0 + n, z), z), Int3.Up);

        var tool = new ToolController();
        tool.SetTool(ToolKind.Dig);
        tool.SetDigMode(DigMode.StairDown);
        tool.Press(first);
        var commands = tool.ReleaseCommands(second, bottom);   // the view level lowered to the bottom step
        Assert.Equal(n + 1, commands.Count);
        var stairCells = StairDig.SolidCells(sim.World, first, second, bottom);
        Int3 roomMin = new(x0 + n + 1, bottom, z - 1), roomMax = new(x0 + n + 3, bottom + 1, z + 1);
        var room = new List<Int3>();
        for (int y = roomMin.Y; y <= roomMax.Y; y++)
            for (int zz = roomMin.Z; zz <= roomMax.Z; zz++)
                for (int x = roomMin.X; x <= roomMax.X; x++)
                    if (sim.World.IsSolid(new Int3(x, y, zz))) room.Add(new Int3(x, y, zz));
        var all = stairCells.Concat(room).ToList();
        int stone = all.Count(c => sim.World.GetBlock(c) == BlockId.Stone);
        _out.WriteLine($"stair from {first.Cell} {n} steps to y {bottom}; {stairCells.Count} stair cells, {room.Count} room cells, {stone} stone");
        Assert.True(stone >= 10, $"only {stone} stone cells in the stair and room");

        foreach (var c in commands) sim.Enqueue(c);
        sim.Enqueue(new DesignateDig(roomMin, roomMax));
        sim.Enqueue(new DesignateChop(x0 - 1, z - 2, x0 + n + 4, z + 2));   // trees on the way wait their floors
        var stoneItem = TestContent.Db.Item("stone").Value;
        int stored0 = sim.Buildings.Totals.GetValueOrDefault(stoneItem);

        long doneAt = -1;
        for (int t = 0; t < 14000; t++)
        {
            sim.Tick();
            if (t % 20 == 0) AssertAllReachHall(sim, hall);
            if (doneAt < 0 && all.All(c => !sim.World.IsSolid(c))) doneAt = sim.Clock.Tick;
            if (doneAt >= 0 && sim.Buildings.Totals.GetValueOrDefault(stoneItem) >= stored0 + stone) break;
        }
        var left = all.Where(c => sim.World.IsSolid(c)).Select(c => $"{c} {DigStatus.Of(sim, c)}").ToList();
        Assert.True(left.Count == 0, $"tick {sim.Clock.Tick}: not dug: {string.Join("; ", left)}");
        _out.WriteLine($"all dug by tick {doneAt}; stone stored {sim.Buildings.Totals.GetValueOrDefault(stoneItem)} (was {stored0})");
        Assert.True(stored0 >= 60, $"the wagon's stone counts: {stored0}");
        Assert.True(sim.Buildings.Totals.GetValueOrDefault(stoneItem) >= stored0 + stone,
            $"stone in store {sim.Buildings.Totals.GetValueOrDefault(stoneItem)}, expected at least {stored0 + stone}");
        AssertAllReachHall(sim, hall);
        Assert.Equal(5, sim.Agents.All.Count(a => a.IsAlive));
        Assert.DoesNotContain(sim.Designations.All, m => m.Mark == DesignationMark.DigUnreachable);

        // The bottom of the stair walks back up to the hall.
        var foot = new Int3(x0 + n, bottom, z);
        Assert.True(sim.PathGrid.IsWalkable(foot));
        Assert.True(sim.Pathfinder.FindPath(foot, hall.EntranceCell).Found);
    }

    /// <summary>A straight-sided pit, 3x3x3, one dwarf: the cells left undug report "would trap a dwarf", never
    /// "unreachable"; while it is dug, the lower cells report "waiting for the cell above".</summary>
    [Fact]
    public void StraightPit_WaitingCellsSayWouldTrap()
    {
        var sim = new ScenarioBuilder().Ground(8).Hub(new Int3(20, 9, 20)).Stock("water", 20).Stock("berries", 20)
            .Agent(new Int3(5, 9, 5)).Build();
        Int3 min = new(10, 6, 10), max = new(12, 8, 12);
        sim.Enqueue(new DesignateDig(min, max));
        sim.Tick();
        Assert.Equal(DigWait.CellAbove, DigStatus.Of(sim, new Int3(11, 7, 11)));
        Assert.Equal("Dig: waiting for the cell above", DigHover.For(sim, new PickHit(new Int3(11, 7, 11), Int3.Up), 63));

        bool sawTrapWhileOpen = false;
        for (int t = 0; t < 8000; t++)
        {
            sim.Tick();
            if (!sawTrapWhileOpen && t % 10 == 0)
                foreach (var (c, mark) in sim.Designations.All)
                    if (mark == DesignationMark.Dig && DigStatus.Of(sim, c) == DigWait.WouldTrap) { sawTrapWhileOpen = true; break; }
        }
        var waiting = sim.Designations.All.Where(m => m.Mark != DesignationMark.None).Select(m => m.Cell).ToList();
        Assert.NotEmpty(waiting);
        var reasons = waiting.Select(c => (c, DigStatus.Of(sim, c))).ToList();
        _out.WriteLine(string.Join("; ", reasons));
        Assert.Contains(reasons, r => r.Item2 == DigWait.WouldTrap);
        Assert.DoesNotContain(reasons, r => r.Item2 is DigWait.Unreachable or DigWait.Queued or DigWait.None);
        Assert.True(sawTrapWhileOpen, "no open dig ever reported WouldTrap before being given up");
        var trap = reasons.First(r => r.Item2 == DigWait.WouldTrap).c;
        Assert.StartsWith("Dig: would trap a dwarf", DigHover.For(sim, new PickHit(trap, Int3.Up), 63));
        Assert.Null(DigHover.For(sim, new PickHit(trap, Int3.Up), trap.Y - 1));   // above the view level
    }

    /// <summary>A dig no dwarf can reach reports "unreachable".</summary>
    [Fact]
    public void WalledYardDig_ReportsUnreachable()
    {
        var sim = new ScenarioBuilder().Ground(8).Hub(new Int3(20, 9, 20)).Agent(new Int3(5, 9, 5))
            .FillBox(new Int3(24, 9, 2), new Int3(30, 11, 8), BlockId.BuildingSolid)
            .FillBox(new Int3(25, 9, 3), new Int3(29, 11, 7), BlockId.Air)
            .Build();
        var yard = new Int3(27, 8, 5);
        sim.Enqueue(new DesignateDig(yard, yard));
        for (int t = 0; t < 5; t++) sim.Tick();
        Assert.Equal(DigWait.Unreachable, DigStatus.Of(sim, yard));
        Assert.Equal(DigWait.None, DigStatus.Of(sim, yard + new Int3(1, 0, 0)));
    }
}
