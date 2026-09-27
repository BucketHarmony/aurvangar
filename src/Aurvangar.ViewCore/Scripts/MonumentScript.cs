using Aurvangar.Sim;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.ViewCore.Scripts;

/// <summary>The M8-T6 monument session on seed 1 (construction.md CON-P1, ADR-066): fixed commands at fixed ticks, like
/// <see cref="SurvivalScript"/>, plus one <see cref="ReleasePlan"/> per course as the tower rises
/// (<see cref="NextCourse"/>). Used by the monument scenarios, the CON-P1 perf test,
/// <c>run-headless.sh --script monument</c> and the screenshot harness (<c>SCRIPT=monument</c>).
/// <list type="number">
/// <item>Tick 0: chop the trees around the site (<see cref="Chop"/>; logs for the warehouses), a Water Pump on the
/// river bank (<see cref="PumpOrigin"/>), a corridor into the hill's west side (<see cref="CorridorA"/>..
/// <see cref="CorridorB"/>), and the plan, Planned only: a hollow Stone wall tower <see cref="TowerSize"/> x
/// <see cref="TowerSize"/>, <see cref="TowerHeight"/> high, at <see cref="TowerA"/>; its door (the south wall's
/// middle, two high) cancelled; an inner stair in two flights against the north and east walls up to one below the
/// top course, so builders can climb onto the walls; and a courtyard south of the door, walled two high with a
/// gate.</item>
/// <item>Tick <see cref="RoomDigTick"/>: the quarry room (<see cref="QuarryA"/>..<see cref="QuarryB"/>, two high, all
/// Stone: GEN-04 puts the hill's stone five cells under its surface), 216 stone for the 221 blocks with the
/// corridor's.</item>
/// <item>Ticks <see cref="QuarryStoreTick"/> and <see cref="YardTick"/>: two Warehouses inside the dug room, so each
/// dug stone (a one-item pile, one haul each) travels a few cells, and all of it is in store before the release.</item>
/// <item>From tick <see cref="ReleaseTick"/>: release the lowest course that still has Planned entries once the course
/// below it is built. A whole course is Ready at once, so the Build jobs carry full batches (CON-12) up to it.</item>
/// </list>
/// Coordinates are fixed for seed 1; other seeds get the same commands, which may be rejected.</summary>
public static class MonumentScript
{
    public const ulong Seed = 1;

    public static readonly DesignateChop Chop = new(52, 34, 70, 53);

    /// <summary>The nearest wet pump site to the hub on seed 1 (<c>ScreenshotScripts.FindSite</c> with wet: true).</summary>
    public static readonly Int3 PumpOrigin = new(54, 18, 76);
    public const int PumpRotation = 0;

    public static readonly Int3 CorridorA = new(71, 24, 41);
    public static readonly Int3 CorridorB = new(79, 25, 42);
    public static readonly Int3 QuarryA = new(80, 24, 37);
    public static readonly Int3 QuarryB = new(91, 25, 45);
    /// <summary>The room is marked after the chop's logs are cut, so digs (JOB-05 25) do not take every dwarf first.</summary>
    public const long RoomDigTick = 1500;

    public const int TowerSize = 7;
    public const int TowerHeight = 8;
    /// <summary>The tower's low corner (least x and z) at its base level (ground y = 22 under the whole site).</summary>
    public static readonly Int3 TowerA = new(58, 23, 39);
    public static Int3 TowerB => TowerA + new Int3(TowerSize - 1, 0, TowerSize - 1);
    /// <summary>The door: the middle of the south wall, two cells high.</summary>
    public static readonly Int3 DoorA = new(TowerA.X + TowerSize / 2, TowerA.Y, TowerA.Z + TowerSize - 1);
    public static readonly Int3 DoorB = DoorA + Int3.Up;

    /// <summary>Courtyard walls (two high) south of the tower: x = 58 and x = 64 for z 46..49, and the front z = 49
    /// with a one-cell gate at x = 61.</summary>
    public const int CourtyardHeight = 2;
    public const int CourtyardFrontZ = 49;

    /// <summary>The first quarry Warehouse, at the room's mouth once its cells are dug.</summary>
    public static readonly Int3 QuarryStoreOrigin = new(80, 24, 43);
    public const long QuarryStoreTick = 4200;
    /// <summary>The second, at the room's far end: 150 stone fit in one Warehouse, and the rest would go to the Great
    /// Hall, a long walk per stone.</summary>
    public static readonly Int3 YardOrigin = new(88, 24, 42);
    public const long YardTick = 6600;

    /// <summary>The first course's release, once the stone is in store.</summary>
    public const long ReleaseTick = 9600;
    /// <summary>A course is released when the one below has at most this many entries left.</summary>
    public const int CourseLeft = 0;

    /// <summary>(tick, command) pairs in tick order; commands of one tick are enqueued in list order. The course
    /// releases are not listed: they depend on the build's progress (<see cref="NextCourse"/>).</summary>
    public static IReadOnlyList<(long Tick, ICommand Command)> Commands { get; } = Build();

