using Aurvangar.Sim;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;

namespace Aurvangar.ViewCore.Scripts;

/// <summary>The command log of the definition-of-done session on seed 1 (docs/00-overview.md, docs/testing.md
/// "Scripted play"): a fixed list of commands, each applied at the start of a fixed tick. Used by
/// <c>GoldenHashTests</c>, the survival scenarios and <c>run-headless.sh --script survival</c> (ADR-045, ADR-051,
/// ADR-056).
/// <list type="number">
/// <item>Tick 0: dig the pump's end of the reservoir (<see cref="PumpEndA"/>..<see cref="PumpEndB"/>, the cells in
/// front of and under the pump's intake side) and its footing <see cref="PumpPad"/>, one step down into the bank.</item>
/// <item>Tick <see cref="PumpTick"/>: the Water Pump on the pad at <see cref="PumpOrigin"/>, facing west, its intake in
/// the reservoir; its worker stands one level up on the bank (ADR-055). The rest of the reservoir, a trench
/// <see cref="ReservoirA"/>..<see cref="ReservoirB"/> one cell wide beside the farm, floor one below the river surface,
/// still dammed from the river by <see cref="ReservoirMouth"/>. Then chop the trees around the Great Hall
/// (<see cref="ChopTick"/>; at tick 0 the chops would hold up the pad dig, ADR-048).</item>
/// <item>Tick <see cref="ReservoirFillTick"/> (the trench is dug by ~tick 900): dig the dam <see cref="ReservoirMouth"/>;
/// the river fills the reservoir and the pump starts. Tick <see cref="WarehouseTick"/>: a Warehouse west of the
/// Great Hall.</item>
/// <item>Tick <see cref="LeveeTick"/>: a line of <see cref="LeveeCount"/> levees along the top of the river bank
/// south-west of the Great Hall (flood wall between the hall and the river).</item>
/// <item>Tick <see cref="FarmTick"/> (DoD step 3): a 5×6 potato field <see cref="Farm"/> on moist ground by the river,
/// west of the reservoir; every tile is within 5 columns of its water (ECO-15).</item>
/// <item>Tick <see cref="HillDigTick"/> (DoD step 6): a two-wide tunnel into the south slope of the hill
/// (<see cref="TunnelA"/>..<see cref="TunnelB"/>, floor one below the river surface, entered from the bank top);
/// its north half is stone.</item>
/// <item>Tick <see cref="BreachTick"/> (DoD step 7): dig the bank cells <see cref="BreachA"/>..<see cref="BreachB"/>
/// between the tunnel mouth and the river. The river floods the tunnel.</item>
/// <item>Tick <see cref="RepairTick"/>: a levee on each breach cell, entrance in the tunnel; the builders stand on the
/// dry bank beside the breach. Once they are complete the flood is walled off.</item>
/// <item>Tick <see cref="ReservoirSealTick"/> (DoD step 8, M7-T3): a levee on the reservoir's mouth. The reservoir
/// keeps its water when the river drains in the drought: the pump keeps running and the field stays moist.</item>
/// </list>
/// Coordinates are fixed for seed 1; other seeds get the same commands, which may be rejected.</summary>
public static class SurvivalScript
{
    public const ulong Seed = 1;

