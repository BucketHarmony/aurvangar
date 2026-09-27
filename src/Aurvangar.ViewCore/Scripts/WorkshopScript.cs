using Aurvangar.Sim;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Screenshots;

namespace Aurvangar.ViewCore.Scripts;

/// <summary>The <c>workshop</c> screenshot script (M11-T6, VIEW-28/29), timed:
/// <list type="bullet">
/// <item>Tick 0: a Stonecutter and a Sawmill at the nearest free sites by the hall (<see cref="ScreenshotScripts.FindSite"/>).</item>
/// <item>Tick 1: orders on their blueprints (CRF-07): the sawmill keeps <see cref="KeepPlanks"/> planks, the
/// stonecutter makes <see cref="MakeCutStone"/> cut stone.</item>
/// <item>Tick <see cref="AcceptTick"/> (the first trader is in since 3000): one lot of offer 0 (logs for stone).</item>
/// </list>
/// <c>--ticks 3200</c> shows the stonecutter short of stone for its order (the first workshop, whose panel
/// <c>--panel workshop</c> opens), the sawmill's order met and the trade wagon in. The <c>workshop</c> camera preset looks at the workshops and the hall.</summary>
public static class WorkshopScript
{
    public const int KeepPlanks = 30;
    public const int MakeCutStone = 60;
    public const long AcceptTick = 3001;

    public static void EnqueueDue(Simulation sim)
    {
        long t = sim.Clock.Tick;
        if (t == 0)
        {
            var taken = new HashSet<Int3>();
            foreach (var b in sim.Buildings.All) ScreenshotScripts.Take(sim, b.Def, b.Origin, b.Rotation, taken);
            foreach (var id in new[] { "stonecutter", "sawmill" })
                if (ScreenshotScripts.FindSite(sim, sim.Content.Building(id), taken) is { } site) sim.Enqueue(site);
        }
        else if (t == 1)
        {
            if (First(sim, "sawmill") is { } mill) sim.Enqueue(new SetWorkshopOrder(mill.Id, 0, OrderMode.Keep, KeepPlanks));
            if (First(sim, "stonecutter") is { } cutter) sim.Enqueue(new SetWorkshopOrder(cutter.Id, 0, OrderMode.Make, MakeCutStone));
        }
        else if (t == AcceptTick && sim.Trader.IsHere)
            sim.Enqueue(new AcceptOffer(0, 1));
    }

    /// <summary>The first building of <paramref name="defId"/> by id, or null.</summary>
    public static Building? First(Simulation sim, string defId) =>
        sim.Buildings.All.Where(b => b.Def.Id == defId).OrderBy(b => b.Id.Value).FirstOrDefault();
}
