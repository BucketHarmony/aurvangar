using Aurvangar.Sim;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Hud;

namespace Aurvangar.ViewCore.Tools;

/// <summary>The stored material no plan entry already claims (M10-T2, VIEW-21, ADR-072), per item: what a new block
/// drag can use. For a build drag it is the stock minus what the Released entries need; for a plan-mode drag it is
/// also minus the Planned entries' need (what releasing the whole plan would leave, as VIEW-23's Planned part).
/// Never below zero. A snapshot: <see cref="Of"/> is O(entries + buildings) (CON-06), so the view takes one at most
/// every <c>PlanTextFrames</c> frames, together with the top bar's plan line, and the block tool's ghost reuses it.</summary>
public sealed class FreeStock
{
    private readonly int[] _build;
    private readonly int[] _plan;

    private FreeStock(int[] build, int[] plan)
    {
        _build = build;
        _plan = plan;
    }

    public static FreeStock Of(Simulation sim)
    {
        var totals = TopBarModel.Totals(sim);   // index i - 1 is item id i
        int n = totals.Count + 1;
        var build = new int[n];
        var plan = new int[n];
        for (int i = 1; i < n; i++) build[i] = plan[i] = totals[i - 1].Count;
        foreach (var (item, need) in sim.Plans.Needed(PlanState.Released))
            if (item.Value > 0 && item.Value < n) { build[item.Value] -= need; plan[item.Value] -= need; }
        foreach (var (item, need) in sim.Plans.Needed(PlanState.Planned))
            if (item.Value > 0 && item.Value < n) plan[item.Value] -= need;
        for (int i = 0; i < n; i++)
        {
            build[i] = Math.Max(0, build[i]);
            plan[i] = Math.Max(0, plan[i]);
        }
        return new FreeStock(build, plan);
    }

    /// <summary>The free units of <paramref name="item"/> for a build drag, or a plan-mode drag.</summary>
    public int Of(ItemId item, bool plan)
    {
        var a = plan ? _plan : _build;
        return item.Value > 0 && item.Value < a.Length ? a[item.Value] : 0;
    }
}
