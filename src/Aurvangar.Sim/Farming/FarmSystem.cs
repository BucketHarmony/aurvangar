using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Farming;

/// <summary>ECO-12 crop states.</summary>
public enum CropState : byte { Empty = 0, Growing = 1, Mature = 2 }

/// <summary>A farm tile (ECO-11): a Farmland cell that is its column's top surface, with the crop on it.</summary>
public sealed class FarmTile
{
    public Int3 Cell { get; init; }
    public CropState State { get; set; }

    /// <summary>ECO-12: moist ticks grown so far (<see cref="FarmSystem.MatureTicks"/> once Mature, 0 when Empty).</summary>
    public int Progress { get; set; }

    /// <summary>ECO-13: continuous dry ticks while Growing.</summary>
    public int DryTicks { get; set; }
}

/// <summary>Farm tiles and crops (ECO-11..14, ADR-047). Ticked at ARCH-01 step 5 after the plants: removes tiles whose
/// Farmland is gone or covered, grows or withers crops from <see cref="Water.MoistureMap"/>, then keeps one Plant job
/// per Empty tile and one Harvest job per Mature tile on the board (unclaimed jobs whose tile no longer needs them are
/// withdrawn). Crop changes made for agents go through <see cref="WorldActions.Plant"/> / <see cref="WorldActions.Harvest"/>.</summary>
public sealed class FarmSystem
{
    /// <summary>ECO-12: moist ticks to maturity (3 days).</summary>
    public const int MatureTicks = 7200;

    /// <summary>ECO-13: continuous dry ticks that wither a growing crop (1 day).</summary>
    public const int WitherTicks = 2400;

    /// <summary>ECO-12: potatoes per harvest.</summary>
    public const int Yield = 3;

    /// <summary>JOB-05 work ticks.</summary>
    public const int PlantWorkTicks = 30;
    public const int HarvestWorkTicks = 20;

    private readonly VoxelWorld _world;
    private readonly SortedDictionary<int, FarmTile> _tiles = new();   // cell index → tile

    public FarmSystem(VoxelWorld world) { _world = world; }

    /// <summary>All tiles in ascending cell index order.</summary>
    public IEnumerable<FarmTile> All => _tiles.Values;

    public int Count => _tiles.Count;

    public FarmTile? Get(Int3 c) => _world.InBounds(c) && _tiles.TryGetValue(_world.Index(c), out var t) ? t : null;

    /// <summary>SaveGame load.</summary>
    internal void Restore(FarmTile t) => _tiles.Add(_world.Index(t.Cell), t);

    // ---- commands ----

