using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;

namespace Aurvangar.Sim.Buildings;

/// <summary>M5-T2 construction flow (BLD-05..09, ADR-041): the PlaceBuilding and Deconstruct command handlers, the
/// ARCH-01 step 7 tick that keeps each site's Deliver, Construct and Deconstruct jobs, and the state changes of a
/// site (start, cancel). Completion and removal happen inside <see cref="Actions.WorldActions.Work"/>. Stateless: the
/// state is on the buildings and the job board.</summary>
public static partial class Construction
{
    // ---- commands ----

    /// <summary>BLD-05. Also clears dig marks on the ground under the blueprint and cancels their jobs (DSG-02 never
    /// marks a building's floor; ADR-041).</summary>
    public static void Place(Simulation sim, string tag, string defId, Int3 origin, int rotation)
    {
        var def = sim.Content.FindBuilding(defId);
        if (def is null) { Reject(sim, tag, "UnknownBuilding"); return; }
        var r = sim.Buildings.TryPlaceBlueprint(def, origin, rotation, out var b);
        if (r != PlacementResult.Ok) { Reject(sim, tag, r.ToString()); return; }
        ClearGroundMarks(sim, b!);
        sim.Events.Emit(new BuildingPlaced(b!.Id));
    }

    /// <summary>BLD-09: cancel a blueprint or site, or start deconstructing a complete building. Rejected with
    /// SupportsBlocks when removing a complete building's footprint would unground a built block (CON-10). A
    /// prebuilt-only building is rejected (PrebuiltOnly) unless it is removable when empty (the wagon, BLD-17): then
    /// only while it still holds items (NotEmpty).</summary>
    public static void Deconstruct(Simulation sim, string tag, BuildingId id)
    {
        var b = sim.Buildings.Get(id);
        if (b is null) { Reject(sim, tag, "UnknownBuilding"); return; }
        if (b.Def.PrebuiltOnly && !b.Def.RemovableWhenEmpty) { Reject(sim, tag, "PrebuiltOnly"); return; }
        if (b.Def.RemovableWhenEmpty && b.Stored.Count > 0) { Reject(sim, tag, "NotEmpty"); return; }   // BLD-17
        if (b.State == BuildingState.Deconstructing) { Reject(sim, tag, "AlreadyDeconstructing"); return; }
        if (HasBuildingOnTop(sim, b)) { Reject(sim, tag, "BuildingOnTop"); return; }
        if (b.State == BuildingState.Complete && SupportsBlocks(sim, b)) { Reject(sim, tag, "SupportsBlocks"); return; }
        if (b.State == BuildingState.Complete)
        {
            b.State = BuildingState.Deconstructing;   // the tick posts its Deconstruct job
            b.Progress = 0;
            return;
        }
        Cancel(sim, b);
    }

    // ---- site state changes ----

    /// <summary>BLD-07: the site's footprint stops being walkable (PTH-02); agents standing in it move to the stand
    /// cell, and loose piles in it move to the nearest free cell outside (ECO-08 spiral from the stand cell).</summary>
    public static void Start(Simulation sim, Building b)
    {
        b.State = BuildingState.UnderConstruction;
        var stand = StandCell(sim, b);
        foreach (var c in b.FootprintCells()) sim.PathGrid.SetSite(c, true);
        MoveAgentsOut(sim, b, stand, alsoOnTop: false);
        foreach (var c in b.FootprintCells())
            if (!sim.Piles.At(c).IsEmpty) sim.Actions.MovePile(c, stand);
    }

    /// <summary>BLD-07 and GRV-07: living agents standing in (or stepping into) a footprint cell, and with
    /// <paramref name="alsoOnTop"/> a cell right above the footprint, are moved to <paramref name="stand"/>. A walk in
    /// progress is planned again from there.</summary>
    internal static void MoveAgentsOut(Simulation sim, Building b, Int3 stand, bool alsoOnTop)
    {
        foreach (var a in sim.Agents.All)
        {
            if (!a.IsAlive || !(On(a.Cell) || (alsoOnTop && On(a.NextCell)))) continue;
            AgentMovement.Halt(a);
            a.Cell = stand;
            a.NextCell = stand;
            // A walk in progress is planned again from the new cell (instead of failing the job).
            if (sim.Jobs.Get(a.CurrentJob) is { } job && job.ClaimedBy == a.Id && a.StepIndex < job.Steps.Count
                && job.Steps[a.StepIndex].Kind == StepKind.GoTo)
                a.StepProgress = 0;
        }

        bool On(Int3 c) => b.Covers(c) || (alsoOnTop && b.Covers(c + Int3.Down));
    }

    /// <summary>BLD-09 cancel: every job of the site is cancelled (carried materials are dropped by their agents), the
    /// delivered materials go back as piles at the stand cell, and the building is removed.</summary>
    public static void Cancel(Simulation sim, Building b)
    {
        foreach (var j in SiteJobs(sim, b.Id)) JobRunner.Cancel(sim, j);
        if (b.State == BuildingState.UnderConstruction)
            foreach (var c in b.FootprintCells()) sim.PathGrid.SetSite(c, false);
        var stand = StandCell(sim, b);
        foreach (var (item, n) in b.Delivered) sim.Actions.PlacePile(stand, new ItemId(item), n);
        sim.Buildings.Remove(b);
        sim.Events.Emit(new BuildingRemoved(b.Id));
    }

    // ---- queries ----

