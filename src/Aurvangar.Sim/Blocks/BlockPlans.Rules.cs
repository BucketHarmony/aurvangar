using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;

namespace Aurvangar.Sim.Blocks;

/// <summary>CON-08 plan validity and CON-05 statuses (M8-T2). Pure functions of the simulation: nothing is cached
/// across ticks.</summary>
public sealed partial class BlockPlans
{
    /// <summary>CON-08: whether <paramref name="cell"/> may get an entry, given the command's own cells
    /// <paramref name="pending"/> (for plan support; null for none). The first failing check is the answer.</summary>
    public static PlanResult CanPlan(Simulation sim, Int3 cell, IReadOnlyList<Int3>? pending)
    {
        var r = CheckCell(sim, cell);
        if (r != PlanResult.Ok) return r;
        var list = new List<Int3> { cell };
        if (pending is not null)
            foreach (var c in pending)
                if (c != cell && CheckCell(sim, c) == PlanResult.Ok) list.Add(c);
        return Support.PlanSupported(sim, list).Contains(sim.World.Index(cell)) ? PlanResult.Ok : PlanResult.Unsupported;
    }

    /// <summary>CON-08 for a whole command's cells with one plan-support flood (the view's ghost, VIEW-21, M8-T5):
    /// <c>result[k] == CanPlan(sim, cells[k], cells)</c>. <see cref="CanPlan"/> floods once per call, so the view must
    /// not call it per ghost cell.</summary>
    public static PlanResult[] CanPlanAll(Simulation sim, IReadOnlyList<Int3> cells)
    {
        var result = new PlanResult[cells.Count];
        var valid = new List<Int3>();
        for (int k = 0; k < cells.Count; k++)
        {
            result[k] = CheckCell(sim, cells[k]);
            if (result[k] == PlanResult.Ok) valid.Add(cells[k]);
        }
        var supported = Support.PlanSupported(sim, valid);
        for (int k = 0; k < cells.Count; k++)
            if (result[k] == PlanResult.Ok && !supported.Contains(sim.World.Index(cells[k]))) result[k] = PlanResult.Unsupported;
        return result;
    }

    /// <summary>CON-05 for every entry at or below <paramref name="maxY"/> with one shared scan (the view's plan ghosts,
    /// VIEW-22, M8-T5): each status equals <see cref="StatusOf(Simulation, Int3)"/>. Ascending cell index.</summary>
    public List<(Int3 Cell, PlanEntry Entry, BuildStatus Status)> Statuses(Simulation sim, int maxY = int.MaxValue)
    {
        var list = new List<(Int3, PlanEntry, BuildStatus)>();
        if (_entries.Count == 0) return list;
        var scan = new BuildScan(sim, BlockBuildSystem.HeldCells(sim));
        foreach (var (i, e) in _entries)
        {
            var c = _world.CellOf(i);
            if (c.Y > maxY) break;   // ascending index is ascending y
            list.Add((c, e, StatusOf(scan, c, e, default, out _)));
        }
        return list;
    }

    /// <summary>CON-08 checks 1..5 (everything but plan support).</summary>
    internal static PlanResult CheckCell(Simulation sim, Int3 cell)
    {
        var world = sim.World;
        if (!world.InBounds(cell) || cell.Y == 0) return PlanResult.OutOfWorld;
        if (world.IsSolid(cell)) return PlanResult.Solid;
        if (sim.Buildings.BuildingAt(cell) is not null || sim.Buildings.IsEntranceOrStand(cell)) return PlanResult.Building;
        if (sim.Plants.IsOccupied(cell)) return PlanResult.Plant;
        if (sim.Farms.Get(cell + Int3.Down) is not null) return PlanResult.Farm;
        return PlanResult.Ok;
    }

    /// <summary>CON-05: the status of the entry at <paramref name="cell"/>, or null when there is none.</summary>
    public BuildStatus? StatusOf(Simulation sim, Int3 cell)
    {
        if (Get(cell) is not { } e) return null;
        var scan = new BuildScan(sim, BlockBuildSystem.HeldCells(sim));
        return StatusOf(scan, cell, e, default, out _);
    }

