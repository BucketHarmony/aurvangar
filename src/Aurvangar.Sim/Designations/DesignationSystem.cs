using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Designations;

/// <summary>Dig and chop designations (DSG-02..07, ADR-029): the command handlers and the ARCH-01 step 8 tick that
/// posts one job per designated target. State lives in <see cref="DesignationMap"/> (dig marks), on the trees
/// (<see cref="Plant.MarkedForChop"/>) and on the job board, so this class holds none.</summary>
public static class DesignationSystem
{
    /// <summary>JOB-05: Work ticks of a chop.</summary>
    public const int ChopTicks = 80;

    /// <summary>DSG-04 height bonus cap: a dig job never outranks the next JOB-05 kind above it (Plant, 30).</summary>
    public const int MaxHeightBonus = 4;

    private static readonly Int3[] Faces =
    {
        new(0, -1, 0), new(0, 0, -1), new(-1, 0, 0), new(1, 0, 0), new(0, 0, 1), new(0, 1, 0),
    };

    // ---- commands ----

    /// <summary>DSG-02. Solid, diggable cells (never y = 0, WLD-05) that are not a building's footprint, the floor
    /// under it, or a plant's floor get <c>Dig</c>; <c>DigUnreachable</c> becomes <c>Dig</c> again (a retry); others are unchanged.</summary>
    public static void DesignateDig(Simulation sim, string tag, Int3 a, Int3 b)
    {
        var world = sim.World;
        if (!Clamp(world, a, b, out var min, out var max)) { Reject(sim, tag); return; }
        var building = BuildingCells(sim);
        for (int y = Math.Max(min.Y, 1); y <= max.Y; y++)
            for (int z = min.Z; z <= max.Z; z++)
                for (int x = min.X; x <= max.X; x++)
                {
                    var c = new Int3(x, y, z);
                    var def = sim.Content.Block(world.GetBlock(c));
                    if (!def.Solid || !def.Diggable) continue;
                    if (building.Contains(world.Index(c)) || building.Contains(world.Index(c + Int3.Up))) continue;
                    if (sim.Plants.IsOccupied(c + Int3.Up)) continue;   // a plant's floor (WorldActions.Dig refuses it)
                    if (sim.Designations.Get(c) != DesignationMark.Dig) sim.Designations.Set(c, DesignationMark.Dig);
                }
    }

    /// <summary>DSG-05. Trees whose base is inside the XZ rectangle, any Y. Re-marking clears a chop give-up.</summary>
    public static void DesignateChop(Simulation sim, string tag, int x0, int z0, int x1, int z1)
    {
        var world = sim.World;
        if (!Clamp(world, new Int3(x0, 0, z0), new Int3(x1, world.SizeY - 1, z1), out var min, out var max))
        {
            Reject(sim, tag);
            return;
        }
        foreach (var p in sim.Plants.All)
        {
            if (p.Kind != PlantKind.Tree || !Inside(p.Base, min, max)) continue;
            p.MarkedForChop = true;
            p.ChopUnreachable = false;
        }
    }

    /// <summary>DSG-06. Clears dig marks in the box and chop marks of trees whose base is in it, and cancels their
    /// jobs (a claimed job is released and its agent goes idle). Farm tiles are M6-T2.</summary>
    public static void Cancel(Simulation sim, string tag, Int3 a, Int3 b)
    {
        if (!Clamp(sim.World, a, b, out var min, out var max)) { Reject(sim, tag); return; }
        var cleared = new List<Int3>();
        foreach (var (c, _) in sim.Designations.All)
            if (Inside(c, min, max)) cleared.Add(c);
        foreach (var c in cleared) sim.Designations.Set(c, DesignationMark.None);
        foreach (var p in sim.Plants.All)
        {
            if (p.Kind != PlantKind.Tree || !Inside(p.Base, min, max)) continue;
            p.MarkedForChop = false;
            p.ChopUnreachable = false;
        }

        var cancel = new List<Job>();
        foreach (var j in sim.Jobs.All)
        {
            if (j.Kind == JobKind.Dig && Inside(j.Target, min, max)) cancel.Add(j);
            else if (j.Kind == JobKind.Chop && sim.Plants.Get(ChopTree(j)) is { } p && Inside(p.Base, min, max)) cancel.Add(j);
        }
        foreach (var j in cancel) JobRunner.Cancel(sim, j);
    }

    // ---- tick (ARCH-01 step 8) ----