    /// <summary>JOB-05: Work ticks of a Deconstruct job, half the build ticks (at least 1).</summary>
    public static int DeconstructTicks(BuildingDef def) => Math.Max(def.BuildTicks / 2, 1);

    /// <summary>Where refunds land, agents are moved to and a pump's worker stands: the entrance cell; when it is not
    /// standable, the cell below it (stacked levees, ADR-040), else for a <c>waterEdge</c> building (the pump) the
    /// cell above it when standable (the entrance is inside the next bank step, ADR-055). A building with no entrance
    /// (the levee, ADR-076) uses the first free standable cell in reach of its footprint, else the cell above its top
    /// (refunds then spiral out from there, ECO-08).</summary>
    public static Int3 StandCell(Simulation sim, Building b)
    {
        if (!b.HasEntrance)
            return sim.Buildings.ReachStandCells(b.Def, b.Origin, b.Rotation) is { Count: > 0 } free
                ? free[0] : b.Origin + new Int3(0, b.Def.Footprint[1], 0);
        var e = b.EntranceCell;
        if (sim.PathGrid.IsStandable(e)) return e;
        if (sim.PathGrid.IsStandable(e + Int3.Down)) return e + Int3.Down;
        if (BuildingSystem.RaisesStand(b.Def) && sim.PathGrid.IsStandable(e + Int3.Up)) return e + Int3.Up;
        return e;
    }

    /// <summary>BLD-04: a site on another building waits until that building is complete.</summary>
    public static bool WaitsForBelow(Simulation sim, Building b)
    {
        foreach (var c in BuildingShape.BottomLayer(b.Def, b.Origin, b.Rotation))
            if (sim.Buildings.BuildingAt(c + Int3.Down) is { } under && under != b && under.State != BuildingState.Complete)
                return true;
        return false;
    }

    /// <summary>CON-10: the building's BuildingSolid footprint is ground for a built block that would not be grounded
    /// without it. Only a building that sets blocks and is complete (or being deconstructed) has such a footprint.</summary>
    public static bool SupportsBlocks(Simulation sim, Building b) =>
        b.Def.SetsBlocks && (b.State is BuildingState.Complete or BuildingState.Deconstructing)
        && Blocks.Support.Depends(sim, b.FootprintCells().ToList());

    /// <summary>Another building stands on this one's top layer (stacked levees).</summary>
    public static bool HasBuildingOnTop(Simulation sim, Building b)
    {
        foreach (var c in b.FootprintCells())
            if (sim.Buildings.BuildingAt(c + Int3.Up) is { } above && above != b) return true;
        return false;
    }

    /// <summary>Cost items still to deliver (0 when fully delivered or not in the cost).</summary>
    public static int Remaining(Simulation sim, Building b, ItemId item)
    {
        var key = sim.Content.ItemDef(item).Id;
        if (!b.Def.Cost.TryGetValue(key, out var cost)) return 0;
        return Math.Max(cost - (b.Delivered.TryGetValue(item.Value, out var n) ? n : 0), 0);
    }

    /// <summary>The building's cost as (item, count) pairs in ascending item id (never Dictionary order).</summary>
    public static List<(ItemId Item, int Count)> Cost(Simulation sim, BuildingDef def)
    {
        var list = new List<(ItemId, int)>(def.Cost.Count);
        foreach (var key in def.Cost.Keys.OrderBy(k => sim.Content.Item(k).Value))
            list.Add((sim.Content.Item(key), def.Cost[key]));
        return list;
    }

    /// <summary>A construction job's site: the target of its last step (DeliverTo or the site Work).</summary>
    public static BuildingId SiteOf(Job job) => new(job.Steps[^1].Target);

    /// <summary>A job posted by this class, known by its shape (other code and tests may post Construct jobs on cells):
    /// a Deliver ending in DeliverTo, or a Construct/Deconstruct ending in Work on a building.</summary>
    public static bool IsSiteJob(Job job)
    {
        if (job.Steps.Count == 0) return false;
        var last = job.Steps[^1];
        return job.Kind switch
        {
            JobKind.Deliver => job.Steps.Count == 4 && last.Kind == StepKind.DeliverTo && job.Steps[1].Kind == StepKind.PickUpFromStorage,
            JobKind.Construct or JobKind.Deconstruct => last.Kind == StepKind.Work && last.Goal == GoalMode.Building,
            _ => false,
        };
    }

    private static List<Job> SiteJobs(Simulation sim, BuildingId site)
    {
        var list = new List<Job>();
        foreach (var j in sim.Jobs.All)
            if (IsSiteJob(j) && SiteOf(j) == site) list.Add(j);
        return list;
    }

    internal static void ClearGroundMarks(Simulation sim, Building b)
    {
        var ground = new List<Int3>();
        foreach (var c in BuildingShape.BottomLayer(b.Def, b.Origin, b.Rotation))
            if (sim.Designations.Get(c + Int3.Down) != DesignationMark.None) ground.Add(c + Int3.Down);
        if (ground.Count == 0) return;
        foreach (var g in ground) sim.Designations.Set(g, DesignationMark.None);
        var cancel = new List<Job>();
        foreach (var j in sim.Jobs.All)
            if (j.Kind == JobKind.Dig && ground.Contains(j.Target)) cancel.Add(j);
        foreach (var j in cancel) JobRunner.Cancel(sim, j);
    }

    private static void Reject(Simulation sim, string tag, string reason) =>
        sim.Events.Emit(new CommandRejected(tag, reason));
}
