using Aurvangar.Sim;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;

namespace Aurvangar.ViewCore.Scripts;

/// <summary>The command log of the definition-of-done session on seed 1 (docs/00-overview.md, docs/testing.md
/// "Scripted play"): a fixed list of commands, each applied at the start of a fixed tick. Used by
/// <c>GoldenHashTests</c>, the survival scenarios and <c>run-headless.sh --script survival</c> (ADR-045, ADR-051).
/// <list type="number">
/// <item>Tick 0: dig the one-cell notch <see cref="PumpNotch"/> in the river bank (ADR-044: on seed 1 every wet pump
/// site has its entrance inside the next bank step).</item>
/// <item>Tick <see cref="PumpTick"/> (the notch is dug by tick ~416, after the first berry picking, M6-T3): the Water
/// Pump on the bank at <see cref="PumpOrigin"/>, facing north, its intake in the river (level 1024). Then chop the
/// trees around the Great Hall (<see cref="ChopTick"/>; at tick 0 the chops would hold up the notch dig, ADR-048).</item>
/// <item>Tick <see cref="WarehouseTick"/>: a Warehouse west of the Great Hall.</item>
/// <item>Tick <see cref="LeveeTick"/>: a line of <see cref="LeveeCount"/> levees along the top of the river bank
/// south-west of the Great Hall (flood wall between the hall and the river).</item>
/// <item>Tick <see cref="FarmTick"/> (DoD step 3): a 6×6 potato field <see cref="Farm"/> on moist ground by the river.</item>
/// <item>Tick <see cref="HillDigTick"/> (DoD step 6): a two-wide tunnel into the south slope of the hill
/// (<see cref="TunnelA"/>..<see cref="TunnelB"/>, floor one below the river surface, entered from the bank top);
/// its north half is stone.</item>
/// <item>Tick <see cref="BreachTick"/> (DoD step 7): dig the bank cells <see cref="BreachA"/>..<see cref="BreachB"/>
/// between the tunnel mouth and the river. The river floods the tunnel.</item>
/// <item>Tick <see cref="RepairTick"/>: a levee on each breach cell, entrance in the tunnel; the builders stand on the
/// dry bank beside the breach. Once they are complete the flood is walled off.</item>
/// </list>
/// Coordinates are fixed for seed 1; other seeds get the same commands, which may be rejected.</summary>
public static class SurvivalScript
{
    public const ulong Seed = 1;

    public static readonly Int3 PumpNotch = new(40, 18, 79);
    public static readonly Int3 PumpOrigin = new(40, 18, 80);
    public const int PumpRotation = 0;
    public const long PumpTick = 600;

    public static readonly Int3 WarehouseOrigin = new(34, 24, 54);
    public const long WarehouseTick = 1200;

    public static readonly Int3 LeveeStart = new(33, 23, 76);
    public const int LeveeCount = 5;
    public const long LeveeTick = 1800;

    /// <summary>The chop area: every tree within 24 cells (X/Z) of the Great Hall's center.</summary>
    public static readonly DesignateChop Chop = new(16, 36, 64, 84);
    /// <summary>After the pump (M6-T3, ADR-048): chops tie with the notch dig (both 25) and would hold it up.</summary>
    public const long ChopTick = PumpTick;

    /// <summary>The nearest 6×6 field to the Great Hall that is all moist farmable ground at tick 0 (the search of
    /// <c>ScreenshotScripts.FindFarm</c> with size 6).</summary>
    public static readonly DesignateFarm Farm = new(62, 67, 67, 72);
    public const long FarmTick = 2400;

    /// <summary>Tunnel box (two cells high: standing cell and headroom). The bank top at z=66 (y=17) is left as a dam
    /// between the tunnel and the river; the surface cell above it (y=18) is the way in.</summary>
    public static readonly Int3 TunnelA = new(84, 17, 56);
    public static readonly Int3 TunnelB = new(85, 18, 65);
    public const long HillDigTick = 3000;

    /// <summary>The dam cells: the river's top water layer (y=17) is on their south side, the tunnel on the north.</summary>
    public static readonly Int3 BreachA = new(84, 17, 66);
    public static readonly Int3 BreachB = new(85, 17, 66);
    public const long BreachTick = 7200;

    /// <summary>Levees on the breach cells, rotation 0 (entrance one cell north, in the tunnel).</summary>
    public const long RepairTick = BreachTick + 300;
    public const int RepairRotation = 0;

    /// <summary>(tick, command) pairs in tick order; commands of one tick are enqueued in list order.</summary>
    public static IReadOnlyList<(long Tick, ICommand Command)> Commands { get; } = Build();

    /// <summary>The last tick that has a command.</summary>
    public static long LastTick => Commands[^1].Tick;

    /// <summary>The breach cells, west to east.</summary>
    public static IEnumerable<Int3> BreachCells()
    {
        for (int x = BreachA.X; x <= BreachB.X; x++) yield return new Int3(x, BreachA.Y, BreachA.Z);
    }

    private static IReadOnlyList<(long, ICommand)> Build()
    {
        var list = new List<(long, ICommand)>
        {
            (0, new DesignateDig(PumpNotch, PumpNotch)),
            (PumpTick, new PlaceBuilding("pump", PumpOrigin, PumpRotation)),
            (ChopTick, Chop),
            (WarehouseTick, new PlaceBuilding("warehouse", WarehouseOrigin, 0)),
        };
        for (int i = 0; i < LeveeCount; i++)
            list.Add((LeveeTick, new PlaceBuilding("levee", LeveeStart + new Int3(i, 0, 0), 0)));
        list.Add((FarmTick, Farm));
        list.Add((HillDigTick, new DesignateDig(TunnelA, TunnelB)));
        list.Add((BreachTick, new DesignateDig(BreachA, BreachB)));
        foreach (var c in BreachCells())
            list.Add((RepairTick, new PlaceBuilding("levee", c, RepairRotation)));
        return list;
    }

    /// <summary>Enqueues the commands due at the sim's current tick, so the next <see cref="Simulation.Tick"/> applies
    /// them (and logs them with that tick). Call once before every <c>Tick()</c>. Returns how many were enqueued.</summary>
    public static int EnqueueDue(Simulation sim)
    {
        int n = 0;
        foreach (var (tick, command) in Commands)
        {
            if (tick != sim.Clock.Tick) continue;
            sim.Enqueue(command);
            n++;
        }
        return n;
    }

    /// <summary>Runs <paramref name="ticks"/> ticks, enqueuing the script's commands at their ticks.</summary>
    public static void Run(Simulation sim, long ticks)
    {
        for (long i = 0; i < ticks; i++)
        {
            EnqueueDue(sim);
            sim.Tick();
        }
    }
}
