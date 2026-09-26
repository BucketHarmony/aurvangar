using Aurvangar.Sim.Core;
using Aurvangar.Sim.Farming;
using Aurvangar.Sim.Items;

namespace Aurvangar.Sim.Actions;

/// <summary>Crop actions (ECO-12, M6-T2).</summary>
public sealed partial class WorldActions
{
    /// <summary>Plants a crop on an Empty farm tile in reach: it becomes Growing with no progress.</summary>
    public ActionResult Plant(AgentId actor, Int3 tile)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        if (!InReach(a.Cell, tile)) return ActionResult.OutOfReach;
        var t = _sim.Farms.Get(tile);
        if (t is not { State: CropState.Empty }) return ActionResult.InvalidTarget;

        FarmSystem.Plant(t);
        return ActionResult.Ok;
    }

    /// <summary>Harvests a Mature crop in reach: the tile becomes Empty and the actor carries
    /// <see cref="FarmSystem.Yield"/> potatoes (it must have room for them, JOB-01).</summary>
    public ActionResult Harvest(AgentId actor, Int3 tile)
    {
        var r = Actor(actor, out var a);
        if (r != ActionResult.Ok) return r;
        if (!InReach(a.Cell, tile)) return ActionResult.OutOfReach;
        var t = _sim.Farms.Get(tile);
        if (t is not { State: CropState.Mature }) return ActionResult.InvalidTarget;
        var potato = _sim.Content.Item("potato");
        r = CanCarry(a, potato, FarmSystem.Yield);
        if (r != ActionResult.Ok) return r;

        FarmSystem.Harvest(t);
        a.Carried = new ItemStack(potato, a.Carried.Count + FarmSystem.Yield);
        return ActionResult.Ok;
    }
}
