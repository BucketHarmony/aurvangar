using Aurvangar.Sim;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Screenshots;

namespace Aurvangar.ViewCore.Scripts;

/// <summary>The M11-T7 economy session on seed 1 (crafting.md scenario 10, CRF-P1, ADR-086), timed like
/// <see cref="WorkshopScript"/>: its commands are computed from the world at their ticks (<see cref="EnqueueDue"/>).
/// <list type="number">
/// <item>Tick 0: chop the trees around the hall site (<see cref="MonumentScript.Chop"/>: logs to pay the trader), a Water
/// Pump on the river bank (<see cref="MonumentScript.PumpOrigin"/>), and a Sawmill and a Stonecutter at the nearest free
/// sites by the Great Hall (<see cref="ScreenshotScripts.FindSite"/>, clear of the hall site).</item>
/// <item>Tick 1: Keep orders on their blueprints (CRF-07): <see cref="KeepPlanks"/> planks and <see cref="KeepCutStone"/>
/// cut stone.</item>
/// <item>Tick <see cref="WideChopTick"/>: chop every tree around the Great Hall (<see cref="SurvivalScript.Chop"/>), logs
/// for the later visits and the sawmill.</item>
/// <item>Ticks <see cref="TradeTicks"/> (a trader is in from 3000 to 4800, 10200 to 12000 and 17400 to 19200): accept as many lots of offer 0
/// ("10 log -> 10 stone") as the free logs pay for (<see cref="Traders.FreeStock"/>), while lots are left.</item>
/// <item>Tick <see cref="HallTick"/>: plan a small hall, <see cref="HallSize"/> x <see cref="HallSize"/> and three courses
/// high, at <see cref="HallA"/>: a Slate course, a Polished stone course and a Wood planks course, with a two-high door
/// in the south wall and a two-step Wood planks stair inside against the north wall (<see cref="StairA"/>), and release
/// it all at once. A builder reaches one level up (26-neighborhood, CON-08), so the top course is built from the stair
/// and the wall top, as in <see cref="MonumentScript"/>.</item>
/// </list>
/// Coordinates are fixed for seed 1; other seeds get the same commands, which may be rejected.</summary>
public static class EconomyScript
{
    public const ulong Seed = 1;

    public const int KeepPlanks = 30;
    public const int KeepCutStone = 40;
    /// <summary>Offer 0 of the trade wagon: 10 log for 10 stone.</summary>
    public const int TradeOffer = 0;
    public static readonly long[] TradeTicks = { 3001, 3600, 4200, 10201, 10800, 17401, 18000 };

    /// <summary>After the first trade, so the wider chop does not hold up the workshops and the hall site.</summary>
    public const long WideChopTick = 3000;

    public const int HallSize = 5;
    public const int HallHeight = 3;
    /// <summary>The hall's low corner at its base level: the monument's flat site (ground y = 22), cleared by the chop.</summary>
    public static readonly Int3 HallA = MonumentScript.TowerA;
    public static Int3 HallB => HallA + new Int3(HallSize - 1, HallHeight - 1, HallSize - 1);
    /// <summary>The door: the middle of the south wall, two cells high.</summary>
    public static readonly Int3 DoorA = new(HallA.X + HallSize / 2, HallA.Y, HallA.Z + HallSize - 1);
    public static readonly Int3 DoorB = DoorA + Int3.Up;
    /// <summary>The inner stair: two steps along the north wall's inside, rising east, so builders reach the top course.</summary>
    public static readonly Int3 StairA = new(HallA.X + 1, HallA.Y, HallA.Z + 1);
    public static readonly Int3 StairB = StairA + new Int3(1, 0, 0);
    public const BlockId StairBlock = BlockId.Planks;
    public const long HallTick = 3600;

    /// <summary>The block of each course, bottom to top.</summary>
    public static readonly BlockId[] Courses = { BlockId.Slate, BlockId.PolishedStone, BlockId.Planks };