    /// <summary>DSG-03/04 and chop posting. One Dig job per <c>Dig</c> mark on a solid, diggable, exposed cell without
    /// a job; a mark whose cell is no longer solid (and, for Dig, has no job) is cleared. Open dig jobs get priority
    /// 25 + min(y − lowest marked y, <see cref="MaxHeightBonus"/>), recomputed every tick. One Chop job per marked,
    /// not given-up tree without a job. Marks, trees and jobs are visited in ascending index / id order.</summary>
    public static void Tick(Simulation sim)
    {
        var marks = sim.Designations;
        var world = sim.World;
        var digJobs = new Dictionary<int, Job>();   // lookups only, never enumerated
        var chopJobs = new HashSet<int>();
        foreach (var j in sim.Jobs.All)
        {
            if (j.Kind == JobKind.Dig && world.InBounds(j.Target)) digJobs[world.Index(j.Target)] = j;
            else if (j.Kind == JobKind.Chop) chopJobs.Add(ChopTree(j).Value);
        }

        if (marks.Count > 0) PostDigs(sim, digJobs);
        foreach (var p in sim.Plants.All)
        {
            if (p.Kind != PlantKind.Tree || !p.MarkedForChop || p.ChopUnreachable || chopJobs.Contains(p.Id.Value)) continue;
            sim.Jobs.Post(JobKind.Chop, p.Base,
                new[] { JobStep.GoTo(p.Base), JobStep.Work(p.Base, ChopTicks), JobStep.Chop(p.Id) },
                new[] { Reservation.OnCell(p.Base) });
        }
    }

    private static void PostDigs(Simulation sim, Dictionary<int, Job> digJobs)
    {
        var world = sim.World;
        var marks = sim.Designations;
        int lowest = int.MaxValue;
        var stale = new List<Int3>();
        foreach (var (c, mark) in marks.All)
        {
            if (mark != DesignationMark.Dig)
            {
                if (!world.IsSolid(c)) stale.Add(c);   // an unreachable target that is gone anyway
                continue;
            }
            if (world.IsSolid(c)) lowest = Math.Min(lowest, c.Y);
            else if (!digJobs.ContainsKey(world.Index(c))) stale.Add(c);
        }
        foreach (var c in stale) marks.Set(c, DesignationMark.None);
        if (lowest == int.MaxValue) return;

        foreach (var (c, mark) in marks.All)
        {
            if (mark != DesignationMark.Dig) continue;
            int priority = Job.DefaultPriority(JobKind.Dig) + Math.Min(c.Y - lowest, MaxHeightBonus);
            if (digJobs.TryGetValue(world.Index(c), out var job))
            {
                if (!job.IsClaimed) job.Priority = priority;
                continue;
            }
            var def = sim.Content.Block(world.GetBlock(c));
            if (!def.Solid || !def.Diggable || !Exposed(world, c)) continue;
            sim.Jobs.Post(JobKind.Dig, c,
                new[] { JobStep.GoTo(c, GoalMode.Dig), JobStep.Work(c, def.Hardness), JobStep.Dig(c) },
                new[] { Reservation.OnCell(c) }, priority);
        }
    }

    // ---- helpers ----

    /// <summary>DSG-03: at least one of the 6 face neighbors is not solid (out of bounds above y = 0 is air, WLD-04).</summary>
    public static bool Exposed(VoxelWorld world, Int3 c)
    {
        foreach (var d in Faces)
            if (!world.IsSolid(c + d)) return true;
        return false;
    }

    /// <summary>The tree a chop job targets (its Chop step).</summary>
    public static PlantId ChopTree(Job job)
    {
        foreach (var s in job.Steps)
            if (s.Kind == StepKind.Chop) return new PlantId(s.Target);
        return default;
    }

    private static HashSet<int> BuildingCells(Simulation sim)
    {
        var set = new HashSet<int>();
        foreach (var b in sim.Buildings.All)
            foreach (var f in b.FootprintCells())
                if (sim.World.InBounds(f)) set.Add(sim.World.Index(f));
        return set;
    }

    /// <summary>Orders the corners and intersects the box with the world; false if nothing is left.</summary>
    private static bool Clamp(VoxelWorld w, Int3 a, Int3 b, out Int3 min, out Int3 max)
    {
        min = new Int3(Math.Max(Math.Min(a.X, b.X), 0), Math.Max(Math.Min(a.Y, b.Y), 0), Math.Max(Math.Min(a.Z, b.Z), 0));
        max = new Int3(Math.Min(Math.Max(a.X, b.X), w.SizeX - 1), Math.Min(Math.Max(a.Y, b.Y), w.SizeY - 1),
            Math.Min(Math.Max(a.Z, b.Z), w.SizeZ - 1));
        return min.X <= max.X && min.Y <= max.Y && min.Z <= max.Z;
    }

    private static bool Inside(Int3 c, Int3 min, Int3 max) =>
        c.X >= min.X && c.X <= max.X && c.Y >= min.Y && c.Y <= max.Y && c.Z >= min.Z && c.Z <= max.Z;

    private static void Reject(Simulation sim, string tag) =>
        sim.Events.Emit(new CommandRejected(tag, "box is outside the world"));
}
