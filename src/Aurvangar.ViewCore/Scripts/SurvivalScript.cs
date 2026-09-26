using Aurvangar.Sim;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;

namespace Aurvangar.ViewCore.Scripts;

/// <summary>The command log of the definition-of-done session on seed 1 (docs/00-overview.md, docs/testing.md
/// "Scripted play"): a fixed list of commands, each applied at the start of a fixed tick. Used by
/// <c>GoldenHashTests</c>, the survival scenarios and <c>run-headless.sh --script survival</c> (ADR-045).
/// Built up by milestone; so far (M5-T7) it covers DoD steps 2, 4 and 5 plus a levee line:
/// <list type="number">
/// <item>Tick 0: dig the one-cell notch <see cref="PumpNotch"/> in the river bank (ADR-044: on seed 1 every wet pump
/// site has its entrance inside the next bank step) and chop the trees around the Great Hall.</item>
/// <item>Tick <see cref="PumpTick"/> (the notch is dug by tick ~485): the Water Pump on the bank at
/// <see cref="PumpOrigin"/>, facing north, its intake in the river (level 1024).</item>
/// <item>Tick <see cref="WarehouseTick"/>: a Warehouse west of the Great Hall.</item>
/// <item>Tick <see cref="LeveeTick"/>: a line of <see cref="LeveeCount"/> levees along the top of the river bank
/// south-west of the Great Hall (flood wall between the hall and the river).</item>
/// </list>
/// M6-T6 adds the farm field, the hill dig, the bank breach and the levee repair.
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

    /// <summary>(tick, command) pairs in tick order; commands of one tick are enqueued in list order.</summary>
    public static IReadOnlyList<(long Tick, ICommand Command)> Commands { get; } = Build();

    /// <summary>The last tick that has a command.</summary>
    public static long LastTick => Commands[^1].Tick;

    private static IReadOnlyList<(long, ICommand)> Build()
    {
        var list = new List<(long, ICommand)>
        {
            (0, new DesignateDig(PumpNotch, PumpNotch)),
            (0, Chop),
            (PumpTick, new PlaceBuilding("pump", PumpOrigin, PumpRotation)),
            (WarehouseTick, new PlaceBuilding("warehouse", WarehouseOrigin, 0)),
        };
        for (int i = 0; i < LeveeCount; i++)
            list.Add((LeveeTick, new PlaceBuilding("levee", LeveeStart + new Int3(i, 0, 0), 0)));
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