    /// <summary>Every cell the hall fills (its walls less the door, and the stair) with its block, in ascending (y, z, x)
    /// order.</summary>
    public static IReadOnlyList<(Int3 Cell, BlockId Block)> HallCells { get; } = Cells();

    private static IReadOnlyList<(Int3, BlockId)> Cells()
    {
        var list = new List<(Int3, BlockId)>();
        for (int i = 0; i < HallHeight; i++)
        {
            var a = HallA + new Int3(0, i, 0);
            var b = new Int3(HallB.X, a.Y, HallB.Z);
            foreach (var c in BuildShapes.Cells(BuildShape.HollowBox, a, b, 1))
                if (c != DoorA && c != DoorB) list.Add((c, Courses[i]));
        }
        foreach (var c in BuildShapes.Cells(BuildShape.Stair, StairA, StairB, 1)) list.Add((c, StairBlock));
        return list.OrderBy(p => p.Item1.Y).ThenBy(p => p.Item1.Z).ThenBy(p => p.Item1.X).ToList();
    }

    /// <summary>True when every hall cell holds its block.</summary>
    public static bool HallBuilt(Simulation sim) => HallCells.All(p => sim.World.GetBlock(p.Cell) == p.Block);

    /// <summary>Enqueues the commands due at the sim's current tick, so the next <see cref="Simulation.Tick"/> applies
    /// them. Call once before every <c>Tick()</c>. Returns how many were enqueued.</summary>
    public static int EnqueueDue(Simulation sim)
    {
        long t = sim.Clock.Tick;
        var list = new List<ICommand>();
        if (t == 0)
        {
            list.Add(MonumentScript.Chop);
            list.Add(new PlaceBuilding("pump", MonumentScript.PumpOrigin, MonumentScript.PumpRotation));
            var taken = new HashSet<Int3>();
            foreach (var b in sim.Buildings.All) ScreenshotScripts.Take(sim, b.Def, b.Origin, b.Rotation, taken);
            for (int y = HallA.Y - 1; y <= HallB.Y + 1; y++)
                for (int z = HallA.Z - 2; z <= HallB.Z + 2; z++)
                    for (int x = HallA.X - 2; x <= HallB.X + 2; x++) taken.Add(new Int3(x, y, z));
            foreach (var id in new[] { "sawmill", "stonecutter" })
                if (ScreenshotScripts.FindSite(sim, sim.Content.Building(id), taken) is { } site) list.Add(site);
        }
        else if (t == 1)
        {
            if (WorkshopScript.First(sim, "sawmill") is { } mill) list.Add(new SetWorkshopOrder(mill.Id, 0, OrderMode.Keep, KeepPlanks));
            if (WorkshopScript.First(sim, "stonecutter") is { } cutter) list.Add(new SetWorkshopOrder(cutter.Id, 0, OrderMode.Keep, KeepCutStone));
        }
        if (t == WideChopTick) list.Add(SurvivalScript.Chop);
        if (Array.IndexOf(TradeTicks, t) >= 0 && sim.Trader.IsHere && TradeOffer < sim.Trader.LotsLeft.Count)
        {
            var offer = sim.Content.Offers[TradeOffer];
            int lots = Math.Min(sim.Trader.LotsLeft[TradeOffer], Traders.FreeStock(sim, offer.Give) / offer.GiveCount);
            if (lots > 0) list.Add(new AcceptOffer(TradeOffer, lots));
        }
        if (t == HallTick)
        {
            for (int i = 0; i < HallHeight; i++)
            {
                var a = HallA + new Int3(0, i, 0);
                list.Add(new DesignateBuild(BuildShape.HollowBox, a, new Int3(HallB.X, a.Y, HallB.Z), 1, Courses[i], true));
            }
            list.Add(new CancelDesignation(DoorA, DoorB));
            list.Add(new DesignateBuild(BuildShape.Stair, StairA, StairB, 1, StairBlock, true));
            list.Add(new ReleasePlan(HallA, HallB));
        }
        foreach (var c in list) sim.Enqueue(c);
        return list.Count;
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