    /// <summary>CON-05 with a per-tick <paramref name="scan"/> (agent and storage regions, held cells; a null
    /// <see cref="BuildScan.Held"/> skips check 2, "ignoring its own hold"). <paramref name="builder"/> is not counted
    /// by the strand check (its own stand cell is). <paramref name="stands"/> gets the strand-filtered stand cells
    /// when checks 1..8 pass.</summary>
    internal static BuildStatus StatusOf(BuildScan scan, Int3 cell, PlanEntry e, AgentId builder, out List<Int3>? stands)
    {
        stands = null;
        var sim = scan.Sim;
        var world = sim.World;
        int index = world.Index(cell);
        if (e.State != PlanState.Released) return BuildStatus.Planned;
        if (scan.Held is not null && scan.Held.ContainsKey(index)) return BuildStatus.InJob;
        if (sim.GiveUps.Count > 0 && sim.GiveUps.IsGivenUp(GiveUpSource.Build, index)) return BuildStatus.GivenUp;
        if (sim.Plans.Has(cell + Int3.Down)) return BuildStatus.BelowFirst;
        if (!Support.Placement(world, cell)) return BuildStatus.NoSupport;
        if (sim.Agents.AnyHolds(cell) || sim.Agents.AnyHolds(cell + Int3.Down) || !sim.Piles.At(cell).IsEmpty
            || sim.Plants.IsOccupied(cell))
            return BuildStatus.Occupied;

        var all = JobGoals.BuildStandCells(sim, cell, strandFree: false, prefer: false);
        bool access = false;
        foreach (var c in all)
            if (scan.AgentRegions.Contains(sim.Regions.RegionOf(c))) { access = true; break; }
        if (!access) return BuildStatus.NoAccess;

        var ok = JobGoals.BuildStandCells(sim, cell, strandFree: true, prefer: false);
        if (ok.Count == 0 || PlaceStrand.StrandsOthers(sim, cell, builder)) return BuildStatus.WouldStrand;

        var (item, cost) = sim.Content.CostOf(e.Block);
        if (!scan.HasMaterial(item, cost, ok)) return BuildStatus.NoMaterial;
        stands = ok;
        return BuildStatus.Ready;
    }
}

/// <summary>What one tick's (or one query's) status checks share: the living agents' regions, each storage's goal
/// regions, and the cells Build jobs hold. Derived, never stored.</summary>
internal sealed class BuildScan
{
    public readonly Simulation Sim;
    public readonly SortedSet<int> AgentRegions = new();
    /// <summary>Cell index -> the Build job holding it (lookups only), or null to skip the CON-05 InJob check.</summary>
    public readonly Dictionary<int, Job>? Held;
    private readonly Dictionary<int, SortedSet<int>> _storageRegions = new();   // building id -> regions (lookups only)

    public BuildScan(Simulation sim, Dictionary<int, Job>? held)
    {
        Sim = sim;
        Held = held;
        foreach (var a in sim.Agents.All)
        {
            if (!a.IsAlive) continue;
            int r = sim.Regions.RegionOf(a.Cell);
            if (r != Paths.Regions.None) AgentRegions.Add(r);
        }
    }

    /// <summary>Regions of a building's GoToBuilding goals.</summary>
    public SortedSet<int> RegionsOf(Building b)
    {
        if (_storageRegions.TryGetValue(b.Id.Value, out var set)) return set;
        set = new SortedSet<int>();
        foreach (var g in JobGoals.For(Sim, JobStep.GoToBuilding(b.Id)))
        {
            int r = Sim.Regions.RegionOf(g);
            if (r != Paths.Regions.None) set.Add(r);
        }
        _storageRegions[b.Id.Value] = set;
        return set;
    }

    /// <summary>A complete storage that accepts the item serves one of the stand cells' regions.</summary>
    public bool Serves(Building s, ItemId item, IReadOnlyList<Int3> stands)
    {
        if (s.State != BuildingState.Complete || s.Def.Storage is null || !Sim.Actions.Accepts(s, item)) return false;
        var regions = RegionsOf(s);
        foreach (var c in stands)
            if (regions.Contains(Sim.Regions.RegionOf(c))) return true;
        return false;
    }

    /// <summary>CON-05 check 9: some storage serving the stand cells holds at least one block's cost unpromised.</summary>
    public bool HasMaterial(ItemId item, int cost, IReadOnlyList<Int3> stands)
    {
        foreach (var s in Sim.Buildings.All)
            if (Sim.Jobs.StorageStock(s, item) >= cost && Serves(s, item, stands)) return true;
        return false;
    }
}