    /// <summary>The reservoir trench (M7-T3, ADR-056): x = 67, z 61..70, dug from the bank surface (y 18..19) down to
    /// the river's surface layer y = 17, so its floor is y = 16. Its water layer is y = 17 (<see cref="ReservoirWaterCells"/>).
    /// Every side is bank at least up to y = 17 except the south end, <see cref="ReservoirMouth"/>.</summary>
    public static readonly Int3 ReservoirA = new(67, 17, 61);
    public static readonly Int3 ReservoirB = new(67, 19, 70);
    /// <summary>The bank cell (surface y = 17) between the reservoir's south end and the river's edge at (67,17,72):
    /// the dam until <see cref="ReservoirFillTick"/>, then the seal levee's cell.</summary>
    public static readonly Int3 ReservoirMouth = new(67, 17, 71);
    /// <summary>The reservoir's pump end, dug at tick 0 so the pump can be placed at <see cref="PumpTick"/>: the cell in
    /// front of the pump's intake side and the intake cell below it (BLD-03).</summary>
    public static readonly Int3 PumpEndA = new(67, 17, 70);
    public static readonly Int3 PumpEndB = new(67, 18, 70);
    /// <summary>The pump's first footprint cell, dug so the pump stands one step down on the bank (y = 18, like the
    /// reservoir's rim at the mouth); its second cell (68,18,71) is already free.</summary>
    public static readonly Int3 PumpPad = new(68, 18, 70);
    public static readonly Int3 PumpOrigin = PumpPad;
    /// <summary>Rotation 90: footprint (68,18,70..71), intake side west over (67,18,70), entrance east at (69,18,70),
    /// which is bank, so the worker stands on (69,19,70) (ADR-055).</summary>
    public const int PumpRotation = 90;
    public const long PumpTick = 600;
    public const long ReservoirFillTick = 1200;
    /// <summary>Well before the drought (day 5) so the levee is complete in time; the reservoir then holds ~10 cells of
    /// water, enough for the pump from day 4 to day 10.</summary>
    public const long ReservoirSealTick = 9600;
    /// <summary>The seal levee's entrance is one cell north, in the reservoir (like the breach repair).</summary>
    public const int SealRotation = 0;

    public static readonly Int3 WarehouseOrigin = new(34, 24, 54);
    public const long WarehouseTick = 1200;

    public static readonly Int3 LeveeStart = new(33, 23, 76);
    public const int LeveeCount = 5;
    public const long LeveeTick = 1800;

    /// <summary>The chop area: every tree within 24 cells (X/Z) of the Great Hall's center.</summary>
    public static readonly DesignateChop Chop = new(16, 36, 64, 84);
    /// <summary>After the pump (M6-T3, ADR-048): chops tie with the tick-0 digs (both 25) and would hold them up.</summary>
    public const long ChopTick = PumpTick;

    /// <summary>The nearest 6×6 field to the Great Hall that is all moist farmable ground at tick 0 (the search of
    /// <c>ScreenshotScripts.FindFarm</c> with size 6) less its east column x = 67, which is the reservoir: 5×6 = 30
    /// tiles at surface y 16..19, all within 5 columns of the reservoir's water at y = 17 (ECO-15, ADR-056).</summary>
    public static readonly DesignateFarm Farm = new(62, 67, 66, 72);
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

    /// <summary>The reservoir's water layer (y = 17), north to south; the pump's intake is the last one.</summary>
    public static IEnumerable<Int3> ReservoirWaterCells()
    {
        for (int z = ReservoirA.Z; z <= ReservoirB.Z; z++) yield return new Int3(ReservoirA.X, ReservoirA.Y, z);
    }

    /// <summary>The breach cells, west to east.</summary>
    public static IEnumerable<Int3> BreachCells()
    {
        for (int x = BreachA.X; x <= BreachB.X; x++) yield return new Int3(x, BreachA.Y, BreachA.Z);
    }

    private static IReadOnlyList<(long, ICommand)> Build()
    {
        var list = new List<(long, ICommand)>
        {
            (0, new DesignateDig(PumpEndA, PumpEndB)),
            (0, new DesignateDig(PumpPad, PumpPad)),
            (PumpTick, new PlaceBuilding("pump", PumpOrigin, PumpRotation)),
            (ChopTick, Chop),
            (PumpTick, new DesignateDig(ReservoirA, ReservoirB)),
            (ReservoirFillTick, new DesignateDig(ReservoirMouth, ReservoirMouth)),
            (WarehouseTick, new PlaceBuilding("warehouse", WarehouseOrigin, 0)),
        };
        for (int i = 0; i < LeveeCount; i++)
            list.Add((LeveeTick, new PlaceBuilding("levee", LeveeStart + new Int3(i, 0, 0), 0)));
        list.Add((FarmTick, Farm));
        list.Add((HillDigTick, new DesignateDig(TunnelA, TunnelB)));
        list.Add((BreachTick, new DesignateDig(BreachA, BreachB)));
        foreach (var c in BreachCells())
            list.Add((RepairTick, new PlaceBuilding("levee", c, RepairRotation)));
        list.Add((ReservoirSealTick, new PlaceBuilding("levee", ReservoirMouth, SealRotation)));
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