    /// <summary>ECO-11. For each column of the XZ rectangle (corners in any order, clamped), its top solid cell becomes
    /// an Empty tile and Farmland at once when it is Grass, Dirt, or Farmland without a tile (ADR-047), the cell above
    /// it is standable (PTH-01), and neither is part of a building. A tile whose cell above holds a block plan entry is
    /// skipped (CON-08). Existing tiles are unchanged.</summary>
    public static void Designate(Simulation sim, string tag, int x0, int z0, int x1, int z1)
    {
        var world = sim.World;
        int minX = Math.Max(Math.Min(x0, x1), 0), maxX = Math.Min(Math.Max(x0, x1), world.SizeX - 1);
        int minZ = Math.Max(Math.Min(z0, z1), 0), maxZ = Math.Min(Math.Max(z0, z1), world.SizeZ - 1);
        if (minX > maxX || minZ > maxZ)
        {
            sim.Events.Emit(new CommandRejected(tag, "area is outside the world"));
            return;
        }
        var farms = sim.Farms;
        for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                int y = sim.Moisture.SurfaceY(x, z);
                if (y < 0) continue;
                var c = new Int3(x, y, z);
                var block = world.GetBlock(c);
                if (farms.Get(c) is not null) continue;
                if (block is not (BlockId.Grass or BlockId.Dirt or BlockId.Farmland)) continue;
                if (!sim.PathGrid.IsStandable(c + Int3.Up)) continue;
                if (sim.Buildings.BuildingAt(c) is not null || sim.Buildings.BuildingAt(c + Int3.Up) is not null) continue;
                if (sim.Plans.Has(c + Int3.Up)) continue;   // CON-08 (M8-T2): a block is planned on it
                if (block != BlockId.Farmland) world.SetBlock(c, BlockId.Farmland);
                farms._tiles.Add(world.Index(c), new FarmTile { Cell = c });
            }
    }

    /// <summary>DSG-06: removes the Empty tiles inside the box (the block stays Farmland) and cancels their Plant jobs,
    /// claimed or not. Tiles with a crop stay.</summary>
    public static void CancelIn(Simulation sim, Int3 min, Int3 max)
    {
        var removed = new List<int>();
        foreach (var (i, t) in sim.Farms._tiles)
        {
            var c = t.Cell;
            if (t.State == CropState.Empty && c.X >= min.X && c.X <= max.X && c.Y >= min.Y && c.Y <= max.Y
                && c.Z >= min.Z && c.Z <= max.Z)
                removed.Add(i);
        }
        if (removed.Count == 0) return;
        foreach (var i in removed) sim.Farms._tiles.Remove(i);
        var cancel = new List<Job>();
        foreach (var j in sim.Jobs.All)
            if (j.Kind == JobKind.Plant && sim.Farms.Get(j.Target) is null) cancel.Add(j);
        foreach (var j in cancel) JobRunner.Cancel(sim, j);
    }

    // ---- tick (ARCH-01 step 5) ----

    public void Tick(Simulation sim)
    {
        if (_tiles.Count == 0 && !HasFarmJobs(sim)) return;
        RemoveLostTiles(sim);
        foreach (var t in _tiles.Values)
        {
            if (t.State != CropState.Growing) continue;
            if (sim.Moisture.IsMoist(t.Cell.X, t.Cell.Z))
            {
                t.DryTicks = 0;
                if (++t.Progress >= MatureTicks) t.State = CropState.Mature;
            }
            else if (++t.DryTicks >= WitherTicks)
            {
                t.State = CropState.Empty;   // ECO-13: withered, no yield
                t.Progress = 0;
                t.DryTicks = 0;
            }
        }
        SyncJobs(sim);
    }

    /// <summary>A tile stops being one when its block is no longer Farmland, or the cell above is solid or part of a
    /// building (it is no longer a top surface anyone can farm).</summary>
    private void RemoveLostTiles(Simulation sim)
    {
        List<int>? lost = null;
        foreach (var (i, t) in _tiles)
        {
            var above = t.Cell + Int3.Up;
            if (_world.GetBlock(t.Cell) != BlockId.Farmland || _world.IsSolid(above) || sim.Buildings.BuildingAt(above) is not null)
                (lost ??= new List<int>()).Add(i);
        }
        if (lost is null) return;
        foreach (var i in lost) _tiles.Remove(i);
    }

    private static bool HasFarmJobs(Simulation sim)
    {
        foreach (var j in sim.Jobs.All)
            if (j.Kind is JobKind.Plant or JobKind.Harvest && !Plants.BushHarvest.Is(j)) return true;   // bush jobs belong to PlantSystem
        return false;
    }

    /// <summary>One Plant job per Empty tile, one Harvest job per Mature tile (ECO-14: regardless of storage).
    /// Unclaimed ones whose tile is gone or in another state are withdrawn; claimed ones run on (their action fails if
    /// the tile changed). Jobs visited in ascending id order, tiles in ascending cell index order.</summary>
    private void SyncJobs(Simulation sim)
    {
        var plant = new HashSet<int>();     // lookups only, never enumerated
        var harvest = new HashSet<int>();
        List<Job>? withdraw = null;
        foreach (var j in sim.Jobs.All)
        {
            if (j.Kind is not (JobKind.Plant or JobKind.Harvest) || Plants.BushHarvest.Is(j)) continue;   // bush jobs: PlantSystem
            var want = j.Kind == JobKind.Plant ? CropState.Empty : CropState.Mature;
            var t = Get(j.Target);
            if (!j.IsClaimed && (t is null || t.State != want)) { (withdraw ??= new()).Add(j); continue; }
            if (t is null) continue;
            (j.Kind == JobKind.Plant ? plant : harvest).Add(_world.Index(j.Target));
        }
        if (withdraw is not null)
            foreach (var j in withdraw) JobRunner.Cancel(sim, j);

        foreach (var (i, t) in _tiles)
        {
            var c = t.Cell;
            if (t.State == CropState.Empty && !plant.Contains(i))
                sim.Jobs.Post(JobKind.Plant, c,
                    new[] { JobStep.GoTo(c), JobStep.Work(c, PlantWorkTicks), JobStep.PlantCrop(c) },
                    new[] { Reservation.OnCell(c) });
            else if (t.State == CropState.Mature && !harvest.Contains(i))
                sim.Jobs.Post(JobKind.Harvest, c,
                    new[] { JobStep.GoTo(c), JobStep.Work(c, HarvestWorkTicks), JobStep.HarvestCrop(c) },
                    new[] { Reservation.OnCell(c) });
        }
    }

    /// <summary>JOB-11: after a harvest the job carries the produce on to the nearest storage with room for all of it
    /// (steps and a storage reservation appended); with no such storage it drops the produce as a pile where the
    /// harvester stands, for <see cref="HaulSystem"/> to move later. The tile's cell reservation is let go, so the
    /// tile can be replanted while the produce is on its way.</summary>
    internal static void ChainDelivery(Simulation sim, Agent a, Job job)
    {
        var res = new List<Reservation>();
        foreach (var r in job.Reservations)
            if (r.Kind != ReservationKind.Cell) res.Add(r);
        if (a.Carried.IsEmpty) { sim.Jobs.SetReservations(job, res); return; }
        var to = HaulSystem.NearestStorage(sim, a.Cell, a.Carried.Item, a.Carried.Count);
        if (to is null) job.Steps.Add(JobStep.Drop(a.Cell));
        else
        {
            job.Steps.Add(JobStep.GoToBuilding(to.Id));
            job.Steps.Add(JobStep.DeliverTo(to.Id));
            res.Add(Reservation.IntoStorage(to.Id, a.Carried.Item, a.Carried.Count));
        }
        sim.Jobs.SetReservations(job, res);
    }

    /// <summary>False for a farm Plant / Harvest job (not a bush harvest, ADR-048) whose tile is gone or no longer Empty / Mature (e.g. a harvest job
    /// released mid-delivery by a need). Such a job is withdrawn at the next farm step; until then it is not taken.</summary>
    public static bool StillWanted(Simulation sim, Job job)
    {
        if (job.Kind is not (JobKind.Plant or JobKind.Harvest) || Plants.BushHarvest.Is(job)) return true;
        var want = job.Kind == JobKind.Plant ? CropState.Empty : CropState.Mature;
        return sim.Farms.Get(job.Target) is { } t && t.State == want;
    }

    // ---- WorldActions support ----

    internal static void Plant(FarmTile t)
    {
        t.State = CropState.Growing;
        t.Progress = 0;
        t.DryTicks = 0;
    }

    internal static void Harvest(FarmTile t)
    {
        t.State = CropState.Empty;
        t.Progress = 0;
        t.DryTicks = 0;
    }

    public void AddToHash(ref StateHasher h)
    {
        h.Add(_tiles.Count);
        foreach (var (i, t) in _tiles)
        {
            h.Add(i); h.Add((byte)t.State); h.Add(t.Progress); h.Add(t.DryTicks);
        }
    }
}