    public static long LastTick => Commands[^1].Tick;

    private static IReadOnlyList<(long, ICommand)> Build()
    {
        var a = TowerA; var b = TowerB;
        int y = a.Y;
        return new List<(long, ICommand)>
        {
            (0, Chop),
            (0, new PlaceBuilding("pump", PumpOrigin, PumpRotation)),
            (0, new DesignateDig(CorridorA, CorridorB)),
            (0, new DesignateBuild(BuildShape.HollowBox, a, b, TowerHeight, BlockId.Masonry, true)),
            (0, new CancelDesignation(DoorA, DoorB)),
            // Flight 1 along the north wall (z = a.Z + 1), rising east: steps y .. y+4.
            (0, new DesignateBuild(BuildShape.Stair, new Int3(a.X + 1, y, a.Z + 1), new Int3(b.X - 1, y, a.Z + 1), 1, BlockId.Masonry, true)),
            // Flight 2 along the east wall (x = b.X - 1), rising south: steps y+5, y+6 (the top course y+7 is reached from it).
            (0, new DesignateBuild(BuildShape.Stair, new Int3(b.X - 1, y + 5, a.Z + 2), new Int3(b.X - 1, y + 5, a.Z + 3), 1, BlockId.Masonry, true)),
            (0, new DesignateBuild(BuildShape.Wall, new Int3(a.X, y, b.Z + 1), new Int3(a.X, y, CourtyardFrontZ), CourtyardHeight, BlockId.Masonry, true)),
            (0, new DesignateBuild(BuildShape.Wall, new Int3(b.X, y, b.Z + 1), new Int3(b.X, y, CourtyardFrontZ), CourtyardHeight, BlockId.Masonry, true)),
            (0, new DesignateBuild(BuildShape.Wall, new Int3(a.X + 1, y, CourtyardFrontZ), new Int3(DoorA.X - 1, y, CourtyardFrontZ), CourtyardHeight, BlockId.Masonry, true)),
            (0, new DesignateBuild(BuildShape.Wall, new Int3(DoorA.X + 1, y, CourtyardFrontZ), new Int3(b.X - 1, y, CourtyardFrontZ), CourtyardHeight, BlockId.Masonry, true)),
            (RoomDigTick, new DesignateDig(QuarryA, QuarryB)),
            (QuarryStoreTick, new PlaceBuilding("warehouse", QuarryStoreOrigin, 0)),
            (YardTick, new PlaceBuilding("warehouse", YardOrigin, 0)),
        };
    }

    /// <summary>Every cell the plan fills (the tower less its door, the stair, the courtyard walls), in ascending
    /// (y, z, x) order: what "complete" means.</summary>
    public static IReadOnlyList<Int3> PlannedCells { get; } = Cells();

    private static IReadOnlyList<Int3> Cells()
    {
        var set = new HashSet<Int3>();
        foreach (var (_, c) in Commands)
            if (c is DesignateBuild d)
                foreach (var cell in BuildShapes.Cells(d.Shape, d.A, d.B, d.Height)) set.Add(cell);
        set.Remove(DoorA);
        set.Remove(DoorB);
        return set.OrderBy(c => c.Y).ThenBy(c => c.Z).ThenBy(c => c.X).ToList();
    }

    /// <summary>Enqueues the commands due at the sim's current tick, so the next <see cref="Simulation.Tick"/> applies
    /// them. Call once before every <c>Tick()</c>. Returns how many were enqueued.</summary>
    public static int EnqueueDue(Simulation sim)
    {
        int n = 0;
        foreach (var (tick, command) in Commands)
        {
            if (tick != sim.Clock.Tick) continue;
            sim.Enqueue(command);
            n++;
        }
        if (NextCourse(sim) is { } course)
        {
            sim.Enqueue(new ReleasePlan(new Int3(0, course, 0), new Int3(WorldFactory.SizeX - 1, course, WorldFactory.SizeZ - 1)));
            n++;
        }
        return n;
    }

    /// <summary>The course (y) to release now, or null: from <see cref="ReleaseTick"/> on, the lowest course that still
    /// has Planned entries, once the course below it has at most <see cref="CourseLeft"/> entries left unbuilt. A
    /// function of the sim state alone, so a loaded save carries on the same way.</summary>
    public static int? NextCourse(Simulation sim)
    {
        if (sim.Clock.Tick < ReleaseTick) return null;
        int lowestPlanned = int.MaxValue;
        foreach (var (c, e) in sim.Plans.All)   // ascending index is ascending y
            if (e.State == PlanState.Planned) { lowestPlanned = c.Y; break; }
        if (lowestPlanned == int.MaxValue) return null;
        int below = 0;
        foreach (var (c, _) in sim.Plans.All)
        {
            if (c.Y > lowestPlanned - 1) break;
            if (c.Y == lowestPlanned - 1) below++;
        }
        return below <= CourseLeft ? lowestPlanned : null;
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
